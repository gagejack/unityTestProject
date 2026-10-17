using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarterGame
{
    // F2 pixel-style menu (docs/spec/UI/DebugMenu/DESIGN_SPEC.md). Shares the F1 menu's open state, input gate and actions.
    public sealed partial class ViewModeDebugMenu
    {
        [SerializeField, Tooltip("UI document displaying the F2 pixel-style debug menu. Its source asset is Game/UI/Prototype/PixelDebugMenu.uxml.")]
        private UIDocument pixelMenuDocument;

        private static readonly string[] Hues = { "green", "blue", "red", "orange", "gray" };
        private static readonly (string section, (string key, string action)[] keys)[] PixelControls =
        {
            ("MOVEMENT", new[] { ("WASD", "MOVE"), ("SHIFT + W", "SPRINT"), ("SPACE", "JUMP"), ("CTRL", "CROUCH"),
                ("CAPS", "ROLL / DIVE"), ("S + DIVE", "BACK DIVE") }),
            ("COMBAT", new[] { ("LMB", "FIRE"), ("RMB", "AIM"), ("R", "RELOAD"), ("Q", "DROP"), ("F", "PICK UP") }),
            ("TOOLS", new[] { ("F5", "GUN RANGE"), ("F6", "MOVEMENT"), ("F7", "MOBS"), ("F9", "RESET"),
                ("K", "FLY CAMERA"), ("F1 / F2", "MENU"), ("ESC", "CURSOR") }),
        };

        private VisualElement pixelRoot, pixelShell;
        private Button[] pixelNavigation;
        private int pixelTab, pixelCategory;
        private bool pixelOpen;

        private void InitializePixelMenu()
        {
            if (!pixelMenuDocument || pixelMenuDocument.rootVisualElement.Q("px-shell") == null) return;
            pixelRoot = pixelMenuDocument.rootVisualElement;
            pixelShell = pixelRoot.Q("px-shell");
            pixelRoot.pickingMode = PickingMode.Ignore;
            pixelRoot.Q("px-overlay").pickingMode = PickingMode.Ignore;
            pixelNavigation = new Button[Tabs.Length];
            for (int i = 0; i < Tabs.Length; i++)
            {
                int tab = i;
                pixelNavigation[i] = PixelButton(Tabs[i].ToUpperInvariant(), "gray", () => SelectPixelTab(tab));
                pixelNavigation[i].name = "px-tab-" + Tabs[i].ToLowerInvariant();
                pixelRoot.Q("px-nav").Add(pixelNavigation[i]);
            }
            var close = PixelButton("CLOSE", "red", () => SetOpen(false));
            close.name = "px-close";
            pixelRoot.Q("px-close-slot").Add(close);
            SelectPixelTab(0);
        }

        public void SetPixelOpen(bool value)
        {
            pixelOpen = value;
            if (value) showMenu = false;
            SyncMenuVisibility();
            if (followCamera) followCamera.SetMouseCaptured(!value);
            if (value) SelectPixelTab(pixelTab); // Values may have changed while the menu was closed.
        }

        // The label is a child element, not Button.text: a child label is measured at the button's real width,
        // so a long name wraps and the button grows taller instead of overflowing.
        private static Button PixelButton(string text, string hue, Action action)
        {
            var button = PixelFrame(new Button(action), hue);
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("px-text");
            button.Add(label);
            return button;
        }

        private static void SetPixelText(Button button, string text) => button.Q<Label>(className: "px-text").text = text;

        // Adds the button frame classes and the highlight, mid-line and lip bands.
        private static T PixelFrame<T>(T element, string hue) where T : VisualElement
        {
            element.AddToClassList("px-btn");
            element.AddToClassList(hue);
            foreach (string band in new[] { "px-hi", "px-mid", "px-lip" })
            {
                var child = new VisualElement { pickingMode = PickingMode.Ignore };
                child.AddToClassList(band);
                element.Add(child);
            }
            return element;
        }

        private static void SetHue(VisualElement element, string hue)
        {
            foreach (string h in Hues) element.EnableInClassList(h, h == hue);
        }

        private static VisualElement PixelRow(VisualElement parent, string className = "px-row")
        {
            var row = new VisualElement();
            row.AddToClassList(className);
            parent.Add(row);
            return row;
        }

        // A value in an outlined box; not clickable.
        private static Label PixelReadout(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList("px-readout");
            parent.Add(label);
            return label;
        }

        private static void PixelSection(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList("px-section");
            if (parent.childCount == 0) label.AddToClassList("px-first");
            parent.Add(label);
        }

        // A row with a fixed-width label column, so the controls of every setting start on the same line.
        private static VisualElement PixelSetting(VisualElement parent, string label)
        {
            var row = PixelRow(parent);
            var text = new Label(label);
            text.AddToClassList("px-setting-label");
            row.Add(text);
            return row;
        }

        private static Button PixelSquare(string text, string hue, Action action)
        {
            var button = PixelButton(text, hue, action);
            button.AddToClassList("px-square");
            return button;
        }

        // Adds ◀ value ▶ to the row and returns it, so callers can add a button after it.
        private static VisualElement PixelStepper(VisualElement row, Func<string> text, Action<int> step, string name = null)
        {
            Action refresh = null;
            row.Add(PixelSquare("◀", "blue", () => { step(-1); refresh(); }));
            var value = PixelReadout(row, "");
            if (name != null) value.name = name;
            row.Add(PixelSquare("▶", "blue", () => { step(1); refresh(); }));
            refresh = () => value.text = text();
            refresh();
            return row;
        }

        private static void PixelToggle(VisualElement parent, string label, Func<bool> get, Action<bool> set)
        {
            var row = PixelSetting(parent, label);
            Button button = null;
            Action refresh = () => { SetPixelText(button, get() ? "ON" : "OFF"); SetHue(button, get() ? "green" : "red"); };
            button = PixelButton("", "green", () => { set(!get()); refresh(); });
            button.AddToClassList("px-toggle");
            row.Add(button);
            refresh();
        }

        private static void PixelVolume(VisualElement parent, string label, Func<float> get, Action<float> set)
        {
            PixelStepper(PixelSetting(parent, label), () => Mathf.RoundToInt(get() * 100) + "%",
                delta => set(Mathf.Clamp01(Mathf.Round(get() * 10 + delta) / 10f)));
        }

        private void SelectPixelTab(int tab)
        {
            if (pixelRoot == null) return;
            pixelTab = tab;
            for (int i = 0; i < pixelNavigation.Length; i++) pixelNavigation[i].EnableInClassList("selected", i == tab);
            var body = pixelRoot.Q<ScrollView>("px-body");
            var header = pixelRoot.Q("px-header");
            body.Clear();
            body.scrollOffset = Vector2.zero;
            header.Clear();
            if (tab == 0) BuildPixelWeapons(body, header);
            if (tab == 1) BuildPixelMobs(body);
            if (tab == 2) BuildPixelPlayer(body);
            if (tab == 3) BuildPixelCamera(body);
            if (tab == 4) BuildPixelAudio(body);
            if (tab == 5) BuildPixelControls(body);
            header.style.display = header.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            // The weapon list keeps its scrollbar even when short, so its rows always end where the header ends.
            body.verticalScrollerVisibility = tab == 0 ? ScrollerVisibility.AlwaysVisible : ScrollerVisibility.Auto;
        }

        private bool InPixelCategory(WeaponSettings weapon) => weapon && (pixelCategory == 0 || (int)weapon.category == pixelCategory - 1);

        // Category stepper in the fixed header, then one row per weapon: name, EQUIP, SPAWN.
        private void BuildPixelWeapons(VisualElement body, VisualElement header)
        {
            if (!weapons || weaponOrder.Length == 0) return;
            PixelStepper(PixelRow(header), () => Categories[pixelCategory].ToUpperInvariant(), delta =>
            {
                pixelCategory = (pixelCategory + delta + Categories.Length) % Categories.Length;
                SelectPixelTab(0);
            }, "px-category");
            foreach (int index in weaponOrder.Where(i => InPixelCategory(weapons.WeaponOffers[i])))
            {
                var weapon = weapons.WeaponOffers[index];
                var row = PixelRow(body);
                row.name = "px-weapon-" + index;
                row.AddToClassList("px-weapon");
                var name = new Label(weapon.displayName.ToUpperInvariant());
                name.AddToClassList("px-weapon-name");
                var equip = PixelButton("EQUIP", "green", () => weapons.EquipFromDebug(index));
                equip.name = "px-equip-" + index;
                var spawn = PixelButton("SPAWN", "orange", () => weapons.SpawnFromDebug(index, out _, out _));
                spawn.name = "px-spawn-" + index;
                foreach (var button in new[] { equip, spawn })
                {
                    button.AddToClassList("px-compact");
                    button.SetEnabled(weapon.model);
                }
                row.Add(name);
                row.Add(equip);
                row.Add(spawn);
            }
        }

        private void BuildPixelMobs(VisualElement body)
        {
            if (!mobSpawner || mobSpawner.Count == 0) return;
            PixelStepper(PixelSetting(body, "MOB"), () => mobSpawner.SelectedName.ToUpperInvariant(), delta => mobSpawner.Select(mobSpawner.Selected + delta));
            var alive = PixelReadout(PixelSetting(body, "ALIVE"), "");
            alive.name = "px-alive";
            Action refresh = () => alive.text = mobSpawner.AliveCount.ToString();
            refresh();
            alive.schedule.Execute(refresh).Every(500);
            var actions = PixelSetting(body, "");
            actions.AddToClassList("px-actions");
            actions.Add(PixelButton("SPAWN", "orange", () => mobSpawner.SpawnSelected()));
            actions.Add(PixelButton("CLEAR", "red", () => mobSpawner.ClearSpawned()));
        }

        private void BuildPixelPlayer(VisualElement body)
        {
            if (!playerStamina) return;
            PixelToggle(body, "INFINITE STAMINA", () => playerStamina.IsInfinite, value => playerStamina.SetInfinite(value));
        }

        private void BuildPixelCamera(VisualElement body)
        {
            var row = PixelSetting(body, "VIEW");
            foreach (var (mode, label) in new[] { (PlayerViewMode.FixedCamera, "FIXED"), (PlayerViewMode.ThirdPerson, "THIRD"), (PlayerViewMode.FreeRoam, "FREE") })
            {
                var button = PixelButton(label, "gray", () => { SetMode(mode); SelectPixelTab(3); });
                button.EnableInClassList("selected", CurrentMode == mode);
                row.Add(button);
            }
            if (CurrentMode == PlayerViewMode.FreeRoam)
                PixelSetting(body, "FLIGHT").Add(PixelButton(followCamera.IsFlying ? "PARK" : "FLY", "orange", () => { followCamera.ToggleFreeRoamControl(); SelectPixelTab(3); }));
        }

        private void BuildPixelAudio(VisualElement body)
        {
            PixelSection(body, "VOLUME");
            PixelVolume(body, "MASTER", () => AudioVolumeSettings.MasterVolume, value => AudioVolumeSettings.SetMasterVolume(value));
            PixelVolume(body, "WEAPONS", () => AudioVolumeSettings.GunVolume, value => AudioVolumeSettings.SetGunVolume(value));
            if (!combatFeedback) return;
            BuildPixelSound(body, true);
            BuildPixelSound(body, false);
        }

        private void BuildPixelSound(VisualElement body, bool hitmarker)
        {
            int count = hitmarker ? combatFeedback.HitmarkerSoundCount : combatFeedback.PunchSoundCount;
            if (count == 0) return;
            PixelSection(body, hitmarker ? "HITMARKER" : "PUNCH");
            PixelToggle(body, "ON CONTACT", () => hitmarker ? combatFeedback.HitmarkerEnabled : combatFeedback.PunchEnabled,
                value => { if (hitmarker) combatFeedback.SetHitmarkerEnabled(value); else combatFeedback.SetPunchEnabled(value); });
            if (!hitmarker) PixelToggle(body, "RANDOM", () => combatFeedback.PunchRandomized, value => combatFeedback.SetPunchRandomized(value));
            Func<int> current = () => hitmarker ? combatFeedback.SelectedHitmarker : combatFeedback.SelectedPunch;
            var sound = PixelStepper(PixelSetting(body, "SOUND"), () => (current() + 1) + " / " + count,
                delta => { if (hitmarker) combatFeedback.SelectHitmarker(current() + delta); else combatFeedback.SelectPunch(current() + delta); });
            sound.Add(PixelButton("PLAY", "orange", () => { if (hitmarker) combatFeedback.PreviewHitmarker(); else combatFeedback.PreviewPunch(); }));
            PixelVolume(body, "VOLUME", () => hitmarker ? combatFeedback.HitmarkerVolume : combatFeedback.PunchVolume,
                value => { if (hitmarker) combatFeedback.SetHitmarkerVolume(value); else combatFeedback.SetPunchVolume(value); });
        }

        private static void BuildPixelControls(VisualElement body)
        {
            foreach (var (section, keys) in PixelControls)
            {
                PixelSection(body, section);
                var grid = PixelRow(body, "px-grid");
                foreach (var (key, action) in keys)
                {
                    var pair = PixelRow(grid, "px-control");
                    PixelReadout(pair, key).AddToClassList("px-key");
                    var label = new Label(action);
                    label.AddToClassList("px-control-action");
                    pair.Add(label);
                }
            }
        }

        private void SyncPixelVisibility()
        {
            if (pixelRoot == null) return;
            pixelRoot.style.display = isActiveAndEnabled && pixelOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!(isActiveAndEnabled && pixelOpen)) pixelRoot.focusController?.focusedElement?.Blur();
        }

        private bool PixelContainsPointer(Vector2 screenPosition)
        {
            if (!(isActiveAndEnabled && pixelOpen) || pixelShell?.panel == null) return false;
            var panelPoint = RuntimePanelUtils.ScreenToPanel(pixelShell.panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return pixelShell.worldBound.Contains(panelPoint);
        }
    }
}
