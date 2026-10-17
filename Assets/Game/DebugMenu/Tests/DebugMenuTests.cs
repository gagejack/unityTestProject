using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.TestAreas;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.DebugMenu.Tests
{
    public sealed class DebugMenuTests : InputTestFixture
    {
        MonoBehaviour menu;
        VisualElement root;
        Keyboard keyboard;

        IEnumerator LoadMenu()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>();
            menu = Object.FindObjectsByType<MonoBehaviour>().Single(b => b.GetType().Name == "ViewModeDebugMenu");
            root = ((UIDocument)menu.GetType().GetField("menuDocument", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu)).rootVisualElement;
            menu.GetType().GetMethod("SetOpen").Invoke(menu, new object[] { true });
            yield return null;
        }

        static void Click(Button button)
        {
            Assert.IsNotNull(button);
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

        bool IsOpen => (bool)menu.GetType().GetProperty("IsOpen").GetValue(menu);

        [UnityTest]
        public IEnumerator PlayModeStartsWithBothMenusClosed()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            menu = Object.FindObjectsByType<MonoBehaviour>().Single(b => b.GetType().Name == "ViewModeDebugMenu");
            Assert.IsFalse(IsOpen);
            Assert.IsTrue(PlayerControl.Enabled);
            foreach (string field in new[] { "menuDocument", "pixelMenuDocument" })
            {
                var document = (UIDocument)menu.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
                Assert.AreEqual(DisplayStyle.None, document.rootVisualElement.resolvedStyle.display, field);
            }
        }

        [UnityTest]
        public IEnumerator F2TogglesDeprecatedMenuAndPlayerControlWhileSearchHasFocus()
        {
            yield return LoadMenu();
#if UNITY_EDITOR
            UnityEditor.EditorWindow.GetWindow(System.Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            yield return null;
#endif
            Assert.IsTrue(Application.isFocused, "The keyboard shortcut test requires the Game view to have application focus.");
            Assert.IsFalse(PlayerControl.Enabled);
            root.Q<TextField>("weapon-search").Focus();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F2));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.IsFalse(IsOpen);
            Assert.IsTrue(PlayerControl.Enabled);
            Assert.AreEqual(DisplayStyle.None, root.resolvedStyle.display);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F2));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.IsTrue(IsOpen);
            Assert.IsFalse(PlayerControl.Enabled);
            Assert.AreEqual(CursorLockMode.None, UnityEngine.Cursor.lockState);
        }

        [UnityTest]
        public IEnumerator F1OpensPixelMenuInPlaceOfDeprecatedMenuAndCloseButtonCloses()
        {
            yield return LoadMenu();
#if UNITY_EDITOR
            UnityEditor.EditorWindow.GetWindow(System.Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            yield return null;
#endif
            var pixelRoot = ((UIDocument)menu.GetType().GetField("pixelMenuDocument", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu)).rootVisualElement;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.IsTrue(IsOpen);
            Assert.IsFalse(PlayerControl.Enabled);
            Assert.AreEqual(DisplayStyle.None, root.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, pixelRoot.resolvedStyle.display);
            var shell = pixelRoot.Q("px-shell");
            Vector2 centre = shell.worldBound.center;
            Vector2 screenCentre = new Vector2(centre.x / pixelRoot.worldBound.width * Screen.width,
                Screen.height - centre.y / pixelRoot.worldBound.height * Screen.height);
            Assert.IsTrue((bool)menu.GetType().GetMethod("ContainsPointer").Invoke(menu, new object[] { screenCentre }));
            foreach (string tab in new[] { "weapons", "mobs", "player", "camera", "audio", "controls" })
            {
                Click(pixelRoot.Q<Button>("px-tab-" + tab));
                yield return null;
                Assert.Greater(pixelRoot.Q<ScrollView>("px-body").contentContainer.childCount, 0, tab);
            }
            Click(pixelRoot.Q<Button>("px-close"));
            yield return null;
            Assert.IsFalse(IsOpen);
            Assert.IsTrue(PlayerControl.Enabled);
            Assert.AreEqual(DisplayStyle.None, pixelRoot.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator F1WeaponRowEquipButtonEquipsThatWeapon()
        {
            yield return LoadMenu();
            TestStartMarker.Find("GR-Firing").TeleportPlayer();
            menu.GetType().GetMethod("SetPixelOpen").Invoke(menu, new object[] { true });
            yield return null;
            var pixelRoot = ((UIDocument)menu.GetType().GetField("pixelMenuDocument", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu)).rootVisualElement;
            var rows = pixelRoot.Q("px-body").Query(className: "px-row").Where(r => r.name.StartsWith("px-weapon-")).ToList();
            Assert.Greater(rows.Count, 10);
            var pistol = rows.First(r => r.Q<Label>(className: "px-weapon-name").text == "M1911");
            Click(pistol.Q<Button>(className: "green"));
            yield return null;
            var weapons = Object.FindObjectsByType<MonoBehaviour>().Single(b => b.GetType().Name == "WeaponCombatController");
            var current = weapons.GetType().GetProperty("CurrentWeapon").GetValue(weapons);
            Assert.AreEqual("M1911", ((string)current.GetType().GetField("displayName").GetValue(current)).ToUpperInvariant());
        }

        [UnityTest]
        public IEnumerator SearchFiltersCaseInsensitivelyAndEmptyResultsDisableActions()
        {
            yield return LoadMenu();
            var search = root.Q<TextField>("weapon-search");
            int total = root.Q("weapon-list").Query<Button>().ToList().Count;
            Assert.Greater(total, 10);
            search.value = "  rIfLe  ";
            yield return null;
            var labels = root.Q("weapon-list").Query<Label>(className: "weapon-row-name").ToList();
            Assert.IsNotEmpty(labels);
            Assert.IsTrue(labels.All(label => label.text.ToLowerInvariant().Contains("rifle")));
            search.value = "no-such-weapon-123";
            yield return null;
            Assert.IsFalse(root.Q<Button>("equip-weapon").enabledSelf);
            Assert.IsFalse(root.Q<Button>("spawn-weapon").enabledSelf);
            Assert.AreEqual("NO WEAPON SELECTED", root.Q<Label>("weapon-name").text);
            Click(root.Q<Button>("clear-search"));
            yield return null;
            Assert.AreEqual(total, root.Q("weapon-list").Query<Button>().ToList().Count);
            Assert.IsTrue(root.Q<Button>("equip-weapon").enabledSelf);
        }

        [UnityTest]
        public IEnumerator EquipAndSpawnButtonsUseTheSelectedWeaponAtGunRange()
        {
            yield return LoadMenu();
            TestStartMarker.Find("GR-Firing").TeleportPlayer();
            yield return null;
            root.Q<TextField>("weapon-search").value = "M1911";
            yield return null;
            var rows = root.Q("weapon-list").Query<Button>().ToList();
            Assert.IsNotEmpty(rows);
            Click(rows[0]);
            yield return null;
            string selected = root.Q<Label>("weapon-name").text;
            Click(root.Q<Button>("equip-weapon"));
            yield return null;
            var weapons = Object.FindObjectsByType<MonoBehaviour>().Single(b => b.GetType().Name == "WeaponCombatController");
            Assert.IsTrue((bool)weapons.GetType().GetProperty("HasWeapon").GetValue(weapons));
            var current = weapons.GetType().GetProperty("CurrentWeapon").GetValue(weapons);
            Assert.AreEqual(selected, current.GetType().GetField("displayName").GetValue(current));
            int before = Object.FindObjectsByType<MonoBehaviour>().Count(b => b.GetType().Name == "DroppedWeapon");
            Click(root.Q<Button>("spawn-weapon"));
            yield return null;
            Assert.AreEqual(before + 1, Object.FindObjectsByType<MonoBehaviour>().Count(b => b.GetType().Name == "DroppedWeapon"), root.Q<Label>("weapon-status").text);
        }

        [UnityTest]
        public IEnumerator TabsPreserveWeaponSearchAndDisableHidesTheDocument()
        {
            yield return LoadMenu();
            root.Q<TextField>("weapon-search").value = "rifle";
            foreach (string tab in new[] { "mobs", "player", "camera", "audio", "controls" })
            {
                Click(root.Q<Button>("tab-" + tab));
                yield return null;
                Assert.AreEqual(tab.ToUpperInvariant(), root.Q<Label>("page-title").text);
                Assert.Greater(root.Q<ScrollView>("utility-page").childCount, 0);
            }
            Click(root.Q<Button>("tab-weapons"));
            yield return null;
            Assert.AreEqual("rifle", root.Q<TextField>("weapon-search").value);
            menu.enabled = false;
            yield return null;
            Assert.AreEqual(DisplayStyle.None, root.resolvedStyle.display);
            Assert.IsTrue(PlayerControl.Enabled);
            menu.enabled = true;
            yield return null;
            Assert.IsTrue(IsOpen);
            Assert.AreEqual(DisplayStyle.Flex, root.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator MenuHitAreaMatchesTheVisiblePanel()
        {
            yield return LoadMenu();
            var panel = root.Q("debug-shell");
            Vector2 panelCentre = panel.worldBound.center;
            Vector2 screenCentre = new Vector2(panelCentre.x / root.worldBound.width * Screen.width,
                Screen.height - panelCentre.y / root.worldBound.height * Screen.height);
            var contains = menu.GetType().GetMethod("ContainsPointer");
            Assert.IsTrue((bool)contains.Invoke(menu, new object[] { screenCentre }));
            Assert.IsFalse((bool)contains.Invoke(menu, new object[] { Vector2.zero }));
            menu.GetType().GetMethod("SetOpen").Invoke(menu, new object[] { false });
            Assert.IsFalse((bool)contains.Invoke(menu, new object[] { screenCentre }));
        }
    }
}
