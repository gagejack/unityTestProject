using UnityEngine;

namespace StarterGame
{
    /// <summary>A paid weapon roll: open, reveal one changing model, settle, then collect.</summary>
    public sealed class MysteryWeaponBox : MonoBehaviour
    {
        public enum BoxState { Closed, Opening, Cycling, Ready, Closing }
        [SerializeField] private WeaponSettings[] weapons;
        [SerializeField] private Transform lid;
        [SerializeField] private Transform displayRoot;
        [SerializeField] private TextMesh sign;
        [SerializeField, Min(0)] private int price = 500;
        [SerializeField, Min(0.5f)] private float interactionDistance = 2.6f;
        [SerializeField, Min(0.1f)] private float openingDuration = 0.7f;
        [SerializeField, Min(1f)] private float rollDuration = 5.2f;
        [SerializeField, Min(0.1f)] private float closingDuration = 0.6f;
        [SerializeField] private Vector3 openLidEuler = new Vector3(-110f, 0f, 0f);
        [SerializeField] private float startHeight = 0.2f;
        [SerializeField] private float revealHeight = 1.65f;
        private GameObject[] previews;
        private int[] bag;
        private int bagPosition;
        private Quaternion closedLidRotation;
        private Quaternion previewRotation;
        private float elapsed, nextChange;
        private WeaponCombatController buyer;
        public BoxState State { get; private set; }
        public WeaponSettings Reward => State == BoxState.Ready && selected >= 0 ? weapons[selected] : null;
        public int Price => price;
        public int VisibleModelCount
        {
            get { int count = 0; if (previews != null) foreach (var preview in previews) if (preview && preview.activeSelf) count++; return count; }
        }
        private int selected = -1;
        private void Awake()
        {
            closedLidRotation = lid ? lid.localRotation : Quaternion.identity;
            State = BoxState.Closed;
            UpdateSign();
        }

        public bool CanReach(WeaponCombatController player)
        {
            if (!isActiveAndEnabled || !player || Vector3.Distance(player.transform.position, transform.position) > interactionDistance) return false;
            Vector3 origin = player.transform.position + Vector3.up * 0.7f;
            Vector3 path = transform.position + Vector3.up * 0.4f - origin;
            return !CombatPhysics.Cast(origin, path.normalized, path.magnitude, player.transform, out var hit)
                || hit.collider.transform.IsChildOf(transform);
        }

        public string PromptFor(WeaponCombatController player)
        {
            if (State == BoxState.Closed) return (player.Points < price ? "NOT ENOUGH POINTS  •  " : "F  ") + "MYSTERY BOX  •  $" + price.ToString("N0");
            if (buyer != player) return "MYSTERY BOX IN USE";
            if (State == BoxState.Ready) return "F  TO PICK UP  " + Reward.displayName;
            return State == BoxState.Closing ? "MYSTERY BOX CLOSING" : "CHOOSING YOUR WEAPON…";
        }

        public bool TryInteract(WeaponCombatController player)
        {
            if (!CanReach(player)) return false;
            if (State == BoxState.Ready)
            {
                if (player != buyer || !player.ReceiveMysteryWeapon(Reward)) return false;
                HideSelected();
                ChangeState(BoxState.Closing);
                return true;
            }
            if (State != BoxState.Closed || !ValidWeapons() || !lid || !displayRoot) return false;
            if (!player.TrySpendPoints(price)) return false;
            buyer = player;
            CreatePreviews();
            Shuffle();
            bagPosition = 0;
            ShowNext();
            displayRoot.localPosition = Vector3.up * startHeight;
            ChangeState(BoxState.Opening);
            return true;
        }

        private bool ValidWeapons()
        {
            if (weapons == null || weapons.Length == 0) return false;
            foreach (var weapon in weapons) if (!weapon || !weapon.model) return false;
            return true;
        }

        private void CreatePreviews()
        {
            if (previews != null) return;
            previews = new GameObject[weapons.Length];
            bag = new int[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                bag[i] = i;
                var preview = Instantiate(weapons[i].model, displayRoot, false);
                preview.name = "Mystery preview " + weapons[i].displayName;
                foreach (var collider in preview.GetComponentsInChildren<Collider>()) collider.enabled = false;
                var bounds = new Bounds();
                bool first = true;
                foreach (var renderer in preview.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                }
                Vector3 center = preview.transform.InverseTransformPoint(bounds.center);
                float scale = 1.5f / Mathf.Max(0.1f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
                preview.transform.localScale *= scale;
                preview.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                preview.transform.localPosition = -(preview.transform.localRotation * Vector3.Scale(center, preview.transform.localScale));
                preview.SetActive(false);
                previews[i] = preview;
            }
        }

        private void Shuffle()
        {
            for (int i = bag.Length - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                int value = bag[i]; bag[i] = bag[other]; bag[other] = value;
            }
        }

        private void HideSelected() { if (selected >= 0 && previews != null) previews[selected].SetActive(false); }
        private void ShowNext()
        {
            int previous = selected;
            HideSelected();
            if (bagPosition >= bag.Length)
            {
                Shuffle(); bagPosition = 0;
                if (bag.Length > 1 && bag[0] == previous) { int swap = bag[0]; bag[0] = bag[1]; bag[1] = swap; }
            }
            selected = bag[bagPosition++];
            previews[selected].SetActive(true);
            previewRotation = Quaternion.Euler(Random.Range(-7f, 7f), Random.Range(-16f, 16f), Random.Range(-5f, 5f));
        }

        private void Update() => Tick(Time.deltaTime);
        internal void Tick(float dt)
        {
            elapsed += Mathf.Max(0f, dt);
            if (State == BoxState.Opening)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / openingDuration);
                lid.localRotation = closedLidRotation * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(openLidEuler), progress);
                if (elapsed >= openingDuration) { nextChange = 0f; ChangeState(BoxState.Cycling); }
            }
            else if (State == BoxState.Cycling)
            {
                float progress = Mathf.Clamp01(elapsed / rollDuration);
                displayRoot.localPosition = Vector3.up * Mathf.Lerp(startHeight, revealHeight, Mathf.SmoothStep(0f, 1f, progress));
                displayRoot.localRotation = Quaternion.Slerp(displayRoot.localRotation, previewRotation, 1f - Mathf.Exp(-dt * 12f));
                if (elapsed >= nextChange && progress < 1f)
                {
                    ShowNext();
                    float fastInterval = Mathf.Min(0.025f, rollDuration / (weapons.Length * 3f));
                    nextChange = elapsed + Mathf.Lerp(fastInterval, 0.7f, progress * progress * progress * progress);
                }
                if (progress >= 1f) ChangeState(BoxState.Ready);
            }
            else if (State == BoxState.Ready)
            {
                displayRoot.localPosition = Vector3.up * (revealHeight + Mathf.Sin(elapsed * 2f) * 0.035f);
                displayRoot.localRotation = Quaternion.Slerp(displayRoot.localRotation, Quaternion.identity, 1f - Mathf.Exp(-dt * 4f));
            }
            else if (State == BoxState.Closing)
            {
                lid.localRotation = closedLidRotation * Quaternion.Slerp(Quaternion.Euler(openLidEuler), Quaternion.identity,
                    Mathf.SmoothStep(0f, 1f, elapsed / closingDuration));
                if (elapsed >= closingDuration) { buyer = null; ChangeState(BoxState.Closed); }
            }
        }

        private void ChangeState(BoxState next) { State = next; elapsed = 0f; UpdateSign(); }
        private void UpdateSign()
        {
            if (!sign) return;
            sign.text = State == BoxState.Ready ? "PRESS F TO TAKE\n" + Reward.displayName
                : State == BoxState.Closed ? "MYSTERY BOX\nPress F to Buy\n$" + price.ToString("N0")
                : State == BoxState.Closing ? "MYSTERY BOX\nCLOSING" : "MYSTERY BOX\nROLLING…";
        }
    }
}
