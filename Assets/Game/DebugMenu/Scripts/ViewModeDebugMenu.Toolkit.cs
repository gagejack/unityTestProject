using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarterGame
{
    // Deprecated: the original debug menu, now on F2. The pixel-style menu on F1 replaces it; this file will be removed.
    public sealed partial class ViewModeDebugMenu
    {
        [SerializeField, Tooltip("Deprecated. UI document displaying the old F2 debug menu. Its source asset is Game/UI/GameUI.uxml.")]
        private UIDocument menuDocument;

        private VisualElement toolkitRoot, toolkitShell;
        private Button[] navigation;
        private readonly System.Collections.Generic.Dictionary<int, Button> weaponRows = new();
        private static readonly (string section, (string key, string action)[] keys)[] ToolkitControls =
        {
            ("MOVEMENT", new[] { ("WASD", "Move"), ("Shift + WASD", "Sprint in any direction (or double-tap W)"), ("Space", "Jump / wall jump"),
                ("Ctrl", "Crouch"), ("Caps Lock", "Roll on ground / dive in air"), ("Hold S", "Dive backward (in an air dive)") }),
            ("COMBAT & PICKUPS", new[] { ("Left mouse", "Fire / unarmed attack"), ("Right mouse", "Aim"), ("R", "Reload"),
                ("Q", "Drop weapon"), ("F", "Pick up / mystery box") }),
            ("CAMERA & TEST AREAS", new[] { ("K", "Fly / park camera"), ("Free roam", "WASD + mouse, Space / Ctrl up / down, Shift boost"),
                ("F5", "Gun range"), ("F6", "Movement course"), ("F7", "Mob test spot"), ("F8", "Aim ring"), ("F9", "Reset targets + refill ammo"),
                ("F2", "Toggle this deprecated menu"), ("F1", "New menu"), ("Esc", "Release cursor") }),
        };

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
                var button = Choice(Tabs[i].ToUpperInvariant(), () => SelectToolkitTab(tab));
                button.name = "tab-" + Tabs[i].ToLowerInvariant();
                button.AddToClassList("nav-button");
                navigation[i] = button;
                toolkitRoot.Q("navigation").Add(button);
            }
            toolkitRoot.Q<TextField>("weapon-search").textEdition.placeholder = "Search weapons";
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
            InitializePixelMenu();
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

        // A neutral bevel that turns cyan when it carries the "selected" class (tabs, camera modes, arrows, switches).
        private static Button Choice(string text, Action action)
        {
            var button = Bevel(text, action);
            button.AddToClassList("choice");
            return button;
        }

        private static Button SmallChoice(string text, Action action, string extraClass)
        {
            var button = Choice(text, action);
            button.AddToClassList("small-bevel");
            button.AddToClassList(extraClass);
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
            SyncPixelVisibility();
            if (toolkitRoot == null) return;
            bool open = isActiveAndEnabled && showMenu;
            toolkitRoot.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open) toolkitRoot.focusController?.focusedElement?.Blur();
        }

        private bool ToolkitContainsPointer(Vector2 screenPosition)
        {
            if (!(isActiveAndEnabled && showMenu) || toolkitShell?.panel == null) return false;
            var panelPoint = RuntimePanelUtils.ScreenToPanel(toolkitShell.panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return toolkitShell.worldBound.Contains(panelPoint);
        }

        private void SelectToolkitTab(int tab)
        {
            for (int i = 0; i < navigation.Length; i++) navigation[i].EnableInClassList("selected", i == tab);
            toolkitRoot.Q<Label>("page-title").text = Tabs[tab].ToUpperInvariant();
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
            if (weaponRows.Count == 0) AddText(list, "No weapons match. Try another filter or clear your search.").style.marginLeft = 8;
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

        private static Label AddText(VisualElement parent, string text, string style = "hint")
        {
            var label = new Label(text); label.AddToClassList(style); parent.Add(label); return label;
        }

        private static void AddSection(VisualElement page, string text)
        {
            AddText(page, text, "section-title").EnableInClassList("first", page.childCount == 1);
        }

        // A row with a fixed-width label column, so the controls of every setting start on the same line.
        private static VisualElement AddSetting(VisualElement page, string label)
        {
            var row = new VisualElement(); row.AddToClassList("setting-row"); page.Add(row);
            AddText(row, label, "setting-label");
            return row;
        }

        // ‹ value ›. Returns the row so callers can add a button after it.
        private static VisualElement AddStepper(VisualElement page, string label, Func<string> text, Action<int> step)
        {
            var row = AddSetting(page, label);
            Label value = null;
            row.Add(SmallChoice("◀", () => { step(-1); value.text = text(); }, "arrow"));
            value = AddText(row, text(), "setting-value");
            row.Add(SmallChoice("▶", () => { step(1); value.text = text(); }, "arrow"));
            return row;
        }

        // ON (green) / OFF (neutral) button.
        private static void AddSwitch(VisualElement page, string label, Func<bool> get, Action<bool> set)
        {
            Button button = null;
            Action refresh = () => { button.text = get() ? "ON" : "OFF"; button.EnableInClassList("on", get()); };
            button = SmallChoice("", () => { set(!get()); refresh(); }, "switch");
            AddSetting(page, label).Add(button);
            refresh();
        }

        private static void AddVolume(VisualElement page, string label, float value, Action<float> changed)
        {
            var row = AddSetting(page, label);
            var slider = new Slider(0, 1) { value = value, showInputField = false };
            slider.AddToClassList("volume-slider");
            row.Add(slider);
            var percent = AddText(row, Mathf.RoundToInt(value * 100) + "%", "volume-value");
            slider.RegisterValueChangedCallback(evt =>
            {
                changed(evt.newValue); percent.text = Mathf.RoundToInt(evt.newValue * 100) + "%";
            });
        }

        private void BuildMobsPage(VisualElement page)
        {
            if (!mobSpawner || mobSpawner.Count == 0) { AddText(page, "No mobs configured."); return; }
            AddStepper(page, "Mob", () => mobSpawner.SelectedName, delta => mobSpawner.Select(mobSpawner.Selected + delta));
            var alive = AddText(AddSetting(page, "Alive"), mobSpawner.AliveCount.ToString(), "setting-value");
            alive.AddToClassList("alive");
            alive.schedule.Execute(() => alive.text = mobSpawner.AliveCount.ToString()).Every(500);
            var actions = AddSetting(page, "");
            actions.AddToClassList("setting-actions");
            actions.Add(Bevel("SPAWN", () => { mobSpawner.SpawnSelected(); SelectToolkitTab(1); }, "coral"));
            actions.Add(Bevel("CLEAR ALL", () => { mobSpawner.ClearSpawned(); SelectToolkitTab(1); }, "amber"));
            AddText(page, mobSpawner.Status);
        }

        private void BuildPlayerPage(VisualElement page)
        {
            AddSection(page, "STAMINA");
            if (!playerStamina) { AddText(page, "Player stamina is not available."); return; }
            AddSwitch(page, "Infinite stamina", () => playerStamina.IsInfinite, value => playerStamina.SetInfinite(value));
            AddText(page, "Turn this off to test stamina costs on the movement course (F6).");
        }

        private void BuildCameraPage(VisualElement page)
        {
            var row = AddSetting(page, "View");
            foreach (var (mode, label) in new[] { (PlayerViewMode.FixedCamera, "FIXED"), (PlayerViewMode.ThirdPerson, "THIRD PERSON"), (PlayerViewMode.FreeRoam, "FREE ROAM") })
            {
                var button = Choice(label, () => { SetMode(mode); SelectToolkitTab(3); });
                button.AddToClassList("segment");
                button.EnableInClassList("selected", CurrentMode == mode);
                row.Add(button);
            }
            if (CurrentMode == PlayerViewMode.FreeRoam)
            {
                var fly = Bevel(followCamera.IsFlying ? "PARK" : "FLY", () => { followCamera.ToggleFreeRoamControl(); SelectToolkitTab(3); });
                fly.AddToClassList("segment");
                AddSetting(page, "Free camera").Add(fly);
            }
            AddText(page, "Close the menu to use camera controls. K switches between flying and controlling the character.");
        }

        private void BuildAudioPage(VisualElement page)
        {
            AddSection(page, "VOLUME");
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
            AddSection(page, hitmarker ? "HITMARKER" : "PUNCH");
            AddSwitch(page, "Play on contact", () => hitmarker ? combatFeedback.HitmarkerEnabled : combatFeedback.PunchEnabled,
                value => { if (hitmarker) combatFeedback.SetHitmarkerEnabled(value); else combatFeedback.SetPunchEnabled(value); });
            if (!hitmarker) AddSwitch(page, "Randomize each hit", () => combatFeedback.PunchRandomized, value => combatFeedback.SetPunchRandomized(value));
            Func<int> current = () => hitmarker ? combatFeedback.SelectedHitmarker : combatFeedback.SelectedPunch;
            var row = AddStepper(page, "Sound", () => (current() + 1) + " / " + count,
                delta => { if (hitmarker) combatFeedback.SelectHitmarker(current() + delta); else combatFeedback.SelectPunch(current() + delta); });
            var preview = Bevel("PREVIEW", () => { if (hitmarker) combatFeedback.PreviewHitmarker(); else combatFeedback.PreviewPunch(); }, "amber");
            preview.AddToClassList("small-bevel");
            preview.AddToClassList("compact-action");
            row.Add(preview);
            AddVolume(page, "Volume", hitmarker ? combatFeedback.HitmarkerVolume : combatFeedback.PunchVolume,
                value => { if (hitmarker) combatFeedback.SetHitmarkerVolume(value); else combatFeedback.SetPunchVolume(value); });
        }

        private static void BuildControlsPage(VisualElement page)
        {
            foreach (var (section, keys) in ToolkitControls)
            {
                AddSection(page, section);
                var grid = new VisualElement(); grid.AddToClassList("key-grid"); page.Add(grid);
                foreach (var (key, action) in keys)
                {
                    var pair = new VisualElement(); pair.AddToClassList("key-pair"); grid.Add(pair);
                    AddText(pair, key, "keycap");
                    AddText(pair, action, "key-action");
                }
            }
        }
    }
}
