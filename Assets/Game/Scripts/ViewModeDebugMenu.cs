using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterGame
{
    public enum PlayerViewMode { FixedCamera, ThirdPerson, FreeRoam }

    [DefaultExecutionOrder(-120)]
    public sealed class ViewModeDebugMenu : MonoBehaviour
    {
        [SerializeField] private CapsuleMovement player;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private PlayerViewMode startingMode = PlayerViewMode.ThirdPerson;
        [SerializeField] private bool showMenu = true;
        private CombatFeedback combatFeedback;
        private PlayerStamina playerStamina;
        private Characters.DebugMobSpawner mobSpawner;
        private WeaponCombatController weapons;
        private static readonly string[] Tabs = { "Weapons", "Mobs", "Player", "Camera", "Audio", "Controls" };
        private static readonly string[] Categories = { "All Weapons", "Pistols & Duals", "SMGs", "Assault Rifles", "Shotguns", "Rifles & Snipers", "Machine Guns", "Launchers" };
        private static readonly string[] WeaponBrowseTabs = { "Weapon Type", "Weapon Pack" };
        private sealed class PackFilter
        {
            public string creator, pack;
            public string Label => string.IsNullOrEmpty(pack) ? "All packs" : pack;
        }
        private readonly List<PackFilter> packFilters = new List<PackFilter>();
        private int weaponBrowseMode, selectedPack;
        private int[] weaponOrder = Array.Empty<int>();
        private Vector2 packScroll;
        private static readonly string[] AudioTabs = { "Hitmarker", "Punch" };
        private static readonly string[] ControlTabs = { "Movement & Dash", "Combat & Pickups", "Camera" };
        private readonly Vector2[] tabScroll = new Vector2[6];
        private Vector2 categoryScroll, weaponScroll;
        private int selectedTab, selectedCategory, selectedWeapon, audioTab, controlsTab;
        private string search = "";
        private string weaponStatus = "Choose a weapon, then Equip or Spawn Pickup.";
        private Rect MenuRect => new Rect(12, 12, Mathf.Max(1f, Mathf.Min(650f, Screen.width - 24f)),
            showMenu ? Mathf.Max(1f, Mathf.Min(560f, Screen.height - 24f)) : 32f);
        public bool IsOpen => isActiveAndEnabled && showMenu;
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
            mobSpawner = GetComponent<Characters.DebugMobSpawner>();
            BuildPackFilters();
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
            if (!showMenu && Keyboard.current.kKey.wasPressedThisFrame) ToggleFreeRoamControl();
            if (Keyboard.current.f1Key.wasPressedThisFrame) SetOpen(!showMenu);
        }

        public bool ContainsPointer(Vector2 screenPosition)
        {
            return enabled && MenuRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        private void OnGUI()
        {
            if (!enabled) return;
            Color previousColor = GUI.color;
            GUI.color = new Color(.075f, .095f, .12f, .6f);
            GUI.DrawTexture(MenuRect, Texture2D.whiteTexture);
            GUI.color = previousColor;
            var panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = null;
            GUILayout.BeginArea(MenuRect, panelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("DEBUG MENU  /  " + (showMenu ? Tabs[selectedTab] : "F1 to open"));
            if (GUILayout.Button(showMenu ? "Close (F1)" : "Open (F1)", GUILayout.Width(96))) SetOpen(!showMenu);
            GUILayout.EndHorizontal();
            if (showMenu)
            {
                int nextTab = GUILayout.Toolbar(selectedTab, Tabs, GUILayout.Height(30));
                if (nextTab != selectedTab) { selectedTab = nextTab; GUI.FocusControl(null); }
                GUILayout.Space(6);
                if (selectedTab == 0) DrawWeapons();
                else
                {
                    tabScroll[selectedTab] = GUILayout.BeginScrollView(tabScroll[selectedTab]);
                    switch (selectedTab)
                    {
                        case 1: if (mobSpawner) mobSpawner.DrawControls(); else GUILayout.Label("No mobs configured."); break;
                        case 2: DrawPlayer(); break;
                        case 3: DrawCamera(); break;
                        case 4:
                            DrawVolumeControls();
                            GUILayout.Space(8);
                            audioTab = GUILayout.Toolbar(audioTab, AudioTabs, GUILayout.Height(28));
                            GUILayout.Space(8);
                            if (audioTab == 0) DrawHitmarkerControls(); else DrawPunchControls();
                            break;
                        case 5: DrawControls(); break;
                    }
                    GUILayout.EndScrollView();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label("F1: close menu  |  Click outside to resume  |  Esc: release cursor");
            }
            GUILayout.EndArea();
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

        private string WeaponFolderLabel()
        {
            if (weaponBrowseMode == 0) return "Weapon Type / " + Categories[selectedCategory];
            var pack = packFilters[Mathf.Clamp(selectedPack, 0, packFilters.Count - 1)];
            return "Weapon Pack / " + (string.IsNullOrEmpty(pack.creator) ? "All Packs" : pack.creator + " / " + pack.Label);
        }

        private void DrawPackFolders()
        {
            var buttonStyle = new GUIStyle(GUI.skin.button) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
            string creator = null;
            for (int i = 0; i < packFilters.Count; i++)
            {
                var pack = packFilters[i];
                if (i > 0 && creator != pack.creator)
                {
                    GUILayout.Space(6);
                    GUILayout.Label(pack.creator, WrapStyle());
                    creator = pack.creator;
                }
                bool selected = selectedPack == i;
                if (GUILayout.Toggle(selected, i == 0 ? "All Creators / All Packs" : pack.Label, buttonStyle, GUILayout.MinHeight(32)) && !selected)
                { selectedPack = i; weaponScroll = Vector2.zero; GUI.FocusControl(null); }
            }
        }

        private void DrawWeapons()
        {
            if (!weapons || weapons.WeaponOffers.Count == 0) { GUILayout.Label("No weapons configured."); return; }
            int nextBrowseMode = GUILayout.Toolbar(weaponBrowseMode, WeaponBrowseTabs, GUILayout.Height(28));
            if (nextBrowseMode != weaponBrowseMode) { weaponBrowseMode = nextBrowseMode; weaponScroll = Vector2.zero; GUI.FocusControl(null); }
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(48));
            GUI.SetNextControlName("WeaponSearch");
            search = GUILayout.TextField(search, 80);
            if (GUILayout.Button("Clear", GUILayout.Width(48))) { search = ""; GUI.FocusControl(null); }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            var catalogue = weapons.WeaponOffers;
            int visible = 0, first = -1;
            foreach (int i in weaponOrder)
                if (MatchesFilter(catalogue[i])) { visible++; if (first < 0) first = i; }
            if (selectedWeapon < 0 || selectedWeapon >= catalogue.Count || !MatchesFilter(catalogue[selectedWeapon])) selectedWeapon = first;
            GUILayout.Label("Weapons / " + WeaponFolderLabel() + "  (" + visible + ")", WrapStyle());
            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            if (weaponBrowseMode == 0)
            {
                categoryScroll = GUILayout.BeginScrollView(categoryScroll, GUILayout.Width(Mathf.Min(158f, MenuRect.width * .3f)));
                int nextCategory = GUILayout.SelectionGrid(selectedCategory, Categories, 1, GUILayout.MinHeight(272));
                if (nextCategory != selectedCategory) { selectedCategory = nextCategory; weaponScroll = Vector2.zero; GUI.FocusControl(null); }
                GUILayout.EndScrollView();
            }
            else
            {
                packScroll = GUILayout.BeginScrollView(packScroll, GUILayout.Width(Mathf.Min(215f, MenuRect.width * .37f)));
                DrawPackFolders();
                GUILayout.EndScrollView();
            }
            weaponScroll = GUILayout.BeginScrollView(weaponScroll, GUI.skin.box);
            foreach (int i in weaponOrder)
            {
                if (!MatchesFilter(catalogue[i])) continue;
                bool selected = selectedWeapon == i;
                bool pressed = GUILayout.Toggle(selected, catalogue[i].displayName, GUI.skin.button, GUILayout.Height(30));
                if (pressed && !selected) { selectedWeapon = i; GUI.FocusControl(null); }
            }
            if (visible == 0) GUILayout.Label("No weapons match. Try another folder or clear your search.", WrapStyle());
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
            // Recheck after a category click in this GUI event.
            bool valid = selectedWeapon >= 0 && selectedWeapon < catalogue.Count && MatchesFilter(catalogue[selectedWeapon]);
            if (valid)
            {
                var weapon = catalogue[selectedWeapon];
                GUILayout.BeginHorizontal(GUI.skin.box);
                if (weapon.icon)
                {
                    Rect icon = GUILayoutUtility.GetRect(100f, 54f, GUILayout.Width(100));
                    GUI.DrawTexture(icon, weapon.icon, ScaleMode.ScaleToFit, true);
                }
                GUILayout.BeginVertical();
                GUILayout.Label(weapon.displayName, WrapStyle());
                GUILayout.Label(CreatorFor(weapon) + " / " + PackFor(weapon), WrapStyle());
                GUILayout.Label(weapon.triggerMode + "  |  " + weapon.magazine + " / " + weapon.reserve + " ammo", WrapStyle());
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && valid && catalogue[selectedWeapon].model;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Equip", GUILayout.Height(34)))
                weaponStatus = weapons.EquipFromDebug(selectedWeapon) ? "Equipped " + catalogue[selectedWeapon].displayName + "." : "Could not equip this weapon.";
            if (GUILayout.Button("Spawn Pickup", GUILayout.Height(34))) weapons.SpawnFromDebug(selectedWeapon, out _, out weaponStatus);
            GUILayout.EndHorizontal();
            GUI.enabled = wasEnabled;
            GUILayout.Label(weaponStatus, WrapStyle());
        }

        private static GUIStyle WrapStyle() => new GUIStyle(GUI.skin.label) { wordWrap = true };

        private void DrawPlayer()
        {
            GUILayout.Label("PLAYER / STAMINA");
            if (playerStamina && GUILayout.Button(playerStamina.IsInfinite ? "Infinite Stamina: ON" : "Infinite Stamina: OFF", GUILayout.Height(32)))
                playerStamina.SetInfinite(!playerStamina.IsInfinite);
        }

        private void DrawCamera()
        {
            string mode = CurrentMode == PlayerViewMode.FreeRoam ? (followCamera.IsFlying ? "Free roam: flying" : "Free roam: character") : CurrentMode.ToString();
            GUILayout.Label("CAMERA / " + mode);
            if (GUILayout.Button("Fixed camera / original movement", GUILayout.Height(32))) SetMode(PlayerViewMode.FixedCamera);
            if (GUILayout.Button("Third person / shooter movement", GUILayout.Height(32))) SetMode(PlayerViewMode.ThirdPerson);
            if (GUILayout.Button("Free roam / place camera", GUILayout.Height(32))) SetMode(PlayerViewMode.FreeRoam);
            if (CurrentMode == PlayerViewMode.FreeRoam && GUILayout.Button(followCamera.IsFlying ? "Park camera and control character" : "Fly camera", GUILayout.Height(32)))
                followCamera.ToggleFreeRoamControl();
            GUILayout.Label("Close the menu to use camera controls. K switches between flying and controlling the character.", WrapStyle());
        }

        private void DrawControls()
        {
            controlsTab = GUILayout.Toolbar(controlsTab, ControlTabs, GUILayout.Height(28));
            GUILayout.Space(8);
            string text = controlsTab == 0
                ? "WASD: move\nShift + W or double-tap W: sprint\nSpace: jump   Ctrl: crouch\nCaps Lock: dash\nIn air: WASD + Caps Lock to air dash\nHold Space / Ctrl for up / down dash\nAlt + Caps Lock: dive (S for backward dive)"
                : controlsTab == 1
                ? "LMB: fire / unarmed attack\nRMB: aim   R: reload\nQ: drop equipped weapon\nF: pick up while unarmed / interact with mystery box\nWeapons tab: equip freely or spawn a pickup"
                : "Third person: mouse aims camera and character\nFixed camera: WASD moves relative to screen\nFree roam: WASD + mouse to fly\nSpace / Ctrl: up / down   Shift: boost\nK: fly / park camera and control character\nF1: open / close debug menu\nEsc: release cursor; click outside menu to resume";
            GUILayout.Label(text, WrapStyle());
        }

        private void OnDisable() { PlayerPrefs.Save(); }

        private void DrawVolumeControls()
        {
            GUILayout.Label("VOLUME");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Master Volume", GUILayout.Width(100));
            float master = GUILayout.HorizontalSlider(AudioVolumeSettings.MasterVolume, 0f, 1f);
            GUILayout.Label(Mathf.RoundToInt(master * 100f) + "%", GUILayout.Width(38));
            if (!Mathf.Approximately(master, AudioVolumeSettings.MasterVolume)) AudioVolumeSettings.SetMasterVolume(master);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Gun Volume", GUILayout.Width(100));
            float gun = GUILayout.HorizontalSlider(AudioVolumeSettings.GunVolume, 0f, 1f);
            GUILayout.Label(Mathf.RoundToInt(gun * 100f) + "%", GUILayout.Width(38));
            if (!Mathf.Approximately(gun, AudioVolumeSettings.GunVolume)) AudioVolumeSettings.SetGunVolume(gun);
            GUILayout.EndHorizontal();
            GUILayout.Label("Master adjusts all audio. Gun adjusts firing, reloads, handling, impacts and explosions.", WrapStyle());
        }

        private void DrawPunchControls()
        {
            if (!combatFeedback || combatFeedback.PunchSoundCount == 0) return;
            GUILayout.Label("PUNCH SOUND");
            bool enabledSound = GUILayout.Toggle(combatFeedback.PunchEnabled, "Play sound on unarmed contact");
            if (enabledSound != combatFeedback.PunchEnabled) combatFeedback.SetPunchEnabled(enabledSound);
            if (GUILayout.Button(combatFeedback.PunchRandomized ? "Randomizer: ON (each hit)" : "Randomizer: OFF"))
                combatFeedback.SetPunchRandomized(!combatFeedback.PunchRandomized);
            var clip = combatFeedback.SelectedPunchClip;
            GUILayout.Label((combatFeedback.PunchRandomized ? "Manual preview: Punch " : "Punch ") + (combatFeedback.SelectedPunch + 1) + " / " + combatFeedback.PunchSoundCount
                + (clip ? "   (" + clip.length.ToString("0.00") + " s)" : ""));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Previous")) combatFeedback.SelectPunch(combatFeedback.SelectedPunch - 1);
            if (GUILayout.Button("Preview")) combatFeedback.PreviewPunch();
            if (GUILayout.Button("Next >")) combatFeedback.SelectPunch(combatFeedback.SelectedPunch + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Volume", GUILayout.Width(50));
            float volume = GUILayout.HorizontalSlider(combatFeedback.PunchVolume, 0f, 1f);
            GUILayout.Label(Mathf.RoundToInt(volume * 100f) + "%", GUILayout.Width(38));
            if (!Mathf.Approximately(volume, combatFeedback.PunchVolume)) combatFeedback.SetPunchVolume(volume);
            GUILayout.EndHorizontal();
        }

        private void DrawHitmarkerControls()
        {
            if (!combatFeedback || combatFeedback.HitmarkerSoundCount == 0) return;
            GUILayout.Label("HITMARKER SOUND");
            bool enabledSound = GUILayout.Toggle(combatFeedback.HitmarkerEnabled, "Play sound on confirmed hits");
            if (enabledSound != combatFeedback.HitmarkerEnabled) combatFeedback.SetHitmarkerEnabled(enabledSound);
            var clip = combatFeedback.SelectedHitmarkerClip;
            GUILayout.Label("Hitmarker " + (combatFeedback.SelectedHitmarker + 1) + " / " + combatFeedback.HitmarkerSoundCount
                + (clip ? "   (" + clip.length.ToString("0.00") + " s)" : ""));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Previous")) combatFeedback.SelectHitmarker(combatFeedback.SelectedHitmarker - 1);
            if (GUILayout.Button("Preview")) combatFeedback.PreviewHitmarker();
            if (GUILayout.Button("Next >")) combatFeedback.SelectHitmarker(combatFeedback.SelectedHitmarker + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Volume", GUILayout.Width(50));
            float volume = GUILayout.HorizontalSlider(combatFeedback.HitmarkerVolume, 0f, 1f);
            GUILayout.Label(Mathf.RoundToInt(volume * 100f) + "%", GUILayout.Width(38));
            if (!Mathf.Approximately(volume, combatFeedback.HitmarkerVolume)) combatFeedback.SetHitmarkerVolume(volume);
            GUILayout.EndHorizontal();
        }
    }
}
