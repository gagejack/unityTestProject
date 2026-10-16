using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Mobs
{
    /// <summary>Picks a mob's attack, idle, walk and run clips, and checks range and facing when an attack lands.</summary>
    [RequireComponent(typeof(BlockCharacterAnimations))]
    public sealed class MobAttackDriver : MonoBehaviour
    {
        [Tooltip("Distance in meters at which the mob stops chasing and starts an attack.")]
        [SerializeField, Min(.5f)] private float range = 1.7f;
        [Tooltip("Seconds after an attack ends before the next one can start.")]
        [SerializeField, Min(.1f)] private float cooldown = .9f;
        [Tooltip("Point in the attack clip, as a fraction of its length, where the hit is checked.")]
        [SerializeField, Range(.1f, .9f)] private float contactTime = .45f;
        [Tooltip("Damage dealt to the target if it is in range and in front when the hit is checked.")]
        [SerializeField, Min(0f)] private float damage = 15f;
        private BlockCharacterAnimations playback;
        private MobBrain brain;
        private NavMeshAgent agent;
        private readonly List<int> attacks = new List<int>();
        private int idle = -1, walk = -1, run = -1, attackCursor;
        private bool contactChecked;
        public float Range => range;
        public float Cooldown => cooldown;
        public float AttackDuration { get; private set; }
        public bool HasAttack => attacks.Count > 0;

        private void Awake()
        {
            playback = GetComponent<BlockCharacterAnimations>();
            brain = GetComponent<MobBrain>();
            agent = GetComponent<NavMeshAgent>();
            playback.LoopPreview = false;
            var entries = playback.AnimationSet.animations;
            bool soldier = playback.AnimationSet.characterName == "Soldier";
            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].clip) continue;
                string label = entries[i].label.Replace(" - ", " ");
                if (label == "Idle01") idle = i;
                if (label == "Walk01 Forward") walk = i;
                if (label == "Run01 Forward") run = i;
                if (soldier ? label == "AssaultRifle Aim01 Shoot01" : label.Contains("Attack") && !label.Contains("Shield"))
                    attacks.Add(i);
            }
            if (idle < 0) idle = 0;
            if (walk < 0) walk = run >= 0 ? run : idle;
            if (run < 0) run = walk;
            if (!HasAttack)
            {
                Debug.LogError(name + " has no usable attack animation.", this);
                if (brain) brain.enabled = false;
                enabled = false;
            }
        }

        public void BeginAttack()
        {
            int index = attacks[attackCursor++ % attacks.Count];
            playback.SetSpeed(1f);
            playback.Play(index);
            AttackDuration = playback.AnimationSet.animations[index].clip.length + .12f;
            contactChecked = false;
        }

        public bool TickAttack(Transform target, bool visible, float elapsed)
        {
            if (contactChecked || elapsed < AttackDuration * contactTime) return false;
            contactChecked = true;
            if (!target || !visible) return false;
            Vector3 offset = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            if (offset.magnitude > range + .2f || Vector3.Angle(transform.forward, offset) > 65f) return false;
            var health = target.GetComponent<Game.Core.IDamageable>();
            health?.TakeDamage(damage);
            return true;
        }

        private void LateUpdate()
        {
            if (!brain || brain.State == MobBrain.BehaviourState.Attacking) return;
            float speed = agent && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            int index = speed < .1f ? idle : brain.TargetVisible ? run : walk;
            var animator = playback.Animator;
            if (playback.CurrentIndex != index) playback.Play(index);
            else if (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                playback.Play(index, false);
        }
    }
}
