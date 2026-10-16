using UnityEngine;
using UnityEngine.UI;

namespace StarterGame
{
    /// <summary>Square stamina fill with a continuous vertical yellow gradient and thin outline.</summary>
    [AddComponentMenu("UI/Stamina Graphic")]
    public sealed class RoundedStaminaGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float fillAmount = 1f;
        [SerializeField, Min(0f)] private float strokeWidth = 2f;
        [SerializeField] private Color lightYellow = new Color(1f, 0.95f, 0.54f);
        [SerializeField] private Color mediumYellow = new Color(0.97f, 0.80f, 0.20f);
        [SerializeField] private Color darkYellow = new Color(0.85f, 0.65f, 0.11f);
        [SerializeField] private Color strokeColor = new Color(0.29f, 0.22f, 0.08f);

        public float FillAmount
        {
            get => fillAmount;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(fillAmount, value)) return;
                fillAmount = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect outer = GetPixelAdjustedRect();
            if (outer.width <= 0f || outer.height <= 0f) return;
            float stroke = Mathf.Min(strokeWidth, Mathf.Min(outer.width, outer.height) * 0.45f);
            Rect inner = Rect.MinMaxRect(outer.xMin + stroke, outer.yMin + stroke,
                outer.xMax - stroke, outer.yMax - stroke);
            if (stroke > 0f)
            {
                Color outline = strokeColor * color;
                AddQuad(mesh, Rect.MinMaxRect(outer.xMin, outer.yMin, outer.xMax, inner.yMin), outline, outline);
                AddQuad(mesh, Rect.MinMaxRect(outer.xMin, inner.yMax, outer.xMax, outer.yMax), outline, outline);
                AddQuad(mesh, Rect.MinMaxRect(outer.xMin, inner.yMin, inner.xMin, inner.yMax), outline, outline);
                AddQuad(mesh, Rect.MinMaxRect(inner.xMax, inner.yMin, outer.xMax, inner.yMax), outline, outline);
            }
            if (fillAmount <= 0f) return;
            inner.width *= fillAmount;
            float middle = inner.center.y;
            // Shared middle color makes both halves meet continuously without a color step.
            AddQuad(mesh, Rect.MinMaxRect(inner.xMin, inner.yMin, inner.xMax, middle),
                darkYellow * color, mediumYellow * color);
            AddQuad(mesh, Rect.MinMaxRect(inner.xMin, middle, inner.xMax, inner.yMax),
                mediumYellow * color, lightYellow * color);
        }

        private static void AddQuad(VertexHelper mesh, Rect rect, Color bottom, Color top)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), bottom, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), top, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), top, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), bottom, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 3, first);
        }
    }
}
