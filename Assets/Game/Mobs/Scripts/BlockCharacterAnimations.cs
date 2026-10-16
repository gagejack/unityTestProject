using UnityEngine;

namespace Game.Mobs
{
    /// <summary>Plays the character's retargeted clips without moving its display position.</summary>
    public sealed class BlockCharacterAnimations : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private BlockCharacterAnimationSet animationSet;
        [SerializeField, Min(0.1f)] private float displayHeight = 2f;
        [SerializeField] private bool loopPreview = true;
        [SerializeField] private GameObject rightWeapon;
        [SerializeField] private GameObject leftWeapon;
        [SerializeField] private GameObject twoHandedWeapon;
        [SerializeField] private GameObject polearm;
        [SerializeField] private GameObject shield;
        [Header("Soldier equipment")]
        [SerializeField] private GameObject assaultRifle;
        [SerializeField] private GameObject handgun;
        [SerializeField] private GameObject leftHandgun;
        [SerializeField] private GameObject rifle;
        [SerializeField] private GameObject bazooka;
        [SerializeField] private GameObject grenade;
        private int currentIndex;

        public Animator Animator => animator;
        public BlockCharacterAnimationSet AnimationSet => animationSet;
        public float DisplayHeight => displayHeight;
        public int CurrentIndex => currentIndex;
        public bool LoopPreview { get => loopPreview; set => loopPreview = value; }
        public int Count => animationSet ? animationSet.animations.Length : 0;
        public string CurrentLabel => Count > 0 ? animationSet.animations[currentIndex].label : "No animations";

        private void OnEnable()
        {
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman ||
                !animator.runtimeAnimatorController || Count == 0)
            {
                Debug.LogError("Block character needs a valid Humanoid Animator and its animation set.", this);
                enabled = false;
                return;
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            currentIndex = Mathf.Clamp(currentIndex, 0, Count - 1);
            ShowEquipment(animationSet.animations[currentIndex].clip.name);
        }

        private void Update()
        {
            if (!loopPreview || Count == 0 || animator.IsInTransition(0)) return;
            if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                animator.Play(animationSet.animations[currentIndex].stateName, 0, 0f);
        }

        public void Play(int index, bool blend = true)
        {
            if (!enabled || Count == 0 || index < 0 || index >= Count) return;
            currentIndex = index;
            string state = animationSet.animations[index].stateName;
            if (blend && animator.isInitialized) animator.CrossFadeInFixedTime(state, 0.12f, 0, 0f);
            else animator.Play(state, 0, 0f);
            ShowEquipment(animationSet.animations[index].clip.name);
        }

        public void Next() => Play((currentIndex + 1) % Mathf.Max(1, Count));
        public void Previous() => Play((currentIndex - 1 + Count) % Mathf.Max(1, Count));
        public void SetSpeed(float value) { if (animator) animator.speed = Mathf.Clamp(value, 0f, 2f); }

        private void ShowEquipment(string clipName)
        {
            if (assaultRifle)
            {
                bool throwing = clipName.Contains("Grenade");
                bool dual = clipName.Contains("DualGun");
                bool pistol = clipName.Contains("Gun");
                bool launcher = clipName.Contains("Bazooka");
                bool longRifle = clipName.Contains("Rifle") && !clipName.Contains("AssaultRifle");
                bool armed = clipName.Contains("Aim") || clipName.Contains("Reload");
                SetVisible(assaultRifle, armed && !pistol && !launcher && !longRifle && !throwing);
                SetVisible(handgun, armed && pistol);
                SetVisible(leftHandgun, armed && dual);
                SetVisible(rifle, armed && longRifle);
                SetVisible(bazooka, armed && launcher);
                SetVisible(grenade, throwing);
                return;
            }
            bool twoHands = clipName.Contains("2H");
            bool spear = clipName.Contains("Polearm");
            bool leftAttack = clipName.Contains("1H") && clipName.EndsWith("_L");
            bool unarmed = clipName.Contains("Unarmed");
            SetVisible(rightWeapon, !twoHands && !spear && !leftAttack && !unarmed);
            SetVisible(leftWeapon, leftAttack);
            SetVisible(twoHandedWeapon, twoHands);
            SetVisible(polearm, spear);
            SetVisible(shield, !twoHands && !spear && !leftAttack && !unarmed);
        }

        private static void SetVisible(GameObject item, bool visible) { if (item) item.SetActive(visible); }
    }
}
