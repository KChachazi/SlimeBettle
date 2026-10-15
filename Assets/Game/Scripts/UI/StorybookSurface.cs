using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Resolution-independent paper/wood skin. Its hit region remains the RectTransform.</summary>
    public sealed class StorybookSurface : Image
    {
        public enum SurfaceKind { Paper, Wood, Inset }
        [SerializeField] private SurfaceKind surface;
        public SurfaceKind Surface { get => surface; set { surface = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var ink = new Color(0.29f, 0.23f, 0.15f, color.a);
            var shadow = new Rect(rect.x + 1, rect.y - 5, rect.width, rect.height);
            Polygon(mesh, shadow, new Color(0.16f, 0.20f, 0.10f, 0.30f * color.a), false);
            Polygon(mesh, rect, ink, false);
            var inner = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
            Polygon(mesh, inner, color, true);
            var edge = surface == SurfaceKind.Wood
                ? new Color(0.98f, 0.82f, 0.56f, 0.22f * color.a)
                : new Color(0.63f, 0.48f, 0.27f, 0.24f * color.a);
            var inset = surface == SurfaceKind.Inset ? 5f : 8f;
            Line(mesh, new Vector2(rect.xMin + inset, rect.yMax - inset), new Vector2(rect.xMax - inset, rect.yMax - inset - 1), 1, edge);
            Line(mesh, new Vector2(rect.xMin + inset, rect.yMin + inset), new Vector2(rect.xMax - inset, rect.yMin + inset + 1), 1, edge);
            // Fixed deterministic marks give a quiet paper grain without a tiled bitmap.
            for (var i = 0; i < 22; i++)
            {
                var u = Mathf.Repeat(i * 0.618034f + 0.13f, 1f);
                var v = Mathf.Repeat(i * 0.381966f + 0.27f, 1f);
                var a = new Vector2(Mathf.Lerp(rect.xMin + 12, rect.xMax - 12, u), Mathf.Lerp(rect.yMin + 12, rect.yMax - 12, v));
                var grain = new Color(edge.r, edge.g, edge.b, (surface == SurfaceKind.Wood ? 0.16f : 0.075f) * color.a);
                var length = surface == SurfaceKind.Wood ? Mathf.Min(rect.width * 0.30f, 70) : 3;
                Line(mesh, a, new Vector2(Mathf.Min(rect.xMax-9,a.x+length),a.y+0.7f), 0.7f, grain);
            }
        }

        private void Polygon(VertexHelper mesh, Rect r, Color fill, bool gradient)
        {
            var start = mesh.currentVertCount;
            var points = new[] {
                new Vector2(r.xMin+6,r.yMin+1),new Vector2(r.center.x,r.yMin),new Vector2(r.xMax-6,r.yMin+2),
                new Vector2(r.xMax,r.yMin+8),new Vector2(r.xMax-1,r.yMax-7),new Vector2(r.xMax-7,r.yMax),
                new Vector2(r.center.x,r.yMax-1),new Vector2(r.xMin+5,r.yMax),new Vector2(r.xMin,r.yMax-7),new Vector2(r.xMin+1,r.yMin+8)
            };
            mesh.AddVert(r.center, fill, Vector2.zero);
            foreach (var point in points)
            {
                var shade = gradient ? Mathf.Lerp(0.94f, 1.02f, Mathf.InverseLerp(r.yMin, r.yMax, point.y)) : 1f;
                mesh.AddVert(point, new Color(fill.r * shade, fill.g * shade, fill.b * shade, fill.a), Vector2.zero);
            }
            for (var i = 0; i < points.Length; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
        }
        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var start = mesh.currentVertCount;
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * width * 0.5f;
            mesh.AddVert(a-n,tint,Vector2.zero); mesh.AddVert(a+n,tint,Vector2.zero);
            mesh.AddVert(b+n,tint,Vector2.zero); mesh.AddVert(b-n,tint,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
