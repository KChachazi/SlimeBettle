using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Small mesh icons; replace this Graphic with production art without changing UI logic.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FirstLevelUiIcon : MaskableGraphic
    {
        public enum IconKind { Slime, Food, Furnace, Obstacle, Pause, Cancel }
        [SerializeField] private IconKind icon;
        public IconKind Icon { get => icon; set { icon = value; SetVerticesDirty(); } }
        private static readonly Color Ink = new Color(0.16f, 0.26f, 0.20f);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (icon)
            {
                case IconKind.Slime:
                    Oval(mesh, 50, 46, 43, 35, new Color(0.55f, 0.78f, 0.46f));
                    Oval(mesh, 50, 60, 34, 29, new Color(0.65f, 0.86f, 0.54f));
                    Oval(mesh, 35, 52, 3, 5, Ink); Oval(mesh, 65, 52, 3, 5, Ink);
                    Box(mesh, 45, 37, 10, 3, Ink);
                    Oval(mesh, 25, 39, 7, 3, new Color(0.92f, 0.64f, 0.47f));
                    Oval(mesh, 75, 39, 7, 3, new Color(0.92f, 0.64f, 0.47f));
                    break;
                case IconKind.Food:
                    Oval(mesh, 50, 40, 40, 22, new Color(0.39f, 0.55f, 0.33f));
                    Oval(mesh, 50, 50, 37, 16, new Color(0.91f, 0.76f, 0.43f));
                    Oval(mesh, 37, 57, 12, 12, new Color(0.96f, 0.85f, 0.56f));
                    Oval(mesh, 62, 59, 13, 11, new Color(0.96f, 0.85f, 0.56f));
                    Box(mesh, 48, 63, 4, 15, Ink);
                    Oval(mesh, 40, 76, 11, 5, new Color(0.39f, 0.63f, 0.31f));
                    Oval(mesh, 60, 80, 11, 5, new Color(0.39f, 0.63f, 0.31f));
                    break;
                case IconKind.Furnace:
                    Box(mesh, 65, 66, 16, 25, new Color(0.49f, 0.39f, 0.29f));
                    Box(mesh, 18, 15, 68, 55, new Color(0.69f, 0.45f, 0.28f));
                    Box(mesh, 27, 21, 49, 38, Ink);
                    Oval(mesh, 51, 36, 16, 20, new Color(0.96f, 0.56f, 0.24f));
                    Oval(mesh, 51, 31, 8, 12, new Color(1f, 0.83f, 0.40f));
                    Box(mesh, 12, 11, 79, 7, new Color(0.49f, 0.39f, 0.29f));
                    break;
                case IconKind.Obstacle:
                    Box(mesh, 18, 18, 12, 65, new Color(0.45f, 0.32f, 0.21f));
                    Box(mesh, 70, 18, 12, 65, new Color(0.45f, 0.32f, 0.21f));
                    Box(mesh, 10, 55, 80, 18, new Color(0.71f, 0.55f, 0.34f));
                    Box(mesh, 10, 28, 80, 18, new Color(0.77f, 0.62f, 0.40f));
                    Oval(mesh, 24, 64, 3, 3, Ink); Oval(mesh, 76, 64, 3, 3, Ink);
                    break;
                case IconKind.Pause:
                    Box(mesh, 26, 18, 16, 64, Ink); Box(mesh, 58, 18, 16, 64, Ink);
                    break;
                case IconKind.Cancel:
                    Box(mesh, 20, 45, 60, 10, Ink, 45); Box(mesh, 20, 45, 60, 10, Ink, -45);
                    break;
            }
        }
        private Vector2 Point(float x, float y)
        {
            var r = GetPixelAdjustedRect();
            return new Vector2(r.xMin + r.width * x / 100f, r.yMin + r.height * y / 100f);
        }
        private void Box(VertexHelper mesh, float x, float y, float w, float h, Color tint, float angle = 0)
        {
            var start = mesh.currentVertCount;
            var center = new Vector2(x + w / 2, y + h / 2);
            foreach (var p in new[] { new Vector2(x,y), new Vector2(x,y+h), new Vector2(x+w,y+h), new Vector2(x+w,y) })
            {
                var rotated = (Vector2)(Quaternion.Euler(0,0,angle) * (p - center)) + center;
                mesh.AddVert(Point(rotated.x, rotated.y), tint * color, Vector2.zero);
            }
            mesh.AddTriangle(start, start+1, start+2); mesh.AddTriangle(start, start+2, start+3);
        }
        private void Oval(VertexHelper mesh, float x, float y, float rx, float ry, Color tint)
        {
            const int segments = 24;
            var start = mesh.currentVertCount;
            mesh.AddVert(Point(x,y), tint * color, Vector2.zero);
            for (var i=0; i<=segments; i++)
            {
                var angle = 2f * Mathf.PI * i / segments;
                mesh.AddVert(Point(x + Mathf.Cos(angle)*rx, y + Mathf.Sin(angle)*ry), tint * color, Vector2.zero);
                if (i>0) { mesh.AddTriangle(start, start+i, start+i+1); }
            }
        }
    }
}
