using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>A non-interactive attraction disk and outline in the gameplay overlay.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GuideRangeGraphic : Graphic
    {
        private const int Segments = 96;
        private bool dashed;

        public void SetStyle(Color tint, bool isPreview)
        {
            color = tint;
            if (dashed == isPreview) return;
            dashed = isPreview;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * .5f;
            if (radius <= 0f) return;
            var center = rect.center;
            var fill = color;
            fill.a *= dashed ? .09f : .065f;
            mesh.AddVert(center, fill, Vector2.zero);
            for (var i = 0; i <= Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    fill, Vector2.zero);
                if (i > 0) mesh.AddTriangle(0, i, i + 1);
            }
            var inner = Mathf.Max(0f, radius - 2.4f);
            for (var i = 0; i < Segments; i++)
            {
                if (dashed && i % 6 >= 4) continue;
                var a = i * Mathf.PI * 2f / Segments;
                var b = (i + 1) * Mathf.PI * 2f / Segments;
                var da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                var index = mesh.currentVertCount;
                mesh.AddVert(center + da * inner, color, Vector2.zero);
                mesh.AddVert(center + da * radius, color, Vector2.zero);
                mesh.AddVert(center + db * radius, color, Vector2.zero);
                mesh.AddVert(center + db * inner, color, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
                mesh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
