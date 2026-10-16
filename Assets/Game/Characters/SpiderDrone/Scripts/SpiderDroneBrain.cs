using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace StarterGame.Characters
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SpiderDroneBrain : MonoBehaviour
    {
        public enum BehaviourState { Watching, Tracking, Investigating, Searching, Rearing, Leaping, Recovering, Attacking }

        [Header("Target and sight")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform sensor;
        [SerializeField] private float sightRange = 22f;
        [SerializeField, Range(30f, 360f)] private float fieldOfView = 200f;
        [SerializeField, Min(.1f)] private float memorySeconds = 6f;
        [SerializeField] private LayerMask environmentMask = ~0;
        [Header("Pursuit")]
        [SerializeField, Min(.1f)] private float chaseSpeed = 3.6f;
        [SerializeField, Min(.1f)] private float repathInterval = .25f;
        [SerializeField, Range(0f, 1f)] private float predictionSeconds = .32f;
        [SerializeField, Min(.1f)] private float searchSeconds = 7f;
        [Header("Pounce")]
        [SerializeField, Min(.5f)] private float attackRange = 3.3f;
        [SerializeField, Min(.2f)] private float rearDuration = .6f;
        [SerializeField, Min(.2f)] private float leapDuration = .65f;
        [SerializeField, Min(.1f)] private float leapHeight = .95f;
        [SerializeField, Min(.2f)] private float recoveryDuration = .75f;
        [SerializeField, Min(.5f)] private float attackCooldown = 2.8f;
        [SerializeField] private UnityEvent<Transform> onPounceHit = new UnityEvent<Transform>();
        [SerializeField] private bool showStatus = true;

        private NavMeshAgent agent;
        private MobAttackDriver mobAttack;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private Vector3 home, lastSeen, previousTarget, estimatedVelocity, leapStart, leapEnd, previousPosition;
        private Vector3 recoveryStart, recoveryFloor;
        private float lastSeenAt = float.NegativeInfinity, nextSight, nextPath, stateTime, cooldown, searchUntil, nextSearchPoint;
        private bool hitThisLeap, initialized;
        public BehaviourState State { get; private set; }
        public Transform Target => target;
        public Vector3 LastKnownPosition => lastSeen;
        public Vector3 Velocity { get; private set; }
        public bool TargetVisible { get; private set; }
        public bool Airborne => State == BehaviourState.Leaping;
        public float StateProgress => Mathf.Clamp01(stateTime / (State == BehaviourState.Rearing ? rearDuration : State == BehaviourState.Leaping ? leapDuration : recoveryDuration));
        public float RearWeight => State == BehaviourState.Rearing ? Mathf.SmoothStep(0f, 1f, StateProgress) : 0f;
        public int PouncesStarted { get; private set; }
        public int HitsLanded { get; private set; }
        public int AttacksStarted { get; private set; }
        public void SetTarget(Transform value) => target = value;
        public Vector3 LookPoint
        {
            get
            {
                if (TargetVisible && target) return target.position + Vector3.up * .35f;
                if (State == BehaviourState.Investigating || State == BehaviourState.Rearing) return lastSeen + Vector3.up * .8f;
                float sweep = Mathf.Sin(Time.time * (State == BehaviourState.Searching ? 1.4f : .65f)) * 75f;
                return transform.position + Quaternion.Euler(0f, sweep, 0f) * transform.forward * 6f + Vector3.up * .7f;
            }
        }

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            mobAttack = GetComponent<MobAttackDriver>();
            StartCoroutine(MarkShopAfterStartup());
            if (!target)
            {
                var player = FindAnyObjectByType<StarterGame.CapsuleMovement>();
                if (player) target = player.transform;
            }
            home = previousPosition = transform.position;
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
            Velocity = Vector3.Lerp(Velocity, (transform.position - previousPosition) / Mathf.Max(dt, .001f), 1f - Mathf.Exp(-dt * 12f));
            previousPosition = transform.position;
            Vector3 measured = (target.position - previousTarget) / Mathf.Max(dt, .001f);
            previousTarget = target.position;
            // Do not turn a respawn or test teleport into an enormous interception prediction.
            estimatedVelocity = Vector3.Lerp(estimatedVelocity, Vector3.ClampMagnitude(Vector3.ProjectOnPlane(measured, Vector3.up), 7f), 1f - Mathf.Exp(-dt * 5f));
            if (Time.time >= nextSight)
            {
                nextSight = Time.time + .1f;
                TargetVisible = CanSeeTarget();
                if (TargetVisible) { lastSeen = TargetFeet(); lastSeenAt = Time.time; }
            }
            if (mobAttack && State == BehaviourState.Attacking)
            {
                // Commit the swing in its original direction so the player can dodge it.
                if (mobAttack.TickAttack(target, TargetVisible, stateTime))
                {
                    HitsLanded++;
                    onPounceHit.Invoke(target);
                }
                if (stateTime < mobAttack.AttackDuration) return;
                cooldown = mobAttack.Cooldown;
                SetState(BehaviourState.Tracking);
            }
            if (State == BehaviourState.Leaping) { UpdateLeap(); return; }
            if (State == BehaviourState.Rearing)
            {
                TurnTowards(lastSeen, dt);
                if (stateTime >= rearDuration)
                {
                    if (TargetVisible && Vector3.Distance(transform.position, TargetFeet()) <= attackRange + .8f && TryLeapDestination(out leapEnd)) BeginLeap();
                    else Recover();
                }
                return;
            }
            if (State == BehaviourState.Recovering)
            {
                transform.position = Vector3.Lerp(recoveryStart, recoveryFloor, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateTime / .24f)));
                if (!agent.updatePosition && stateTime >= .24f) { agent.Warp(recoveryFloor); agent.updatePosition = true; }
                if (stateTime >= recoveryDuration) SetState(BehaviourState.Tracking);
                else return;
            }
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;
            agent.speed = TargetVisible ? chaseSpeed : chaseSpeed * .6f;
            if (TargetVisible)
            {
                SetState(BehaviourState.Tracking);
                Vector3 predicted = lastSeen + estimatedVelocity * predictionSeconds;
                GoTo(predicted);
                TurnTowards(agent.desiredVelocity.sqrMagnitude > .04f ? transform.position + agent.desiredVelocity : lastSeen, dt);
                float distance = Vector3.Distance(transform.position, lastSeen);
                if (mobAttack)
                {
                    if (distance <= mobAttack.Range)
                    {
                        agent.isStopped = true;
                        agent.ResetPath();
                        TurnTowards(lastSeen, dt);
                        if (cooldown <= 0f && Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(lastSeen - transform.position, Vector3.up)) < 25f)
                        {
                            mobAttack.BeginAttack();
                            AttacksStarted++;
                            SetState(BehaviourState.Attacking);
                        }
                    }
                    return;
                }
                if (cooldown <= 0f && distance > .75f && distance <= attackRange &&
                    Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(lastSeen - transform.position, Vector3.up)) < 28f &&
                    Mathf.Abs(lastSeen.y - transform.position.y) < .65f && TryLeapDestination(out leapEnd))
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                    leapStart = transform.position;
                    SetState(BehaviourState.Rearing);
                }
            }
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
                    GoTo(lastSeen + new Vector3(offset.x, 0, offset.y), true);
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

        private void BeginSearch()
        {
            SetState(BehaviourState.Searching);
            searchUntil = Time.time + searchSeconds;
            nextSearchPoint = 0f;
        }

        private void SetState(BehaviourState value)
        {
            if (State == value) return;
            State = value;
            stateTime = 0f;
        }

        private Vector3 TargetFeet()
        {
            var controller = target.GetComponent<CharacterController>();
            return controller ? new Vector3(target.position.x, controller.bounds.min.y, target.position.z) : target.position;
        }

        private bool CanSeeTarget()
        {
            Vector3 origin = sensor ? sensor.position : transform.position + Vector3.up;
            Vector3 direction = target.position + Vector3.up * .35f - origin;
            if (direction.magnitude > sightRange || Vector3.Angle(sensor ? sensor.forward : transform.forward, direction) > fieldOfView * .5f) return false;
            return !SceneCast(origin, direction.normalized, direction.magnitude, 0f, true, out _);
        }

        private void GoTo(Vector3 destination, bool immediate = false)
        {
            if (!immediate && Time.time < nextPath) return;
            nextPath = Time.time + repathInterval;
            if (NavMesh.SamplePosition(destination, out var point, 2f, agent.areaMask)) agent.SetDestination(point.position);
        }

        private static void MarkRuntimeShopObstacles()
        {
            // The existing weapon shop is created during Play mode, after the static navigation bake.
            var shop = GameObject.Find("Weapon Shop Wall");
            if (!shop) return;
            foreach (var box in shop.GetComponentsInChildren<BoxCollider>())
            {
                if (box.isTrigger || box.GetComponent<NavMeshObstacle>()) continue;
                var obstacle = box.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = box.center;
                obstacle.size = box.size;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                obstacle.carvingTimeToStationary = .1f;
            }
        }

        private System.Collections.IEnumerator MarkShopAfterStartup()
        {
            yield return null;
            MarkRuntimeShopObstacles();
        }

        private void TurnAlongPath(float dt)
        {
            if (agent.desiredVelocity.sqrMagnitude > .035f) TurnTowards(transform.position + agent.desiredVelocity, dt);
        }

        private void TurnTowards(Vector3 point, float dt)
        {
            Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240f * dt);
        }

        private bool TryLeapDestination(out Vector3 destination)
        {
            Vector3 desired = TargetFeet() + estimatedVelocity * .16f;
            Vector3 direction = Vector3.ProjectOnPlane(desired - transform.position, Vector3.up).normalized;
            desired -= direction * .5f;
            destination = transform.position;
            if (!NavMesh.SamplePosition(desired, out var point, .8f, agent.areaMask) ||
                Mathf.Abs(point.position.y - transform.position.y) > .65f ||
                NavMesh.Raycast(transform.position, point.position, out _, agent.areaMask)) return false;
            Vector3 toEnd = point.position + Vector3.up * .65f - (transform.position + Vector3.up * .65f);
            if (SceneCast(transform.position + Vector3.up * .65f, toEnd.normalized, toEnd.magnitude, .28f, true, out _)) return false;
            destination = point.position;
            return true;
        }

        private void BeginLeap()
        {
            leapStart = transform.position;
            leapEnd = leapStart + Vector3.ClampMagnitude(leapEnd - leapStart, attackRange + .25f);
            agent.updatePosition = false;
            hitThisLeap = false;
            PouncesStarted++;
            AttacksStarted++;
            SetState(BehaviourState.Leaping);
        }

        private void UpdateLeap()
        {
            float t = StateProgress;
            Vector3 next = Vector3.Lerp(leapStart, leapEnd, t) + Vector3.up * (4f * leapHeight * t * (1f - t));
            Vector3 movement = next - transform.position;
            if (movement.sqrMagnitude > .000001f && SceneCast(transform.position + Vector3.up * .58f, movement.normalized, movement.magnitude + .03f, .3f, false, out var collision))
            {
                transform.position += movement.normalized * Mathf.Max(0f, collision.distance - .035f);
                if (collision.collider.transform == target || collision.collider.transform.IsChildOf(target)) RegisterHit();
                Recover();
                return;
            }
            transform.position = next;
            if (!hitThisLeap && t > .2f && PlanarDistance(transform.position, target.position) < .85f &&
                Mathf.Abs(transform.position.y + .5f - target.position.y) < 1.25f && CanSeeTarget()) RegisterHit();
            if (t >= 1f) Recover();
        }

        private void RegisterHit()
        {
            if (hitThisLeap) return;
            hitThisLeap = true;
            HitsLanded++;
            onPounceHit.Invoke(target);
        }

        private void Recover()
        {
            // A collision ends the flight, then settles onto a reachable floor over the recovery pose.
            Vector3 floor = new Vector3(transform.position.x, leapStart.y, transform.position.z);
            recoveryStart = transform.position;
            recoveryFloor = floor;
            if (NavMesh.SamplePosition(floor, out var landing, 1.5f, agent.areaMask))
            {
                recoveryFloor = landing.position;
                agent.updatePosition = false;
                agent.isStopped = true;
            }
            cooldown = attackCooldown;
            SetState(BehaviourState.Recovering);
        }

        private bool SceneCast(Vector3 origin, Vector3 direction, float distance, float radius, bool ignoreTarget, out RaycastHit nearest)
        {
            int count = radius > 0f
                ? Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance, environmentMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(origin, direction, hits, distance, environmentMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            nearest = default;
            for (int i = 0; i < count; i++)
            {
                var candidate = hits[i];
                if (candidate.transform == transform || candidate.transform.IsChildOf(transform)) continue;
                if (ignoreTarget && (candidate.transform == target || candidate.transform.IsChildOf(target))) continue;
                if (candidate.distance < best) { best = candidate.distance; nearest = candidate; }
            }
            return !float.IsPositiveInfinity(best);
        }

        private static float PlanarDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

        private void OnDisable()
        {
            if (agent && agent.enabled && agent.isOnNavMesh) { agent.updatePosition = true; agent.isStopped = true; }
        }

        private void OnGUI()
        {
            if (!showStatus || !initialized) return;
            GUILayout.BeginArea(new Rect(12, Screen.height - 82, 300, 68), GUI.skin.box);
            GUILayout.Label(name + "  |  " + State);
            GUILayout.Label("Pounces: " + PouncesStarted + "   Contacts: " + HitsLanded + (TargetVisible ? "   Target in sight" : "   Scanning"));
            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            if (Application.isPlaying && initialized)
            {
                Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(lastSeen, .2f);
                if (agent && agent.enabled && agent.isOnNavMesh && agent.hasPath)
                { var points = agent.path.corners; for (int i = 1; i < points.Length; i++) Gizmos.DrawLine(points[i - 1], points[i]); }
            }
        }
    }
}
