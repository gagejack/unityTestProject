using System.Collections.Generic;
using UnityEngine;

namespace StarterGame
{
    /// <summary>Rounds the visible blocks after the locomotion script poses them.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(200)]
    public sealed class PlayerBlockRounding : MonoBehaviour
    {
        [Tooltip("Radius in player-body meters. Zero restores sharp corners. Clamped to half the smallest dimension of each block.")]
        [SerializeField, Range(0f, 0.12f)] private float edgeRadius = 0.04f;
        [Tooltip("Segments across each rounded edge. Four is enough for this small character.")]
        [SerializeField, Range(2, 8)] private int cornerSegments = 4;
        [SerializeField, HideInInspector] private Mesh sourceMesh;
        public float EdgeRadius => edgeRadius;

        private sealed class Block
        {
            public MeshFilter filter;
            public Mesh mesh;
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector3 dimensions;
            public float radius = -1f;
            public int segments;
        }

        private readonly List<Block> blocks = new List<Block>();

        private void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += RefreshInEditor;
#endif
            Transform body = transform.Find("Block Body");
            if (!body) return;
            foreach (MeshFilter filter in body.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!sourceMesh && filter.sharedMesh) sourceMesh = filter.sharedMesh;
                blocks.Add(new Block { filter = filter });
            }
            Refresh();
        }

        private void LateUpdate() => Refresh();

#if UNITY_EDITOR
        private void RefreshInEditor()
        {
            if (!Application.isPlaying) Refresh();
        }
#endif

        private void OnValidate()
        {
            edgeRadius = Mathf.Clamp(edgeRadius, 0f, 0.12f);
            cornerSegments = Mathf.Clamp(cornerSegments, 2, 8);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void Refresh()
        {
            foreach (Block block in blocks)
            {
                if (!block.filter) continue;
                if (edgeRadius <= 0f)
                {
                    if (block.filter.sharedMesh != sourceMesh) block.filter.sharedMesh = sourceMesh;
                    block.radius = -1f;
                    continue;
                }
                Vector3 scale = block.filter.transform.lossyScale;
                Vector3 dimensions = new Vector3(Mathf.Max(Mathf.Abs(scale.x), 0.001f),
                    Mathf.Max(Mathf.Abs(scale.y), 0.001f), Mathf.Max(Mathf.Abs(scale.z), 0.001f));
                float radius = Mathf.Min(edgeRadius, Mathf.Min(dimensions.x, Mathf.Min(dimensions.y, dimensions.z)) * 0.499f);
                if (!block.mesh || block.segments != cornerSegments) CreateMesh(block);
                if (block.dimensions != dimensions || !Mathf.Approximately(block.radius, radius))
                    ShapeMesh(block, dimensions, radius);
                if (block.filter.sharedMesh != block.mesh) block.filter.sharedMesh = block.mesh;
            }
        }

        private void CreateMesh(Block block)
        {
            ReleaseMesh(block.mesh);
            block.mesh = new Mesh { name = block.filter.name + " Rounded Block", hideFlags = HideFlags.HideAndDontSave };
            block.mesh.MarkDynamic();
            block.segments = cornerSegments;
            int side = cornerSegments * 2 + 2;
            int faceVertices = side * side;
            block.vertices = new Vector3[6 * faceVertices];
            block.normals = new Vector3[block.vertices.Length];
            int[] triangles = new int[6 * (side - 1) * (side - 1) * 6];
            int index = 0;
            for (int face = 0; face < 6; face++)
            for (int y = 0; y < side - 1; y++)
            for (int x = 0; x < side - 1; x++)
            {
                int a = face * faceVertices + y * side + x;
                int b = a + 1;
                int c = a + side;
                int d = c + 1;
                bool positive = face % 2 == 0;
                triangles[index++] = a;
                triangles[index++] = positive ? b : c;
                triangles[index++] = positive ? c : b;
                triangles[index++] = b;
                triangles[index++] = positive ? d : c;
                triangles[index++] = positive ? c : d;
            }
            // Allocate vertices before assigning triangle indices.
            block.mesh.vertices = block.vertices;
            block.mesh.triangles = triangles;
            block.radius = -1f;
        }

        private void ShapeMesh(Block block, Vector3 dimensions, float radius)
        {
            int side = block.segments * 2 + 2;
            Vector3 half = dimensions * 0.5f;
            Vector3 inner = half - Vector3.one * radius;
            int index = 0;
            for (int face = 0; face < 6; face++)
            {
                int axis = face / 2;
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;
                float sign = face % 2 == 0 ? 1f : -1f;
                for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    Vector3 point = Vector3.zero;
                    point[axis] = half[axis] * sign;
                    point[u] = EdgeCoordinate(x, block.segments, half[u], radius);
                    point[v] = EdgeCoordinate(y, block.segments, half[v], radius);
                    Vector3 core = new Vector3(Mathf.Clamp(point.x, -inner.x, inner.x),
                        Mathf.Clamp(point.y, -inner.y, inner.y), Mathf.Clamp(point.z, -inner.z, inner.z));
                    Vector3 normal = (point - core).normalized;
                    Vector3 rounded = core + normal * radius;
                    // Store normalized coordinates so the existing limb scales still control size.
                    block.vertices[index] = new Vector3(rounded.x / dimensions.x,
                        rounded.y / dimensions.y, rounded.z / dimensions.z);
                    // Counteract the object's nonuniform scale when Unity transforms the normals.
                    block.normals[index++] = Vector3.Scale(normal, dimensions).normalized;
                }
            }
            block.mesh.vertices = block.vertices;
            block.mesh.normals = block.normals;
            block.mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            block.dimensions = dimensions;
            block.radius = radius;
        }

        private static float EdgeCoordinate(int index, int segments, float half, float radius)
        {
            return index <= segments
                ? -half + radius * index / segments
                : half - radius + radius * (index - segments - 1) / segments;
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= RefreshInEditor;
#endif
            foreach (Block block in blocks)
            {
                if (block.filter && block.filter.sharedMesh == block.mesh)
                    block.filter.sharedMesh = sourceMesh;
                ReleaseMesh(block.mesh);
            }
            blocks.Clear();
        }

        private static void ReleaseMesh(Mesh mesh)
        {
            if (!mesh) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
