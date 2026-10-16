using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.TestAreas.Editor
{
    /// <summary>
    /// Builds the Gun Range and Movement Course in the open scene from primitives.
    /// Running it again deletes and rebuilds only the objects it owns.
    /// Layout and purpose are documented in docs/TEST_AREAS.md.
    /// </summary>
    public static class TestAreaBuilder
    {
        const string GunRangeName = "Gun Range";
        const string CourseName = "Movement Course";
        const string ToolsName = "Test Area Tools";
        const string OldLaneName = "Combat Practice Lane";
        const string MaterialFolder = "Assets/Game/TestAreas/Materials";
        const float FloorY = -0.5f;
        const float LabelScale = 2.5f;

        static Material floor, platform, wall, wallJump, slope, marker, start, targetBody, targetHead, cover;
        static Font font;

        [MenuItem("Tools/Test Areas/Rebuild Gun Range and Movement Course")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play mode before rebuilding the test areas."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            float targetHitPoints = ReadOldTargetHitPoints(100f);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == GunRangeName || root.name == CourseName || root.name == ToolsName || root.name == OldLaneName)
                    Object.DestroyImmediate(root);
            LoadMaterials();
            BuildTools();
            BuildGunRange(targetHitPoints);
            BuildMovementCourse();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Test areas rebuilt: Gun Range (F5), Movement Course (F6).");
        }

        // The rebuilt targets keep the hit points saved on the original practice lane.
        static float ReadOldTargetHitPoints(float fallback)
        {
            var old = GameObject.Find(OldLaneName + "/Target 5 meters");
            var health = old ? old.GetComponent("TargetHealth") : null;
            if (!health) return fallback;
            var hp = new SerializedObject(health).FindProperty("hitPoints");
            return hp != null ? hp.floatValue : fallback;
        }

        static void BuildTools()
        {
            var tools = new GameObject(ToolsName);
            tools.AddComponent<TestAreaControls>();
            tools.AddComponent<DamagePopups>();
            // Start of the original Test Area, where mobs can be spawned on the NavMesh.
            Marker(tools.transform, "TA-Start", "Mob test area", new Vector3(0f, FloorY, -8f), 0f);
        }

        // ---------------------------------------------------------------- Gun range
        static void BuildGunRange(float targetHitPoints)
        {
            var root = new GameObject(GunRangeName).transform;
            root.position = new Vector3(-60f, FloorY, -15f);
            Box(root, "Floor", new Vector3(0f, -0.5f, 19f), new Vector3(70f, 1f, 62f), floor);
            Label(root, "GUN RANGE", new Vector3(0f, 3.2f, -11f), 0.16f);
            Label(root, "F9: reset targets + refill ammo     F1 > Weapons: equip any gun", new Vector3(0f, 2.6f, -11f), 0.06f);

            // Firing line and the three practice targets (rebuilt Combat Practice Lane).
            Box(root, "Firing line", new Vector3(0f, 0.01f, 0f), new Vector3(10f, 0.02f, 0.08f), marker, false);
            Marker(root, "GR-Firing", "Firing line", new Vector3(0f, 0f, -1f), 0f);
            var lane = Group(root, "Practice targets", true);
            foreach (float distance in new[] { 5f, 15f, 30f })
                Target(lane, "Target " + distance + " m", new Vector3(0f, 0f, distance), targetHitPoints, distance + " m");
            Box(root, "Backstop", new Vector3(0f, 2f, 49f), new Vector3(30f, 4f, 0.5f), wall);

            // Low cover: standing muzzles clear it, crouched ones are blocked.
            Box(root, "Low cover", new Vector3(3f, 0.55f, 1.2f), new Vector3(1.6f, 1.1f, 0.4f), cover);
            Label(root, "LOW COVER", new Vector3(3f, 1.4f, 1.2f), 0.05f);
            Marker(root, "GR-Cover", "Behind low cover", new Vector3(3f, 0f, 0f), 0f);

            // Strafe lane parallel to the target row, 10 m with a tick every meter.
            for (int m = 0; m <= 10; m++)
                Box(root, "Strafe tick " + m, new Vector3(-12f + m, 0.01f, -4f), new Vector3(m % 5 == 0 ? 0.08f : 0.04f, 0.02f, m % 5 == 0 ? 1.2f : 0.6f), marker, false);
            Box(root, "Strafe lane", new Vector3(-7f, 0.01f, -4f), new Vector3(10f, 0.02f, 0.04f), marker, false);
            Label(root, "STRAFE LANE 10 m", new Vector3(-7f, 1.2f, -4.6f), 0.05f);
            Marker(root, "GR-Strafe", "Strafe lane (left end)", new Vector3(-12f, 0f, -4f), 0f);

            // Melee dummy within punching reach of its marker.
            var melee = Group(root, "Melee dummy", true);
            Target(melee, "Melee dummy", new Vector3(6.5f, 0f, -0.8f), 1000f, "MELEE");
            Marker(root, "GR-Melee", "Melee dummy", new Vector3(6.5f, 0f, -2f), 0f);

            // Moving targets, offset to the right so they never cross the static lane.
            var moving = Group(root, "Moving targets", true);
            var slow = Target(moving, "Moving target 2 m/s", new Vector3(10f, 0f, 12f), 1000f, "2 m/s");
            AddMover(slow, 8f, 2f);
            var fast = Target(moving, "Moving target 4 m/s", new Vector3(10f, 0f, 22f), 1000f, "4 m/s");
            AddMover(fast, 10f, 4f);

            // Stationary mobs: real mob prefabs with their AI and navigation switched off.
            var mobs = Group(root, "Stationary mobs", true);
            StationaryMob(mobs, "DwarfMob", new Vector3(-6f, 0f, 8f));
            StationaryMob(mobs, "ZombieMob", new Vector3(-9f, 0f, 10f));
            StationaryMob(mobs, "GiantMob", new Vector3(-14f, 0f, 13f));
            Label(root, "STATIONARY MOBS", new Vector3(-10f, 4.6f, 11f), 0.06f);

            // Rocket blast area: posts in an arc around the impact wall, each at a labeled distance.
            Box(root, "Blast wall", new Vector3(-24f, 1.5f, 46f), new Vector3(6f, 3f, 0.5f), wall);
            var posts = Group(root, "Blast posts", true);
            Vector3 impact = new Vector3(-24f, 0f, 45.75f);
            float[] radii = { 1f, 2f, 3f, 4f, 6f };
            float[] angles = { -60f, -30f, 0f, 30f, 60f };
            for (int i = 0; i < radii.Length; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, angles[i], 0f) * Vector3.back * radii[i];
                Post(posts, "Post " + radii[i] + " m", impact + offset, radii[i] + " m from wall");
            }
            Label(root, "ROCKET BLAST AREA", new Vector3(-24f, 3.6f, 46f), 0.08f);
            Marker(root, "GR-Rocket", "Rocket firing spot", new Vector3(-24f, 0f, 22f), 0f);
        }

        static Transform Target(Transform parent, string name, Vector3 local, float hitPoints, string caption)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = local;
            Box(root, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 1.2f, 0.35f), targetBody);
            Box(root, "Head", new Vector3(0f, 1.65f, 0f), new Vector3(0.4f, 0.4f, 0.4f), targetHead);
            AddHealth(root.gameObject, hitPoints);
            Label(root, caption, new Vector3(0f, 2.15f, 0f), 0.05f);
            Readout(root, new Vector3(0f, 2.45f, 0f));
            return root;
        }

        static void Post(Transform parent, string name, Vector3 local, string caption)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = local;
            Box(root, "Post", new Vector3(0f, 0.8f, 0f), new Vector3(0.4f, 1.6f, 0.4f), targetBody);
            AddHealth(root.gameObject, 1000f);
            Label(root, caption, new Vector3(0f, 1.9f, 0f), 0.04f);
            Readout(root, new Vector3(0f, 2.2f, 0f));
        }

        static void AddHealth(GameObject go, float hitPoints)
        {
            var type = System.Type.GetType("StarterGame.TargetHealth, Assembly-CSharp");
            var health = go.AddComponent(type);
            var so = new SerializedObject(health);
            so.FindProperty("hitPoints").floatValue = hitPoints;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddMover(Transform target, float length, float speed)
        {
            target.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            target.gameObject.AddComponent<MovingTarget>().Configure(length, speed);
        }

        static void StationaryMob(Transform parent, string prefabName, Vector3 local)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Mobs/Prefabs/" + prefabName + ".prefab");
            if (!prefab) { Debug.LogWarning("Missing mob prefab " + prefabName); return; }
            var mob = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            mob.name = prefabName + " (stationary)";
            mob.transform.localPosition = local;
            mob.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (string behaviour in new[] { "MobBrain", "MobAttackDriver" })
                if (mob.GetComponent(behaviour) is Behaviour b) b.enabled = false;
            if (mob.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.enabled = false;
            Readout(mob.transform, new Vector3(0f, 4.2f, 0f));
        }

        // ---------------------------------------------------------------- Movement course
        static void BuildMovementCourse()
        {
            var root = new GameObject(CourseName).transform;
            root.position = new Vector3(75f, FloorY, -15f);
            Box(root, "Floor", new Vector3(0f, -0.5f, 20f), new Vector3(100f, 1f, 64f), floor);
            Label(root, "MOVEMENT COURSE", new Vector3(0f, 3.2f, -11f), 0.16f);

            // 1. Straight lane: a line every meter, numbered every 5 m.
            var lane = Group(root, "Straight lane", false);
            for (int m = 0; m <= 40; m++)
            {
                bool major = m % 5 == 0;
                Box(lane, m + " m", new Vector3(-44f, 0.01f, m), new Vector3(4f, 0.02f, major ? 0.1f : 0.04f), marker, false);
                if (major) Label(lane, m + " m", new Vector3(-41.6f, 0.5f, m), 0.05f);
            }
            Label(lane, "STRAIGHT LANE 40 m", new Vector3(-44f, 1.6f, -1f), 0.06f);
            Marker(root, "MC-Lane", "Straight lane", new Vector3(-44f, 0f, 0f), 0f);

            // 2. Open area for turning, reversing and strafing.
            var open = Group(root, "Open area", false);
            Outline(open, new Vector3(-26f, 0f, 10f), 20f, 20f);
            Label(open, "OPEN AREA 20 x 20 m", new Vector3(-26f, 1.6f, -0.5f), 0.06f);
            Marker(root, "MC-Open", "Open area (centre)", new Vector3(-26f, 0f, 10f), 0f);

            // 3. Slopes of 20 and 40 degrees, both rising 2.5 m to a landing.
            Ramp(root, "Slope 20°", -7f, 20f, 2.5f);
            Ramp(root, "Slope 40°", 1f, 40f, 2.5f);
            Marker(root, "MC-Slope20", "Slope 20°", new Vector3(-7f, 0f, 0f), 0f);
            Marker(root, "MC-Slope40", "Slope 40°", new Vector3(1f, 0f, 0f), 0f);

            // 4. Steps: ten 0.25 m rises (the character steps up to 0.3 m without jumping).
            var steps = Group(root, "Steps 0.25 m", false);
            for (int i = 0; i < 10; i++)
            {
                float h = 0.25f * (i + 1);
                Box(steps, "Step " + (i + 1), new Vector3(10.5f, h * 0.5f, 2.2f + 0.4f * i), new Vector3(5f, h, 0.4f), slope);
            }
            Box(steps, "Top", new Vector3(10.5f, 1.25f, 7.5f), new Vector3(5f, 2.5f, 3f), slope);
            Label(steps, "STEPS 0.25 m", new Vector3(10.5f, 3.2f, 6f), 0.06f);
            Marker(root, "MC-Steps", "Steps", new Vector3(10.5f, 0f, 0f), 0f);

            // 5. Ledges of increasing height, each with room to land.
            var ledges = Group(root, "Ledges", false);
            float[] heights = { 0.5f, 1f, 1.5f, 2f, 2.5f };
            for (int i = 0; i < heights.Length; i++)
            {
                float z = 3f + i * 5f;
                Box(ledges, "Ledge " + heights[i] + " m", new Vector3(19.5f, heights[i] * 0.5f, z + 1f), new Vector3(5f, heights[i], 2f), platform);
                Label(ledges, heights[i] + " m", new Vector3(19.5f, heights[i] + 0.5f, z), 0.05f);
            }
            Label(ledges, "LEDGES 0.5-2.5 m", new Vector3(19.5f, 1.6f, -0.5f), 0.06f);
            Marker(root, "MC-Ledges", "Ledges", new Vector3(19.5f, 0f, 0f), 0f);

            // 6. Gaps of increasing width between 1 m high platforms.
            var gaps = Group(root, "Gaps", false);
            float z0 = 2f;
            float[] widths = { 1f, 2f, 3f, 4f, 5f };
            Box(gaps, "Platform 0", new Vector3(28f, 0.5f, z0 + 1.5f), new Vector3(4f, 1f, 3f), platform);
            z0 += 3f;
            foreach (float w in widths)
            {
                Label(gaps, "gap " + w + " m", new Vector3(28f, 1.6f, z0 + w * 0.5f), 0.05f);
                z0 += w;
                Box(gaps, "Platform after " + w + " m gap", new Vector3(28f, 0.5f, z0 + 1.5f), new Vector3(4f, 1f, 3f), platform);
                z0 += 3f;
            }
            Label(gaps, "GAPS 1-5 m", new Vector3(28f, 1.6f, -0.5f), 0.06f);
            Marker(root, "MC-Gaps", "Gaps", new Vector3(28f, 0f, 0f), 0f);

            // 7. Low tunnel: 1.5 m clear height (standing needs 2 m, crouching 1.05 m).
            var tunnel = Group(root, "Low tunnel", false);
            Box(tunnel, "Left wall", new Vector3(34.6f, 0.75f, 7f), new Vector3(0.4f, 1.5f, 8f), wall);
            Box(tunnel, "Right wall", new Vector3(37.4f, 0.75f, 7f), new Vector3(0.4f, 1.5f, 8f), wall);
            Box(tunnel, "Roof", new Vector3(36f, 1.7f, 7f), new Vector3(3.2f, 0.4f, 8f), wall);
            Label(tunnel, "LOW TUNNEL 1.5 m", new Vector3(36f, 2.4f, 3f), 0.06f);
            Marker(root, "MC-Tunnel", "Low tunnel", new Vector3(36f, 0f, 0f), 0f);

            // 8. Wall jump: two 8 m walls facing each other 3 m apart.
            var walls = Group(root, "Wall jump", false);
            Box(walls, "Wall A", new Vector3(42.25f, 4f, 7f), new Vector3(0.5f, 8f, 8f), wallJump);
            Box(walls, "Wall B", new Vector3(45.75f, 4f, 7f), new Vector3(0.5f, 8f, 8f), wallJump);
            Label(walls, "WALL JUMP (walls 3 m apart)", new Vector3(44f, 1.6f, 2f), 0.06f);
            Marker(root, "MC-Wall", "Wall jump", new Vector3(44f, 0f, 0f), 0f);
        }

        static void Ramp(Transform parent, string name, float x, float degrees, float rise)
        {
            var group = Group(parent, name, false);
            float a = degrees * Mathf.Deg2Rad;
            float run = rise / Mathf.Tan(a);
            float length = rise / Mathf.Sin(a);
            const float thickness = 0.4f, width = 5f, startZ = 2f;
            var rotation = Quaternion.Euler(-degrees, 0f, 0f);
            Vector3 topMiddle = new Vector3(x, rise * 0.5f, startZ + run * 0.5f);
            var slab = Box(group, "Ramp", topMiddle - rotation * Vector3.up * (thickness * 0.5f), new Vector3(width, thickness, length), slope);
            slab.localRotation = rotation;
            Box(group, "Landing", new Vector3(x, rise * 0.5f, startZ + run + 1.5f), new Vector3(width, rise, 3f), slope);
            Label(group, name.ToUpperInvariant(), new Vector3(x, rise + 0.9f, startZ + run + 1.5f), 0.06f);
        }

        static void Outline(Transform parent, Vector3 centre, float sizeX, float sizeZ)
        {
            Box(parent, "Edge N", centre + new Vector3(0f, 0.01f, sizeZ * 0.5f), new Vector3(sizeX, 0.02f, 0.08f), marker, false);
            Box(parent, "Edge S", centre + new Vector3(0f, 0.01f, -sizeZ * 0.5f), new Vector3(sizeX, 0.02f, 0.08f), marker, false);
            Box(parent, "Edge E", centre + new Vector3(sizeX * 0.5f, 0.01f, 0f), new Vector3(0.08f, 0.02f, sizeZ), marker, false);
            Box(parent, "Edge W", centre + new Vector3(-sizeX * 0.5f, 0.01f, 0f), new Vector3(0.08f, 0.02f, sizeZ), marker, false);
            Box(parent, "Centre cross X", centre + new Vector3(0f, 0.01f, 0f), new Vector3(1f, 0.02f, 0.08f), marker, false);
            Box(parent, "Centre cross Z", centre + new Vector3(0f, 0.01f, 0f), new Vector3(0.08f, 0.02f, 1f), marker, false);
        }

        // ---------------------------------------------------------------- Helpers
        static Transform Group(Transform parent, string name, bool resettable)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (resettable) go.AddComponent<ResettableGroup>();
            return go.transform;
        }

        static Transform Box(Transform parent, string name, Vector3 localCentre, Vector3 size, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        static void Marker(Transform parent, string id, string label, Vector3 localFloor, float yaw)
        {
            var go = new GameObject("Start " + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localFloor;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<TestStartMarker>().Configure(id, label);
            Box(go.transform, "Pad", new Vector3(0f, 0.015f, 0f), new Vector3(1f, 0.03f, 1f), start, false);
            Box(go.transform, "Facing", new Vector3(0f, 0.03f, 0.6f), new Vector3(0.12f, 0.03f, 0.5f), start, false);
            Label(go.transform, id, new Vector3(0f, 0.35f, -0.6f), 0.04f);
        }

        static void Label(Transform parent, string text, Vector3 local, float size)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            // Readable from the start markers, which look along +Z.
            go.transform.localRotation = Quaternion.identity;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = size * LabelScale;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.text = text;
            mesh.color = new Color(0.08f, 0.1f, 0.14f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        static void Readout(Transform parent, Vector3 local)
        {
            Label(parent, "", local, 0.045f);
            var label = parent.GetChild(parent.childCount - 1);
            label.name = "Health readout";
            label.GetComponent<TextMesh>().color = new Color(0.05f, 0.45f, 0.15f);
            label.gameObject.AddComponent<HealthReadout>();
        }

        static void LoadMaterials()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            floor = Load("Assets/Game/World/Materials/Ground.mat");
            platform = Load("Assets/Game/World/Materials/Platforms.mat");
            wall = Load("Assets/Game/World/Materials/Boundary.mat");
            wallJump = Load("Assets/Game/World/Materials/Parkour/Wall Route.mat");
            slope = Load("Assets/Game/World/Materials/Parkour/Balance.mat");
            start = Load("Assets/Game/World/Materials/Parkour/Start Finish.mat");
            // Dark so floor markings read clearly against the white ground.
            marker = GridMaterial("Marker", new Color(0.1f, 0.13f, 0.18f));
            targetBody = GridMaterial("Target Body", new Color(0.85f, 0.29f, 0.12f));
            targetHead = GridMaterial("Target Head", new Color(1f, 0.71f, 0.25f));
            cover = GridMaterial("Cover", new Color(0.2f, 0.3f, 0.37f));
        }

        static Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        // Test-area colours use the same grid shader so every surface shows 1 m cells.
        static Material GridMaterial(string name, Color colour)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Game/TestAreas", "Materials");
                material = new Material(floor.shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetColor("_LineColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
