using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarterGame
{
    public sealed partial class ViewModeDebugMenu
    {
        [SerializeField, Tooltip("UI document displaying the F1 debug menu. Its source asset is Game/UI/GameUI.uxml.")]
        private UIDocument menuDocument;

        private VisualElement toolkitRoot, toolkitShell;
        private Button[] navigation;
        private readonly System.Collections.Generic.Dictionary<int, Button> weaponRows = new();
        private static readonly string[] TabColors = { "", "coral", "green", "purple", "amber", "" };
        private static readonly string[] TabBadges = { "LOADOUT LAB", "ENCOUNTER LAB", "PLAYER SETTINGS", "FIND YOUR ANGLE", "SOUND CHECK", "QUICK REFERENCE" };

        private void InitializeToolkit()
        {
            if (!menuDocument || menuDocument.rootVisualElement.Q("debug-shell") == null)
            {
                Debug.LogError("Debug menu needs its UI document. Run Tools > Project > Set Up Debug Menu UI.", this);
                enabled = false;
                return;
            }
            toolkitRoot = menuDocument.rootVisualElement;
            toolkitShell = toolkitRoot.Q("debug-shell");
            toolkitRoot.pickingMode = PickingMode.Ignore;
            toolkitRoot.Q("debug-overlay").pickingMode = PickingMode.Ignore;
            toolkitShell.generateVisualContent += DrawPaperGrid;
            toolkitShell.RegisterCallback<GeometryChangedEvent>(_ => toolkitShell.MarkDirtyRepaint());
            toolkitRoot.Query<Button>(className: "bevel").ForEach(AddButtonShine);
            toolkitRoot.Q<Button>("resume").clicked += () => SetOpen(false);
            navigation = new Button[Tabs.Length];
            for (int i = 0; i < Tabs.Length; i++)
            {
                int tab = i;
                var button = Bevel(Tabs[i].ToUpperInvariant(), () => SelectToolkitTab(tab), TabColors[i]);
                button.name = "tab-" + Tabs[i].ToLowerInvariant();
                button.AddToClassList("nav-button");
                navigation[i] = button;
                toolkitRoot.Q("navigation").Add(button);
            }
            toolkitRoot.Q<TextField>("weapon-search").RegisterValueChangedCallback(evt =>
            {
                search = evt.newValue;
                RefreshWeaponList();
            });
            toolkitRoot.Q<Button>("clear-search").clicked += () => toolkitRoot.Q<TextField>("weapon-search").value = "";
            toolkitRoot.Q<Button>("browse-type").clicked += () => SetBrowseMode(0);
            toolkitRoot.Q<Button>("browse-pack").clicked += () => SetBrowseMode(1);
            toolkitRoot.Q<Button>("equip-weapon").clicked += () =>
            {
                if (!ValidWeaponSelection()) return;
                weaponStatus = weapons.EquipFromDebug(selectedWeapon)
                    ? "Equipped " + weapons.WeaponOffers[selectedWeapon].displayName + ". Ready when you are."
                    : "Could not equip this weapon.";
                RefreshWeaponPreview();
            };
            toolkitRoot.Q<Button>("spawn-weapon").clicked += () =>
            {
                if (!ValidWeaponSelection()) return;
                weapons.SpawnFromDebug(selectedWeapon, out _, out weaponStatus);
                RefreshWeaponPreview();
            };
            RebuildWeaponFilters();
            RefreshWeaponList();
            SelectToolkitTab(0);
            SyncMenuVisibility();
        }

        private static void AddButtonShine(Button button)
        {
            var shine = new VisualElement { pickingMode = PickingMode.Ignore };
            shine.AddToClassList("button-shine");
            button.Add(shine);
        }

        private static Button Bevel(string text, Action action, string color = "")
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("bevel");
            if (!string.IsNullOrEmpty(color)) button.AddToClassList(color);
            AddButtonShine(button);
            return button;
        }

        private void DrawPaperGrid(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.strokeColor = new Color(.60f, .77f, .81f, .17f);
            painter.lineWidth = 1;
            painter.BeginPath();
            for (float x = 0; x < toolkitShell.layout.width; x += 36)
            { painter.MoveTo(new Vector2(x, 0)); painter.LineTo(new Vector2(x, toolkitShell.layout.height)); }
            for (float y = 0; y < toolkitShell.layout.height; y += 36)
            { painter.MoveTo(new Vector2(0, y)); painter.LineTo(new Vector2(toolkitShell.layout.width, y)); }
            painter.Stroke();
        }

        private void SyncMenuVisibility()
        {
            if (toolkitRoot == null) return;
            toolkitRoot.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!IsOpen) toolkitRoot.focusController?.focusedElement?.Blur();
        }

        private bool ToolkitContainsPointer(Vector2 screenPosition)
        {
            if (!IsOpen || toolkitShell?.panel == null) return false;
            var panelPoint = RuntimePanelUtils.ScreenToPanel(toolkitShell.panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return toolkitShell.worldBound.Contains(panelPoint);
        }

        private void SelectToolkitTab(int tab)
        {
            for (int i = 0; i < navigation.Length; i++) navigation[i].EnableInClassList("selected", i == tab);
            toolkitRoot.Q<Label>("page-title").text = Tabs[tab].ToUpperInvariant();
            toolkitRoot.Q<Label>("page-badge").text = TabBadges[tab];
            toolkitRoot.Q("weapons-page").style.display = tab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var utility = toolkitRoot.Q<ScrollView>("utility-page");
            utility.style.display = tab == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            utility.Clear();
            utility.scrollOffset = Vector2.zero;
            if (tab == 1) BuildMobsPage(utility);
            if (tab == 2) BuildPlayerPage(utility);
            if (tab == 3) BuildCameraPage(utility);
            if (tab == 4) BuildAudioPage(utility);
            if (tab == 5) BuildControlsPage(utility);
        }

        private void SetBrowseMode(int mode)
        {
            weaponBrowseMode = mode;
            RebuildWeaponFilters();
            RefreshWeaponList();
        }

        private void RebuildWeaponFilters()
        {
            toolkitRoot.Q<Button>("browse-type").EnableInClassList("selected", weaponBrowseMode == 0);
            toolkitRoot.Q<Button>("browse-pack").EnableInClassList("selected", weaponBrowseMode == 1);
            var filters = toolkitRoot.Q<ScrollView>("weapon-filters");
            filters.Clear();
            string creator = null;
            int count = weaponBrowseMode == 0 ? Categories.Length : packFilters.Count;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                string text;
                if (weaponBrowseMode == 0) text = Categories[i];
                else
                {
                    var pack = packFilters[i];
                    if (i > 0 && creator != pack.creator)
                    {
                        var heading = new Label(pack.creator); heading.AddToClassList("pack-heading"); filters.Add(heading);
                        creator = pack.creator;
                    }
                    text = i == 0 ? "All Creators / All Packs" : pack.Label;
                }
                var button = new Button(() =>
                {
                    if (weaponBrowseMode == 0) selectedCategory = index; else selectedPack = index;
                    RebuildWeaponFilters();
                    RefreshWeaponList();
                }) { text = text, name = "filter-" + index };
                button.AddToClassList("filter-button");
                button.EnableInClassList("selected", i == (weaponBrowseMode == 0 ? selectedCategory : selectedPack));
                filters.Add(button);
            }
        }

        private void RefreshWeaponList()
        {
            var list = toolkitRoot.Q<ScrollView>("weapon-list");
            list.Clear();
            list.scrollOffset = Vector2.zero;
            weaponRows.Clear();
            int first = -1;
            if (weapons)
            {
                foreach (int index in weaponOrder)
                {
                    var weapon = weapons.WeaponOffers[index];
                    if (!MatchesFilter(weapon)) continue;
                    if (first < 0) first = index;
                    var row = new Button(() =>
                    {
                        selectedWeapon = index;
                        UpdateWeaponSelection();
                        RefreshWeaponPreview();
                    }) { name = "weapon-" + index, tooltip = weapon.displayName };
                    row.AddToClassList("weapon-row");
                    var title = new Label(weapon.displayName) { pickingMode = PickingMode.Ignore };
                    title.AddToClassList("weapon-row-name");
                    var ammo = new Label(weapon.magazine + " RDS") { pickingMode = PickingMode.Ignore };
                    ammo.AddToClassList("weapon-row-ammo");
                    row.Add(title); row.Add(ammo);
                    list.Add(row); weaponRows.Add(index, row);
                }
            }
            if (!weaponRows.ContainsKey(selectedWeapon)) selectedWeapon = first;
            if (weaponRows.Count == 0) AddText(list, "No weapons match. Try another filter or clear your search.");
            toolkitRoot.Q<Label>("weapon-count").text = weaponRows.Count + " WEAPONS";
            UpdateWeaponSelection();
            RefreshWeaponPreview();
        }

        private void UpdateWeaponSelection()
        {
            foreach (var pair in weaponRows) pair.Value.EnableInClassList("selected", pair.Key == selectedWeapon);
        }

        private bool ValidWeaponSelection() => weapons && selectedWeapon >= 0 && selectedWeapon < weapons.WeaponOffers.Count
            && MatchesFilter(weapons.WeaponOffers[selectedWeapon]);

        private void RefreshWeaponPreview()
        {
            var weapon = ValidWeaponSelection() ? weapons.WeaponOffers[selectedWeapon] : null;
            toolkitRoot.Q<Label>("weapon-name").text = weapon ? weapon.displayName : "NO WEAPON SELECTED";
            toolkitRoot.Q<Label>("weapon-source").text = weapon ? CreatorFor(weapon) + " / " + PackFor(weapon) : "Clear your search or choose another category.";
            toolkitRoot.Q<Label>("weapon-stats").text = weapon ? weapon.triggerMode + "   •   " + weapon.magazine + " IN MAGAZINE   /   " + weapon.reserve + " RESERVE" : "";
            var icon = toolkitRoot.Q<Image>("weapon-icon");
            icon.image = weapon ? weapon.icon : null;
            icon.style.display = weapon && weapon.icon ? DisplayStyle.Flex : DisplayStyle.None;
            toolkitRoot.Q<Button>("equip-weapon").SetEnabled(weapon && weapon.model);
            toolkitRoot.Q<Button>("spawn-weapon").SetEnabled(weapon && weapon.model);
            toolkitRoot.Q<Label>("weapon-status").text = weaponStatus;
        }

        private static Label AddText(VisualElement parent, string text, string style = "controls-text")
        {
            var label = new Label(text); label.AddToClassList(style); parent.Add(label); return label;
        }

        private static void AddToggle(VisualElement parent, string label, bool value, Action<bool> changed)
        {
            var toggle = new Toggle(label) { value = value };
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue)); parent.Add(toggle);
        }

        private static void AddVolume(VisualElement parent, string label, float value, Action<float> changed)
        {
            var slider = new Slider(label, 0, 1) { value = value, showInputField = false };
            slider.AddToClassList("volume-slider");
            slider.label = label + "  " + Mathf.RoundToInt(value * 100) + "%";
            slider.RegisterValueChangedCallback(evt =>
            {
                changed(evt.newValue); slider.label = label + "  " + Mathf.RoundToInt(evt.newValue * 100) + "%";
            });
            parent.Add(slider);
        }

        private void BuildMobsPage(VisualElement page)
        {
            if (!mobSpawner || mobSpawner.Count == 0) { AddText(page, "No mobs configured."); return; }
            var selected = AddText(page, mobSpawner.SelectedName, "section-title");
            var controls = new VisualElement(); controls.AddToClassList("action-row"); page.Add(controls);
            var spawn = Bevel("SPAWN " + mobSpawner.SelectedName.ToUpperInvariant(), () => { mobSpawner.SpawnSelected(); SelectToolkitTab(1); }, "coral");
            Action<int> select = delta =>
            {
                mobSpawner.Select(mobSpawner.Selected + delta); selected.text = mobSpawner.SelectedName;
                spawn.text = "SPAWN " + mobSpawner.SelectedName.ToUpperInvariant();
            };
            controls.Add(Bevel("‹ PREVIOUS", () => select(-1)));
            controls.Add(Bevel("NEXT ›", () => select(1)));
            page.Add(spawn);
            page.Add(Bevel("CLEAR SPAWNED MOBS", () => { mobSpawner.ClearSpawned(); SelectToolkitTab(1); }, "amber"));
            var alive = AddText(page, "Alive: " + mobSpawner.AliveCount, "section-title");
            alive.schedule.Execute(() => alive.text = "Alive: " + mobSpawner.AliveCount).Every(500);
            AddText(page, mobSpawner.Status);
        }

        private void BuildPlayerPage(VisualElement page)
        {
            AddText(page, "STAMINA", "section-title");
            if (!playerStamina) { AddText(page, "Player stamina is not available."); return; }
            AddToggle(page, "Infinite stamina", playerStamina.IsInfinite, value => playerStamina.SetInfinite(value));
            AddText(page, "Turn this off to test stamina costs on the movement course.\n\nF6 takes you to the movement course.");
        }

        private void BuildCameraPage(VisualElement page)
        {
            AddText(page, "Current: " + CurrentMode, "section-title");
            page.Add(Bevel("FIXED CAMERA", () => { SetMode(PlayerViewMode.FixedCamera); SelectToolkitTab(3); }, "purple"));
            page.Add(Bevel("THIRD PERSON", () => { SetMode(PlayerViewMode.ThirdPerson); SelectToolkitTab(3); }, "purple"));
            page.Add(Bevel("FREE ROAM", () => { SetMode(PlayerViewMode.FreeRoam); SelectToolkitTab(3); }, "purple"));
            if (CurrentMode == PlayerViewMode.FreeRoam)
                page.Add(Bevel(followCamera.IsFlying ? "PARK CAMERA" : "FLY CAMERA", () => { followCamera.ToggleFreeRoamControl(); SelectToolkitTab(3); }));
            AddText(page, "Close the menu to use camera controls. K switches between flying and controlling the character.");
        }

        private void BuildAudioPage(VisualElement page)
        {
            AddText(page, "VOLUME", "section-title");
            AddVolume(page, "Master", AudioVolumeSettings.MasterVolume, value => AudioVolumeSettings.SetMasterVolume(value));
            AddVolume(page, "Weapons", AudioVolumeSettings.GunVolume, value => AudioVolumeSettings.SetGunVolume(value));
            if (!combatFeedback) return;
            BuildSoundControls(page, true);
            BuildSoundControls(page, false);
        }

        private void BuildSoundControls(VisualElement page, bool hitmarker)
        {
            int count = hitmarker ? combatFeedback.HitmarkerSoundCount : combatFeedback.PunchSoundCount;
            if (count == 0) return;
            AddText(page, hitmarker ? "HITMARKER" : "PUNCH", "section-title");
            AddToggle(page, "Play on contact", hitmarker ? combatFeedback.HitmarkerEnabled : combatFeedback.PunchEnabled,
                value => { if (hitmarker) combatFeedback.SetHitmarkerEnabled(value); else combatFeedback.SetPunchEnabled(value); });
            if (!hitmarker) AddToggle(page, "Randomize each hit", combatFeedback.PunchRandomized, value => combatFeedback.SetPunchRandomized(value));
            Func<int> current = () => hitmarker ? combatFeedback.SelectedHitmarker : combatFeedback.SelectedPunch;
            var label = AddText(page, "Sound " + (current() + 1) + " / " + count, "muted");
            Action<int> select = delta =>
            {
                if (hitmarker) combatFeedback.SelectHitmarker(current() + delta); else combatFeedback.SelectPunch(current() + delta);
                label.text = "Sound " + (current() + 1) + " / " + count;
            };
            var row = new VisualElement(); row.AddToClassList("action-row"); page.Add(row);
            row.Add(Bevel("‹ PREV", () => select(-1), "amber"));
            row.Add(Bevel("PREVIEW", () => { if (hitmarker) combatFeedback.PreviewHitmarker(); else combatFeedback.PreviewPunch(); }, "amber"));
            row.Add(Bevel("NEXT ›", () => select(1), "amber"));
            AddVolume(page, "Volume", hitmarker ? combatFeedback.HitmarkerVolume : combatFeedback.PunchVolume,
                value => { if (hitmarker) combatFeedback.SetHitmarkerVolume(value); else combatFeedback.SetPunchVolume(value); });
        }

        private void BuildControlsPage(VisualElement page)
        {
            AddText(page, "MOVEMENT", "section-title");
            AddText(page, "WASD   Move\nShift + W / double-tap W   Sprint\nSpace   Jump / wall jump\nCtrl   Crouch\nCaps Lock   Roll on ground / dive in air\nHold S + air dive   Dive backward");
            AddText(page, "COMBAT & PICKUPS", "section-title");
            AddText(page, "Left mouse   Fire / unarmed attack\nRight mouse   Aim\nR   Reload     Q   Drop weapon\nF   Pick up / interact with mystery box");
            AddText(page, "CAMERA & TEST AREAS", "section-title");
            AddText(page, "K   Fly / park camera\nFree roam: WASD + mouse, Space / Ctrl up / down, Shift boost\nF5   Gun range     F6   Movement course\nF7   Mob test spot     F9   Reset targets + refill ammo\nF1   Toggle menu     Esc   Release cursor");
        }
    }
}
