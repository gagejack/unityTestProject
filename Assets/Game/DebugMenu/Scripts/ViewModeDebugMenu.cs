using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterGame
{
    public enum PlayerViewMode { FixedCamera, ThirdPerson, FreeRoam }

    [DefaultExecutionOrder(-120)]
    public sealed partial class ViewModeDebugMenu : MonoBehaviour, IControlBlocker
    {
        [SerializeField, Tooltip("Player whose equipment and movement this menu controls.")] private CapsuleMovement player;
        [SerializeField, Tooltip("Camera that releases and captures the cursor when the menu opens and closes.")] private FollowCamera followCamera;
        [SerializeField, Tooltip("Camera mode selected at the start of Play mode.")] private PlayerViewMode startingMode = PlayerViewMode.ThirdPerson;
        [SerializeField, Tooltip("Open the deprecated F2 debug menu at the start of Play mode. Off by default: Play mode starts with every menu closed.")] private bool showMenu;
        private CombatFeedback combatFeedback;
        private PlayerStamina playerStamina;
        private Game.Mobs.DebugMobSpawner mobSpawner;
        private WeaponCombatController weapons;
        private static readonly string[] Tabs = { "Weapons", "Mobs", "Player", "Camera", "Audio", "Controls" };
        private static readonly string[] Categories = { "All Weapons", "Pistols & Duals", "SMGs", "Assault Rifles", "Shotguns", "Rifles & Snipers", "Machine Guns", "Launchers" };
        private sealed class PackFilter
        {
            public string creator, pack;
            public string Label => string.IsNullOrEmpty(pack) ? "All packs" : pack;
        }
        private readonly List<PackFilter> packFilters = new List<PackFilter>();
        private int weaponBrowseMode, selectedPack;
        private int[] weaponOrder = Array.Empty<int>();
        private int selectedCategory, selectedWeapon = -1;
        private string search = "";
        private string weaponStatus = "Choose a weapon, then Equip or Spawn Pickup.";
        public bool IsOpen => isActiveAndEnabled && (showMenu || pixelOpen);
        public PlayerViewMode CurrentMode { get; private set; }

        private void Start()
        {
            if (player == null || followCamera == null)
            {
                Debug.LogError("View Mode Debug Menu needs Player and Follow Camera assigned.", this);
                enabled = false;
                return;
            }
            combatFeedback = player.GetComponent<CombatFeedback>();
            playerStamina = player.GetComponent<PlayerStamina>();
            weapons = player.GetComponent<WeaponCombatController>();
            mobSpawner = GetComponent<Game.Mobs.DebugMobSpawner>();
            BuildPackFilters();
            InitializeToolkit();
            SetMode(startingMode);
            if (showMenu) followCamera.SetMouseCaptured(false);
        }

        public void SetMode(PlayerViewMode mode)
        {
            CurrentMode = mode;
            player.MovementCamera = followCamera.transform;
            player.FaceCameraDirection = mode == PlayerViewMode.ThirdPerson;
            if (mode == PlayerViewMode.FreeRoam) followCamera.BeginFreeRoam();
            else followCamera.SetThirdPerson(player.FaceCameraDirection);
        }

        public void SetOpen(bool value)
        {
            showMenu = value;
            pixelOpen = false;
            SyncMenuVisibility();
            if (followCamera) followCamera.SetMouseCaptured(!value);
        }

        public void ToggleFreeRoamControl()
        {
            if (CurrentMode != PlayerViewMode.FreeRoam) SetMode(PlayerViewMode.FreeRoam);
            else followCamera.ToggleFreeRoamControl();
        }

        private void Update()
        {
            if (!Application.isFocused || Keyboard.current == null) return;
            if (!IsOpen && Keyboard.current.kKey.wasPressedThisFrame) ToggleFreeRoamControl();
            if (Keyboard.current.f1Key.wasPressedThisFrame && pixelRoot != null) SetPixelOpen(!pixelOpen);
            if (Keyboard.current.f2Key.wasPressedThisFrame) SetOpen(!showMenu); // Deprecated menu.
        }

        public bool ContainsPointer(Vector2 screenPosition)
        {
            return ToolkitContainsPointer(screenPosition) || PixelContainsPointer(screenPosition);
        }

        private void BuildPackFilters()
        {
            packFilters.Clear();
            packFilters.Add(new PackFilter());
            if (!weapons) return;
            weaponOrder = Enumerable.Range(0, weapons.WeaponOffers.Count)
                .OrderBy(i => weapons.WeaponOffers[i] ? weapons.WeaponOffers[i].displayName : "", StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var creator in weapons.WeaponOffers.Where(w => w).GroupBy(w => CreatorFor(w))
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (creator.Select(PackFor).Distinct().Count() > 1)
                    packFilters.Add(new PackFilter { creator = creator.Key });
                foreach (string pack in creator.Select(PackFor).Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                    packFilters.Add(new PackFilter { creator = creator.Key, pack = pack });
            }
            selectedPack = Mathf.Clamp(selectedPack, 0, packFilters.Count - 1);
        }

        private static string CreatorFor(WeaponSettings weapon) => string.IsNullOrWhiteSpace(weapon.creator) ? "Project" : weapon.creator;
        private static string PackFor(WeaponSettings weapon) => string.IsNullOrWhiteSpace(weapon.sourcePack) ? "Project weapons" : weapon.sourcePack;

        private bool MatchesFilter(WeaponSettings weapon)
        {
            if (!weapon) return false;
            bool groupMatches;
            if (weaponBrowseMode == 0) groupMatches = selectedCategory == 0 || (int)weapon.category == selectedCategory - 1;
            else
            {
                var pack = packFilters[Mathf.Clamp(selectedPack, 0, packFilters.Count - 1)];
                groupMatches = (string.IsNullOrEmpty(pack.creator) || CreatorFor(weapon) == pack.creator)
                    && (string.IsNullOrEmpty(pack.pack) || PackFor(weapon) == pack.pack);
            }
            return groupMatches && (string.IsNullOrWhiteSpace(search) || weapon.displayName.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public bool BlocksPlayerControl => IsOpen;
        private void OnEnable() { PlayerControl.Register(this); SyncMenuVisibility(); }
        private void OnDisable() { PlayerControl.Unregister(this); SyncMenuVisibility(); PlayerPrefs.Save(); }

    }
}
