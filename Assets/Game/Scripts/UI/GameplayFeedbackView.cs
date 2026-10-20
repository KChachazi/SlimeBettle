using System.Collections.Generic;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Placement;
using SlimeBattle.Slimes;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Read-only, pooled range and state feedback. Gameplay remains owned by the context.</summary>
    public sealed class GameplayFeedbackView : MonoBehaviour
    {
        private static readonly Color FoodColor = new Color(.20f, .64f, .40f, .78f);
        private static readonly Color FurnaceColor = new Color(.94f, .49f, .15f, .78f);
        private readonly List<GuideRangeGraphic> ranges = new List<GuideRangeGraphic>();
        private readonly List<Bubble> bubbles = new List<Bubble>();
        private readonly List<Rect> bubbleBounds = new List<Rect>();
        private LevelContext context;
        private Camera worldCamera;
        private PlacementSystem placement;
        private Font font;
        private RectTransform canvasRect;
        private RectTransform rangeLayer;
        private RectTransform bubbleLayer;
        private IReadOnlyList<GuideSourceSnapshot> sources;
        private float nextSourceRefresh;

        private sealed class Bubble
        {
            public SlimeSpeechBubbleGraphic Frame;
            public Text Label;
            public Image Leader;
        }

        public int VisibleRangeCount { get; private set; }
        public int VisibleBubbleCount { get; private set; }
        public bool IsVisible => canvasRect != null && canvasRect.gameObject.activeInHierarchy;

        public void Initialize(LevelContext owner, Camera camera, PlacementSystem placementSystem, Font bodyFont)
        {
            Unbind();
            context = owner;
            worldCamera = camera;
            placement = placementSystem;
            font = bodyFont;
            nextSourceRefresh = 0f;
            if (canvasRect == null)
            {
                var root = new GameObject("GameplayFeedbackCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                root.transform.SetParent(transform, false);
                canvasRect = (RectTransform)root.transform;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 19;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                rangeLayer = Layer("GuideRanges", canvasRect);
                bubbleLayer = Layer("SlimeStateBubbles", canvasRect);
            }
            if (placement != null) placement.Changed += InvalidateSources;
            Render();
        }

        public void Unbind()
        {
            if (placement != null) placement.Changed -= InvalidateSources;
            placement = null; context = null; sources = null;
            VisibleRangeCount = VisibleBubbleCount = 0;
            if (canvasRect != null) canvasRect.gameObject.SetActive(false);
        }

        private void InvalidateSources() { nextSourceRefresh = 0f; }
        private void LateUpdate() { Render(); }

        private void Render()
        {
            if (canvasRect == null) return;
            var phase = context != null ? context.GetLevelState() : LevelState.Unloaded;
            var visible = isActiveAndEnabled && worldCamera != null &&
                (phase == LevelState.Running || phase == LevelState.Paused);
            canvasRect.gameObject.SetActive(visible);
            VisibleRangeCount = VisibleBubbleCount = 0;
            if (!visible) return;
            var slimes = context.SlimeManager?.GetAliveSnapshots();
            var pixelsPerUnit = 0f;
            if (slimes != null)
                foreach (var slime in slimes)
                    if (slime.PixelsPerUnit > 0f) { pixelsPerUnit = slime.PixelsPerUnit; break; }
            if (Time.unscaledTime >= nextSourceRefresh)
            {
                sources = context.MechanismSystem?.GetGuideSources();
                nextSourceRefresh = Time.unscaledTime + .1f;
            }
            if (pixelsPerUnit > 0f && sources != null)
                foreach (var source in sources)
                {
                    var tint = source.Type == GuideSourceType.Food ? FoodColor : FurnaceColor;
                    if (!source.IsAvailable) tint = new Color(.50f, .49f, .44f, .40f);
                    var key = source.IsMapAreaSource ? source.AreaId.Value : source.MechanismId.Value;
                    DrawRange("GuideRange_" + key, source.Position,
                        source.AttractionRadiusPixels / pixelsPerUnit, tint, false);
                }
            if (pixelsPerUnit > 0f && placement != null && placement.IsPlacing)
            {
                var preview = placement.GetCurrentPreview();
                var kind = preview.Item == PlacementItemId.Food ? MechanismKind.Food :
                    preview.Item == PlacementItemId.Furnace ? MechanismKind.Furnace : MechanismKind.None;
                if (kind != MechanismKind.None && preview.HasPosition && context.MechanismSystem != null &&
                    context.MechanismSystem.TryGetPlayerConfigId(kind, out var configId) &&
                    context.ConfigRegistry.TryGetMechanism(configId, out var config))
                    DrawRange("GuideRange_Preview", preview.WorldCenter,
                        config.AttractionRadiusPixels / pixelsPerUnit,
                        preview.CanConfirm ? (kind == MechanismKind.Food ? FoodColor : FurnaceColor) :
                            new Color(.85f, .28f, .24f, .78f), true);
            }
            for (var i = VisibleRangeCount; i < ranges.Count; i++) ranges[i].gameObject.SetActive(false);
            bubbleBounds.Clear();
            if (slimes != null && font != null)
                foreach (var slime in slimes) DrawBubble(slime);
            for (var i = VisibleBubbleCount; i < bubbles.Count; i++)
            { bubbles[i].Frame.gameObject.SetActive(false); bubbles[i].Leader.gameObject.SetActive(false); }
        }

        private void DrawRange(string name, Vector2 position, float radius, Color tint, bool preview)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius)) return;
            var center = Project(position);
            var edge = Project(position + Vector2.right * radius);
            var diameter = Vector2.Distance(center, edge) * 2f;
            if (VisibleRangeCount == ranges.Count)
            {
                var rect = Item("GuideRange", rangeLayer);
                var graphic = rect.gameObject.AddComponent<GuideRangeGraphic>();
                graphic.raycastTarget = false;
                ranges.Add(graphic);
            }
            var ring = ranges[VisibleRangeCount++];
            ring.gameObject.name = name;
            ring.gameObject.SetActive(true);
            ring.rectTransform.anchoredPosition = center;
            ring.rectTransform.sizeDelta = Vector2.one * diameter;
            ring.SetStyle(tint, preview);
        }

        private void DrawBubble(SlimeSnapshot slime)
        {
            var hungry = SlimeStatusIds.Contains(slime.StatusIds, SlimeStatusIds.Hungry);
            var agitated = SlimeStatusIds.Contains(slime.StatusIds, SlimeStatusIds.Agitated);
            var afraid = SlimeStatusIds.Contains(slime.StatusIds, SlimeStatusIds.Afraid);
            if (!slime.IsAlive || (!hungry && !agitated && !afraid)) return;
            var viewport = worldCamera.WorldToViewportPoint(slime.Position);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return;
            var text = hungry ? "饥饿" : string.Empty;
            if (agitated) text += (text.Length > 0 ? " · " : "") + "暴躁";
            if (afraid) text += (text.Length > 0 ? " · " : "") + "害怕";
            if (VisibleBubbleCount == bubbles.Count) bubbles.Add(CreateBubble());
            var bubble = bubbles[VisibleBubbleCount++];
            bubble.Frame.gameObject.name = "SlimeStateBubble_" + slime.ActorId;
            bubble.Frame.gameObject.SetActive(true);
            bubble.Frame.color = afraid ? new Color(.34f, .37f, .65f) :
                agitated ? new Color(.73f, .25f, .23f) : new Color(.56f, .39f, .16f);
            if (bubble.Label.font != font) bubble.Label.font = font;
            if (bubble.Label.text != text) bubble.Label.text = text;
            bubble.Label.color = bubble.Frame.color;
            var size = new Vector2(28f + text.Length * 17f, 34f);
            var head = Project(slime.Position + Vector2.up * .4f);
            var position = head + Vector2.up * 12f;
            position.x = Mathf.Clamp(position.x, canvasRect.rect.xMin + size.x * .5f + 6f,
                canvasRect.rect.xMax - size.x * .5f - 6f);
            position = FindBubblePosition(position, size);
            var rect = bubble.Frame.rectTransform;
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            bubbleBounds.Add(new Rect(position.x - size.x * .5f, position.y - 8f, size.x, size.y + 8f));
            var end = position - Vector2.up * 6f;
            var direction = end - head;
            var leader = bubble.Leader.rectTransform;
            bubble.Leader.gameObject.SetActive(direction.magnitude > 10f);
            leader.anchoredPosition = (head + end) * .5f;
            leader.sizeDelta = new Vector2(1.3f, direction.magnitude);
            leader.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            var leaderColor = bubble.Frame.color; leaderColor.a = .7f;
            bubble.Leader.color = leaderColor;
        }

        private Vector2 FindBubblePosition(Vector2 desired, Vector2 size)
        {
            var screen = canvasRect.rect;
            var best = desired;
            var nearest = float.PositiveInfinity;
            // Search nearby rows and columns, retaining a leader to the corresponding slime.
            for (var column = 0; column < 7; column++)
                for (var row = 0; row < 9; row++)
                {
                    var dx = column == 0 ? 0 : (column + 1) / 2 * (column % 2 == 0 ? 1 : -1);
                    var dy = row <= 4 ? row : -(row - 4);
                    var candidate = desired + new Vector2(dx * (size.x + 10f), dy * (size.y + 16f));
                    var bounds = new Rect(candidate.x - size.x * .5f - 3f, candidate.y - 8f, size.x + 6f, size.y + 12f);
                    if (bounds.xMin < screen.xMin + 6f || bounds.xMax > screen.xMax - 6f ||
                        bounds.yMin < screen.yMin + 6f || bounds.yMax > screen.yMax - 6f) continue;
                    var overlap = false;
                    foreach (var other in bubbleBounds)
                        if (bounds.Overlaps(other)) { overlap = true; break; }
                    var distance = (candidate - desired).sqrMagnitude;
                    if (!overlap && distance < nearest) { best = candidate; nearest = distance; }
                }
            if (!float.IsPositiveInfinity(nearest)) return best;
            desired.y = Mathf.Clamp(desired.y, screen.yMin + 14f, screen.yMax - size.y - 8f);
            return desired;
        }

        private Bubble CreateBubble()
        {
            var leader = Item("BubbleLeader", bubbleLayer).gameObject.AddComponent<Image>();
            leader.raycastTarget = false;
            leader.transform.SetAsFirstSibling();
            var frame = Item("SlimeStateBubble", bubbleLayer).gameObject.AddComponent<SlimeSpeechBubbleGraphic>();
            frame.raycastTarget = false;
            var labelRect = Layer("Status", frame.transform);
            var label = labelRect.gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = 18; label.supportRichText = false;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            return new Bubble { Frame = frame, Label = label, Leader = leader };
        }

        private Vector2 Project(Vector2 position)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                worldCamera.WorldToScreenPoint(position), null, out var local);
            return local;
        }

        private static RectTransform Item(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            return (RectTransform)root.transform;
        }

        private static RectTransform Layer(string name, Transform parent)
        {
            var rect = Item(name, parent);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void OnDisable() { Unbind(); }
        private void OnDestroy()
        {
            Unbind();
            if (canvasRect == null) return;
            if (Application.isPlaying) Destroy(canvasRect.gameObject);
            else DestroyImmediate(canvasRect.gameObject);
        }
    }
}
