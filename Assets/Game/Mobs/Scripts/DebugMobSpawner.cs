using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace StarterGame.Characters
{
    public sealed class DebugMobSpawner : MonoBehaviour
    {
        [Serializable]
        public struct Mob
        {
            public string label;
            public GameObject prefab;
        }
        [SerializeField] private Transform player;
        [SerializeField] private Mob[] mobs = Array.Empty<Mob>();
        [SerializeField, Min(3f)] private float spawnDistance = 8f;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private int selected;
        public int Count => mobs.Length;
        public int Selected => selected;
        public string SelectedName => Count > 0 ? mobs[selected].label : "No mobs configured";
        public string Status { get; private set; } = "Choose a mob, then Spawn.";
        public int AliveCount { get { spawned.RemoveAll(m => !m); return spawned.Count; } }
        public void Select(int index) { if (Count > 0) selected = (index % Count + Count) % Count; }

        public GameObject SpawnSelected()
        {
            if (!Application.isPlaying || !player || Count == 0 || !mobs[selected].prefab) return null;
            var prefab = mobs[selected].prefab;
            var agent = prefab.GetComponent<NavMeshAgent>();
            float radius = agent ? agent.radius : .5f;
            float height = agent ? agent.height : 2f;
            Vector3 playerFloor = player.position;
            var controller = player.GetComponent<CharacterController>();
            if (controller) playerFloor.y = controller.bounds.min.y;
            if (!NavMesh.SamplePosition(playerFloor, out var start, 3f, NavMesh.AllAreas))
            { Status = "Player is outside the walkable area."; return null; }
            var path = new NavMeshPath();
            // Try in front first, then around the player; never spawn inside a wall or another mob.
            for (int i = 0; i < 24; i++)
            {
                float angle = (i % 8) * 45f;
                float distance = spawnDistance + (i / 8) * 2f;
                Vector3 desired = start.position + Quaternion.Euler(0f, angle, 0f) * player.forward * distance;
                if (!NavMesh.SamplePosition(desired, out var point, 1.5f, NavMesh.AllAreas) ||
                    Vector3.Distance(start.position, point.position) < 3f ||
                    !NavMesh.CalculatePath(start.position, point.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                Vector3 bottom = point.position + Vector3.up * (radius + .12f);
                Vector3 top = point.position + Vector3.up * Mathf.Max(radius + .12f, height - radius);
                if (Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore)) continue;
                var mob = Instantiate(prefab, point.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(player.position - point.position, Vector3.up)));
                mob.name = mobs[selected].label;
                mob.GetComponent<SpiderDroneBrain>().SetTarget(player);
                spawned.Add(mob);
                Status = "Spawned " + SelectedName + ".";
                return mob;
            }
            Status = "No clear, reachable spawn spot. Move to open ground and try again.";
            return null;
        }

        public void ClearSpawned()
        {
            foreach (var mob in spawned) if (mob) Destroy(mob);
            spawned.Clear();
            Status = "Cleared spawned mobs.";
        }

        public void DrawControls()
        {
            GUILayout.Label("SPAWN MOBS  |  Alive: " + AliveCount);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(35))) Select(selected - 1);
            GUILayout.Label(SelectedName, GUILayout.MinWidth(120));
            if (GUILayout.Button(">", GUILayout.Width(35))) Select(selected + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn " + SelectedName, GUILayout.Height(32))) SpawnSelected();
            if (GUILayout.Button("Clear mobs", GUILayout.Height(32))) ClearSpawned();
            GUILayout.EndHorizontal();
            GUILayout.Label(Status, new GUIStyle(GUI.skin.label) { wordWrap = true });
        }
    }
}
