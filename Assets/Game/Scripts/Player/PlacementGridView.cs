using System;
using System.Collections.Generic;
using SlimeBattle.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Player.Placement
{
    /// <summary>Independent world-space grid overlay. Owns only visual sprites, no mechanism objects.</summary>
    public sealed class PlacementGridView : MonoBehaviour
    {
        [SerializeField] private Color gridColor = new Color(0.35f, 0.7f, 0.8f, 0.35f);
        [SerializeField] private Color freeColor = new Color(0.2f, 0.95f, 0.55f, 0.7f);
        [SerializeField] private Color occupiedColor = new Color(1f, 0.35f, 0.25f, 0.75f);
        [SerializeField] private Color outsideColor = new Color(0.8f, 0.35f, 1f, 0.75f);
        [SerializeField, Min(0.01f)] private float lineWidth = 0.08f;
        private PlacementGridAuthoring grid;
        private PlacementSystem placement;
        private GameObject visuals;
        private Sprite sprite;
        private SpriteRenderer previewRenderer;
        private readonly List<SpriteRenderer> lines = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> occupiedCells = new List<SpriteRenderer>();
        private readonly List<AnchorVisual> anchorVisuals = new List<AnchorVisual>();
        private sealed class AnchorVisual
        {
            public GameObject Root;
            public SpriteRenderer[] Edges;
            public TextMesh Label;
        }
        public bool IsVisible => visuals != null && visuals.activeInHierarchy;
        public int VisibleAnchorCount => IsVisible ? placement.GetAnchors().Count : 0;
        public Color PreviewColor => previewRenderer != null ? previewRenderer.color : Color.clear;

        public void Initialize(PlacementSystem placement, PlacementGridAuthoring grid)
        {
            Unbind();
            this.placement = placement ?? throw new ArgumentNullException(nameof(placement));
            this.grid = grid != null ? grid : throw new ArgumentNullException(nameof(grid));
            if (visuals == null)
            {
                visuals = new GameObject("PlacementGrid_Visuals");
                // Keep world-space dimensions independent of the view owner's UI/scene scale.
                SceneManager.MoveGameObjectToScene(visuals, gameObject.scene);
                sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
            }
            if (isActiveAndEnabled) { Bind(); }
            Render();
        }

        private void Bind() { placement.Changed += Render; Render(); }
        private void Unbind()
        {
            if (placement != null) { placement.Changed -= Render; }
            if (visuals != null) { visuals.SetActive(false); }
            DestroyPreview();
        }
        private void OnEnable() { if (placement != null) { Bind(); } }
        private void OnDisable() { Unbind(); }

        private void Render()
        {
            if (visuals == null) { return; }
            var visible = isActiveAndEnabled && placement.IsPlacing && placement.IsRunning;
            visuals.SetActive(visible);
            if (!visible) { DestroyPreview(); return; }
            foreach (var line in lines) { line.gameObject.SetActive(false); }
            var bounds = placement.GetMapBounds();
            var lineIndex = 0;
            if (bounds.HasValue)
            {
                grid.GetCoveredRange(bounds.Value, out var min, out var max);
                // Rendering budget only; very dense maps show the region around the current preview.
                var centerCell = placement.GetCurrentPreview().Cell;
                var startX = Mathf.Clamp(centerCell.x - 128, min.x, max.x);
                var startY = Mathf.Clamp(centerCell.y - 128, min.y, max.y);
                var endX = Mathf.Min(max.x + 1, startX + 256);
                var endY = Mathf.Min(max.y + 1, startY + 256);
                for (var x = startX; x <= endX; x++)
                { DrawLine(grid.GetCellCorner(new Vector3Int(x, startY, 0)), grid.GetCellCorner(new Vector3Int(x, endY, 0)), bounds.Value, ref lineIndex); }
                for (var y = startY; y <= endY; y++)
                { DrawLine(grid.GetCellCorner(new Vector3Int(startX, y, 0)), grid.GetCellCorner(new Vector3Int(endX, y, 0)), bounds.Value, ref lineIndex); }
            }
            foreach (var cell in occupiedCells) { cell.gameObject.SetActive(false); }
            var occupiedIndex = 0;
            foreach (var cell in placement.GetOccupiedCells())
            {
                if (occupiedIndex >= 512) { break; }
                if (occupiedIndex == occupiedCells.Count) { occupiedCells.Add(MakeSprite("OccupiedCell")); }
                var renderer = occupiedCells[occupiedIndex++];
                SetCell(renderer, cell);
                renderer.color = new Color(occupiedColor.r, occupiedColor.g, occupiedColor.b, 0.2f);
            }
            DrawAnchors();
            var preview = placement.GetCurrentPreview();
            if (!preview.HasPosition) { DestroyPreview(); return; }
            if (previewRenderer == null)
            {
                previewRenderer = MakeSprite("PlacementCellPreview");
                previewRenderer.sortingOrder = 102;
            }
            SetCell(previewRenderer, preview.Cell);
            if (preview.IsAnchor) { previewRenderer.transform.position = preview.WorldCenter; }
            previewRenderer.color = preview.CanConfirm ? freeColor :
                preview.Status == PlacementValidationStatus.OutOfBounds ||
                    preview.Status == PlacementValidationStatus.InvalidArea ? outsideColor :
                preview.Status == PlacementValidationStatus.Occupied ? occupiedColor : new Color(1f, 0.75f, 0.2f, 0.75f);
        }

        private void DrawAnchors()
        {
            foreach (var visual in anchorVisuals) { visual.Root.SetActive(false); }
            var index = 0;
            foreach (var anchor in placement.GetAnchors())
            {
                if (index == anchorVisuals.Count)
                {
                    var visual = new AnchorVisual { Root = new GameObject("PlacementAnchor"), Edges = new SpriteRenderer[4] };
                    visual.Root.transform.SetParent(visuals.transform, false);
                    for (var edge = 0; edge < 4; edge++)
                    {
                        visual.Edges[edge] = MakeSprite("AnchorEdge");
                        visual.Edges[edge].transform.SetParent(visual.Root.transform, false);
                        visual.Edges[edge].sortingOrder = 103;
                    }
                    var label = new GameObject("AnchorId", typeof(TextMesh)); label.transform.SetParent(visual.Root.transform, false);
                    visual.Label = label.GetComponent<TextMesh>();
                    visual.Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    visual.Label.fontSize = 48; visual.Label.characterSize = 0.075f;
                    visual.Label.anchor = TextAnchor.LowerCenter; visual.Label.alignment = TextAlignment.Center;
                    var renderer = label.GetComponent<MeshRenderer>(); renderer.sharedMaterial = visual.Label.font.material;
                    renderer.sortingOrder = 104;
                    anchorVisuals.Add(visual);
                }
                var item = anchorVisuals[index++]; item.Root.SetActive(true); item.Root.name = "PlacementAnchor_" + anchor.AnchorId.Value;
                item.Root.transform.position = new Vector3(anchor.Position.x, anchor.Position.y, grid.GetCellCenter(default).z);
                var status = placement.GetAnchorStatus(anchor);
                var color = status == PlacementValidationStatus.AnchorFree ? new Color(0.15f, 0.9f, 1f, 0.95f) :
                    status == PlacementValidationStatus.TypeMismatch || status == PlacementValidationStatus.ReplacementDenied ? occupiedColor :
                    new Color(1f, 0.75f, 0.2f, 0.95f);
                const float side = 0.68f;
                for (var edge = 0; edge < 4; edge++)
                {
                    var renderer = item.Edges[edge]; renderer.color = color;
                    var horizontal = edge < 2;
                    renderer.transform.localPosition = horizontal ? new Vector3(0f, (edge == 0 ? -1f : 1f) * side / 2f) :
                        new Vector3((edge == 2 ? -1f : 1f) * side / 2f, 0f);
                    renderer.transform.localScale = horizontal ? new Vector3(side, 0.045f, 1f) : new Vector3(0.045f, side, 1f);
                }
                item.Label.text = anchor.AnchorId.Value; item.Label.color = color;
                item.Label.transform.localPosition = new Vector3(0f, side / 2f + 0.06f, 0f);
            }
        }

        private void DrawLine(Vector3 a, Vector3 b, MapBounds bounds, ref int index)
        {
            var delta = b - a;
            var low = 0f;
            var high = 1f;
            if (!Clip(-delta.x, a.x - bounds.Min.x, ref low, ref high) ||
                !Clip(delta.x, bounds.Max.x - a.x, ref low, ref high) ||
                !Clip(-delta.y, a.y - bounds.Min.y, ref low, ref high) ||
                !Clip(delta.y, bounds.Max.y - a.y, ref low, ref high)) { return; }
            b = a + delta * high;
            a += delta * low;
            if (index == lines.Count) { lines.Add(MakeSprite("GridLine")); }
            var renderer = lines[index++];
            renderer.gameObject.SetActive(true);
            renderer.transform.position = (a + b) * 0.5f;
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            renderer.transform.localScale = new Vector3(Vector3.Distance(a, b), lineWidth, 1f);
            renderer.color = gridColor;
        }

        private static bool Clip(float p, float q, ref float low, ref float high)
        {
            if (Mathf.Abs(p) < 0.000001f) { return q >= 0f; }
            var t = q / p;
            if (p < 0f) { if (t > high) { return false; } low = Mathf.Max(low, t); }
            else { if (t < low) { return false; } high = Mathf.Min(high, t); }
            return true;
        }

        private void SetCell(SpriteRenderer renderer, Vector3Int cell)
        {
            renderer.gameObject.SetActive(true);
            renderer.transform.position = grid.GetCellCenter(cell);
            renderer.transform.rotation = grid.transform.rotation;
            var size = grid.Grid.cellSize;
            var scale = grid.transform.lossyScale;
            renderer.transform.localScale = new Vector3(size.x * scale.x * 0.92f, size.y * scale.y * 0.92f, 1f);
        }

        private SpriteRenderer MakeSprite(string name)
        {
            var item = new GameObject(name, typeof(SpriteRenderer));
            item.transform.SetParent(visuals.transform, false);
            var renderer = item.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 100;
            return renderer;
        }
        private void DestroyPreview()
        {
            if (previewRenderer == null) { return; }
            if (Application.isPlaying) { Destroy(previewRenderer.gameObject); }
            else { DestroyImmediate(previewRenderer.gameObject); }
            previewRenderer = null;
        }
        private void OnDestroy()
        {
            Unbind();
            if (Application.isPlaying)
            {
                if (visuals != null) { Destroy(visuals); }
                if (sprite != null) { Destroy(sprite); }
            }
            else
            {
                if (visuals != null) { DestroyImmediate(visuals); }
                if (sprite != null) { DestroyImmediate(sprite); }
            }
        }
    }
}
