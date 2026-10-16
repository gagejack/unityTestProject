using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Mobs
{
    /// <summary>
    /// Mob AI: watches for the target, chases it (leading its movement), attacks in range through
    /// <see cref="MobAttackDriver"/>, investigates where it last saw the target, searches nearby, then
    /// returns home.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(MobAttackDriver))]
    public sealed class MobBrain : MonoBehaviour
    {
        public enum BehaviourState { Watching, Tracking, Investigating, Searching, Attacking }

        [Header("Target and sight")]
        [Tooltip("Who the mob hunts. Left empty, it hunts the player.")]
        [SerializeField] private Transform target;
        [Tooltip("Eye point for sight checks; its forward is the centre of the view cone. Empty uses 1 m above the mob, facing its forward.")]
        [SerializeField] private Transform sensor;
        [Tooltip("Farthest distance the mob can see the target, in meters.")]
        [SerializeField] private float sightRange = 22f;
        [Tooltip("Width of the view cone, in degrees.")]
        [SerializeField, Range(30f, 360f)] private float fieldOfView = 200f;
        [Tooltip("Seconds the mob keeps heading for the last place it saw the target before it starts searching.")]
        [SerializeField, Min(.1f)] private float memorySeconds = 6f;
        [Tooltip("Layers that block the mob's sight.")]
        [SerializeField] private LayerMask environmentMask = ~0;
        [Header("Pursuit")]
        [Tooltip("Travel speed in meters per second while the target is visible. Investigating and searching use 60 % of it.")]
        [SerializeField, Min(.1f)] private float chaseSpeed = 3.6f;
        [Tooltip("Seconds between path updates while chasing or investigating.")]
        [SerializeField, Min(.1f)] private float repathInterval = .25f;
        [Tooltip("How far ahead to lead a moving target, in seconds of its current velocity.")]
        [SerializeField, Range(0f, 1f)] private float predictionSeconds = .32f;
        [Tooltip("Seconds spent searching around the last known position before returning home.")]
        [SerializeField, Min(.1f)] private float searchSeconds = 7f;

        const float SightInterval = .1f;
        const float TurnSpeed = 240f;
        const float AttackFacingAngle = 25f;

        NavMeshAgent agent;
        MobAttackDriver attack;
        readonly RaycastHit[] hits = new RaycastHit[32];
        Vector3 home, lastSeen, previousTarget, estimatedVelocity;
        float lastSeenAt = float.NegativeInfinity, nextSight, nextPath, stateTime, cooldown, searchUntil, nextSearchPoint;
        bool initialized;

        public BehaviourState State { get; private set; }
        public Transform Target => target;
        public Vector3 LastKnownPosition => lastSeen;
        public bool TargetVisible { get; private set; }
        public int AttacksStarted { get; private set; }
        public int HitsLanded { get; private set; }
        public void SetTarget(Transform value) => target = value;

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            attack = GetComponent<MobAttackDriver>();
            if (!target) target = PlayerPresence.Current;
            home = transform.position;
            if (target) previousTarget = target.position;
            if (!NavMesh.SamplePosition(transform.position, out var placement, 3f, agent.areaMask))
            {
                Debug.LogWarning(name + " needs a baked navigation surface before it can pursue the player.", this);
                enabled = false;
                return;
            }
            agent.enabled = true;
            agent.Warp(placement.position);
            agent.updateRotation = false;
            agent.speed = chaseSpeed;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || !target || !agent.enabled) return;
            float dt = Time.deltaTime;
            cooldown = Mathf.Max(0f, cooldown - dt);
            stateTime += dt;
            TrackTargetVelocity(dt);
            if (Time.time >= nextSight)
            {
                nextSight = Time.time + SightInterval;
                TargetVisible = CanSeeTarget();
                if (TargetVisible) { lastSeen = TargetFeet(); lastSeenAt = Time.time; }
            }
            if (State == BehaviourState.Attacking)
            {
                // The swing is committed in its original direction so the player can dodge it.
                if (attack.TickAttack(target, TargetVisible, stateTime)) HitsLanded++;
                if (stateTime < attack.AttackDuration) return;
                cooldown = attack.Cooldown;
                SetState(BehaviourState.Tracking);
            }
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;
            agent.speed = TargetVisible ? chaseSpeed : chaseSpeed * .6f;
            if (TargetVisible) Chase(dt);
            else if (Time.time - lastSeenAt < memorySeconds)
            {
                SetState(BehaviourState.Investigating);
                GoTo(lastSeen);
                TurnAlongPath(dt);
                if (PlanarDistance(transform.position, lastSeen) < .7f) BeginSearch();
            }
            else if (State == BehaviourState.Searching && Time.time < searchUntil)
            {
                if (Time.time >= nextSearchPoint)
                {
                    nextSearchPoint = Time.time + 1.6f;
                    Vector2 offset = Random.insideUnitCircle * 2.4f;
                    GoTo(lastSeen + new Vector3(offset.x, 0f, offset.y), true);
                }
                TurnAlongPath(dt);
            }
            else if (State == BehaviourState.Investigating || State == BehaviourState.Tracking) BeginSearch();
            else
            {
                SetState(BehaviourState.Watching);
                GoTo(home);
                TurnAlongPath(dt);
            }
        }

        void Chase(float dt)
        {
            SetState(BehaviourState.Tracking);
            GoTo(lastSeen + estimatedVelocity * predictionSeconds);
            TurnTowards(agent.desiredVelocity.sqrMagnitude > .04f ? transform.position + agent.desiredVelocity : lastSeen, dt);
            if (Vector3.Distance(transform.position, lastSeen) > attack.Range) return;
            agent.isStopped = true;
            agent.ResetPath();
            TurnTowards(lastSeen, dt);
            if (cooldown <= 0f && Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(lastSeen - transform.position, Vector3.up)) < AttackFacingAngle)
            {
                attack.BeginAttack();
                AttacksStarted++;
                SetState(BehaviourState.Attacking);
            }
        }

        // Smoothed planar velocity of the target, used to lead it. Clamped so a teleport is not read as a sprint.
        void TrackTargetVelocity(float dt)
        {
            Vector3 measured = (target.position - previousTarget) / Mathf.Max(dt, .001f);
            previousTarget = target.position;
            estimatedVelocity = Vector3.Lerp(estimatedVelocity, Vector3.ClampMagnitude(Vector3.ProjectOnPlane(measured, Vector3.up), 7f), 1f - Mathf.Exp(-dt * 5f));
        }

        void BeginSearch()
        {
            SetState(BehaviourState.Searching);
            searchUntil = Time.time + searchSeconds;
            nextSearchPoint = 0f;
        }

        void SetState(BehaviourState value)
        {
            if (State == value) return;
            State = value;
            stateTime = 0f;
        }

        Vector3 TargetFeet()
        {
            var controller = target.GetComponent<CharacterController>();
            return controller ? new Vector3(target.position.x, controller.bounds.min.y, target.position.z) : target.position;
        }

        bool CanSeeTarget()
        {
            Vector3 origin = sensor ? sensor.position : transform.position + Vector3.up;
            Vector3 direction = target.position + Vector3.up * .35f - origin;
            if (direction.magnitude > sightRange || Vector3.Angle(sensor ? sensor.forward : transform.forward, direction) > fieldOfView * .5f) return false;
            return !SightBlocked(origin, direction.normalized, direction.magnitude);
        }

        void GoTo(Vector3 destination, bool immediate = false)
        {
            if (!immediate && Time.time < nextPath) return;
            nextPath = Time.time + repathInterval;
            if (NavMesh.SamplePosition(destination, out var point, 2f, agent.areaMask)) agent.SetDestination(point.position);
        }

        void TurnAlongPath(float dt)
        {
            if (agent.desiredVelocity.sqrMagnitude > .035f) TurnTowards(transform.position + agent.desiredVelocity, dt);
        }

        void TurnTowards(Vector3 point, float dt)
        {
            Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), TurnSpeed * dt);
        }

        // True when anything other than the mob itself or the target is between the eye and the target.
        bool SightBlocked(Vector3 origin, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, environmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i].transform;
                if (hit == transform || hit.IsChildOf(transform) || hit == target || hit.IsChildOf(target)) continue;
                return true;
            }
            return false;
        }

        static float PlanarDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

        private void OnDisable()
        {
            if (agent && agent.enabled && agent.isOnNavMesh) { agent.updatePosition = true; agent.isStopped = true; }
        }

        private void OnDrawGizmosSelected()
        {
            var driver = GetComponent<MobAttackDriver>();
            Gizmos.color = Color.cyan;
            if (driver) Gizmos.DrawWireSphere(transform.position, driver.Range);
            if (!Application.isPlaying || !initialized) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastSeen, .2f);
            if (agent && agent.enabled && agent.isOnNavMesh && agent.hasPath)
            {
                var points = agent.path.corners;
                for (int i = 1; i < points.Length; i++) Gizmos.DrawLine(points[i - 1], points[i]);
            }
        }
    }
}
