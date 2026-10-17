using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Animation.Editor
{
    /// <summary>
    /// Rebuilds the block player's Humanoid avatar from the rig in the open scene.
    /// - Root at the feet: the avatar root sits level with the toes, so Unity's human scale is positive and
    ///   the clips' hip bob and sway reach the body (the old root sat at the body's centre, above the hips).
    /// - Arm down range: the block body is wider than the human the clips were made for, so the "Arm Down-Up"
    ///   muscle gets a longer down range; the same clips then hang the arms closer to the torso.
    /// Running it again is safe: it only re-roots once and rewrites the avatar with the current values.
    /// </summary>
    public static class PlayerAvatarSetup
    {
        const string AvatarPath = "Assets/Game/Animation/Avatars/BlockHumanoidAvatar.asset";
        /// <summary>Degrees of "Arm Down-Up" range below neutral (Unity's default is 60).</summary>
        public const float ArmDownRange = 76f;

        [MenuItem("Tools/Animation/Rebuild Player Avatar")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play mode before rebuilding the player avatar."); return; }
            var player = GameObject.Find("Player");
            var rig = player ? player.transform.Find("Humanoid Rig") : null;
            var animator = rig ? rig.GetComponent<Animator>() : null;
            if (!animator || !animator.avatar) { Debug.LogError("Open the Main scene: Player/Humanoid Rig with an Animator was not found."); return; }

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var toes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            animator.Rebind();
            float toesBelowRoot = rig.InverseTransformPoint(toes.position).y;
            var description = animator.avatar.humanDescription;
            if (toesBelowRoot < -0.01f)
            {
                // Move the root down to the toes and the hips up by the same amount: no bone moves in the world.
                Undo.RecordObjects(new Object[] { rig, hips }, "Re-root player avatar");
                rig.localPosition += Vector3.up * toesBelowRoot;
                hips.localPosition -= Vector3.up * toesBelowRoot;
                UpdateRestBones(player, hips, -toesBelowRoot);
            }
            var skeleton = description.skeleton;
            for (int i = 0; i < skeleton.Length; i++)
            {
                // The avatar root's own entry stays at the origin: Unity measures the hips relative to it.
                if (skeleton[i].name == rig.name) skeleton[i].position = Vector3.zero;
                if (skeleton[i].name == hips.name) skeleton[i].position = hips.localPosition;
            }
            description.skeleton = skeleton;

            var human = description.human;
            for (int i = 0; i < human.Length; i++)
            {
                if (human[i].humanName != "LeftUpperArm" && human[i].humanName != "RightUpperArm") continue;
                var limit = human[i].limit;
                limit.useDefaultValues = false;
                // x = twist, y = front-back, z = down-up (Unity defaults: 90, 100, 60 down / 100 up).
                limit.min = new Vector3(-90f, -100f, -ArmDownRange);
                limit.max = new Vector3(90f, 100f, 100f);
                limit.center = Vector3.zero;
                human[i].limit = limit;
            }
            description.human = human;

            var built = AvatarBuilder.BuildHumanAvatar(rig.gameObject, description);
            if (!built.isValid || !built.isHuman) { Debug.LogError("The rebuilt avatar is not a valid Humanoid; nothing was saved."); return; }
            var asset = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            built.name = asset ? asset.name : "BlockHumanoidAvatar";
            if (asset) EditorUtility.CopySerialized(built, asset); else AssetDatabase.CreateAsset(built, AvatarPath);
            asset = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            animator.avatar = asset;
            animator.Rebind();
            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
            Debug.Log($"Player avatar rebuilt: human scale {animator.humanScale:F3}, rig root at {rig.localPosition}, arm down range {ArmDownRange}°.");
        }

        // BlockHumanoidRig poses special moves from stored rest positions; keep the hips entry in step with the re-root.
        static void UpdateRestBones(GameObject player, Transform hips, float hipsRaise)
        {
            var rig = player.GetComponent("BlockHumanoidRig");
            if (!rig) return;
            var so = new SerializedObject(rig);
            var rest = so.FindProperty("restBones");
            for (int i = 0; i < rest.arraySize; i++)
            {
                var entry = rest.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("bone").objectReferenceValue != hips) continue;
                var position = entry.FindPropertyRelative("position");
                position.vector3Value += Vector3.up * hipsRaise;
            }
            so.ApplyModifiedProperties();
        }
    }
}
