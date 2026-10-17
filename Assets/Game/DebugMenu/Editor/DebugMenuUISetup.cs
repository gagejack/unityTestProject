using StarterGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class DebugMenuUISetup
{
    [MenuItem("Tools/Project/Set Up Debug Menu UI")]
    public static void SetUp()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before setting up the menu.");
        var menu = Object.FindAnyObjectByType<ViewModeDebugMenu>();
        if (!menu) throw new System.InvalidOperationException("Open the Main scene first.");
        var serializedMenu = new SerializedObject(menu);
        var document = serializedMenu.FindProperty("menuDocument").objectReferenceValue as UIDocument;
        if (!document)
        {
            // Reuse the user's empty UI document, when present.
            foreach (var candidate in Object.FindObjectsByType<UIDocument>())
                if (candidate.name == "UIDocument" || candidate.name == "Debug Menu UI") { document = candidate; break; }
        }
        if (!document)
        {
            var go = new GameObject("Debug Menu UI");
            Undo.RegisterCreatedObjectUndo(go, "Create debug menu UI");
            document = Undo.AddComponent<UIDocument>(go);
        }
        const string panelPath = "Assets/Game/UI/DebugMenuPanelSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
        if (!settings)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(settings, panelPath);
        }
        Undo.RecordObject(settings, "Configure debug menu panel");
        settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Game/UI/DebugMenuTheme.tss");
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1440, 900);
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = .5f;
        settings.sortingOrder = 100;
        EditorUtility.SetDirty(settings);
        Undo.RecordObject(document, "Configure debug menu document");
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Game/UI/GameUI.uxml");
        document.panelSettings = settings;
        serializedMenu.FindProperty("menuDocument").objectReferenceValue = document;
        serializedMenu.ApplyModifiedProperties();
        EditorUtility.SetDirty(document);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Debug menu UI is ready. Save the scene, then press Play and F1.");
    }
}
