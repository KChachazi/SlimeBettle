using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Small paper dialogue box with a downward tail, drawn without texture assets.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SlimeSpeechBubbleGraphic : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            Draw(mesh, rect, color, 7f);
            rect.xMin += 1.8f; rect.xMax -= 1.8f;
            rect.yMin += 1.8f; rect.yMax -= 1.8f;
            Draw(mesh, rect, new Color(1f, .98f, .87f, .97f), 6f);
        }

        private static void Draw(VertexHelper mesh, Rect rect, Color tint, float tail)
        {
            const float corner = 5f;
            var cx = rect.center.x;
            var points = new[] {
                new Vector2(rect.xMin + corner, rect.yMin),
                new Vector2(cx - 5f, rect.yMin), new Vector2(cx, rect.yMin - tail),
                new Vector2(cx + 5f, rect.yMin), new Vector2(rect.xMax - corner, rect.yMin),
                new Vector2(rect.xMax, rect.yMin + corner), new Vector2(rect.xMax, rect.yMax - corner),
                new Vector2(rect.xMax - corner, rect.yMax), new Vector2(rect.xMin + corner, rect.yMax),
                new Vector2(rect.xMin, rect.yMax - corner), new Vector2(rect.xMin, rect.yMin + corner)
            };
            var start = mesh.currentVertCount;
            mesh.AddVert(rect.center, tint, Vector2.zero);
            foreach (var point in points) mesh.AddVert(point, tint, Vector2.zero);
            for (var i = 0; i < points.Length; i++)
                mesh.AddTriangle(start, start + i + 1, start + (i + 1) % points.Length + 1);
        }
    }
}
