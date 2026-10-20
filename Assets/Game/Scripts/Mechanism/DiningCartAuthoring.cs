using System;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    /// <summary>Scene-authored cart path, cargo slot and pointer target.</summary>
    [DisallowMultipleComponent]
    public sealed class DiningCartAuthoring : MonoBehaviour
    {
        [SerializeField] private string cartId;
        [SerializeField] private Transform foodSlot;
        [SerializeField] private Collider2D interactionCollider;
        [SerializeField] private Transform[] trackPoints = Array.Empty<Transform>();
        [SerializeField, Min(0.01f)] private float trackSnapDistance = 1.25f;
        [SerializeField, Min(0f)] private float movementClearanceRadius = 2.8f;
        [SerializeField] private SpriteRenderer cartRenderer;
        [SerializeField] private SpriteRenderer highlightRenderer;
        [SerializeField, ColorUsage(false)] private Color highlightColor = new Color(1f, 0.78f, 0.24f, 1f);

        private MaterialPropertyBlock highlightProperties;
        private MechanismSystem system;

        public MechanismId CartId => new MechanismId(cartId);
        public Vector2 Position => transform.position;
        public Transform FoodSlot => foodSlot;
        internal Collider2D InteractionCollider => interactionCollider != null
            ? interactionCollider : GetComponent<Collider2D>();
        public float MovementClearanceRadius => movementClearanceRadius;
        public bool HasValidTrack => trackPoints != null && trackPoints.Length >= 2;
        public int FoodSortingLayerId => cartRenderer != null
            ? cartRenderer.sortingLayerID : 0;
        public int FoodSortingOrder => cartRenderer != null
            ? cartRenderer.sortingOrder + 1 : 1;

        public bool ContainsPointer(Vector2 worldPosition)
        {
            var collider = InteractionCollider;
            return collider != null && collider.enabled && collider.OverlapPoint(worldPosition);
        }

        internal void Bind(MechanismSystem mechanismSystem) => system = mechanismSystem;

        internal void Unbind(MechanismSystem mechanismSystem)
        {
            if (system == mechanismSystem) system = null;
        }

        public void SetFoodVisualSorting(MechanismAuthoring food)
        {
            if (food == null) return;
            foreach (var renderer in food.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (cartRenderer != null) renderer.sortingLayerID = cartRenderer.sortingLayerID;
                renderer.sortingOrder = FoodSortingOrder;
            }
        }

        public bool TryProjectToTrack(Vector2 worldPosition, out Vector2 projected)
        {
            projected = default;
            if (!HasValidTrack) return false;

            var bestDistanceSquared = float.PositiveInfinity;
            var foundSegment = false;
            for (var i = 1; i < trackPoints.Length; i++)
            {
                var start = trackPoints[i - 1];
                var end = trackPoints[i];
                if (start == null || end == null) return false;
                var segment = (Vector2)(end.position - start.position);
                var lengthSquared = segment.sqrMagnitude;
                if (lengthSquared <= 0.000001f) continue;

                var fromStart = worldPosition - (Vector2)start.position;
                var t = Mathf.Clamp01(Vector2.Dot(fromStart, segment) / lengthSquared);
                var candidate = (Vector2)start.position + segment * t;
                var distanceSquared = (worldPosition - candidate).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                projected = candidate;
                foundSegment = true;
            }

            return foundSegment && bestDistanceSquared <= trackSnapDistance * trackSnapDistance;
        }

        public void MoveTo(Vector2 position)
        {
            var current = transform.position;
            transform.position = new Vector3(position.x, position.y, current.z);
        }

        public void SetFoodPlacementPrompt(bool visible, bool hovered)
        {
            if (highlightRenderer == null) return;
            highlightRenderer.enabled = visible;
            if (!visible) return;

            if (highlightProperties == null) highlightProperties = new MaterialPropertyBlock();
            highlightRenderer.GetPropertyBlock(highlightProperties);
            highlightProperties.SetColor("_OutlineColor", highlightColor);
            highlightProperties.SetFloat("_HighlightStrength", hovered ? 1.8f : 1.1f);
            highlightRenderer.SetPropertyBlock(highlightProperties);
        }

        private void OnDisable() => SetFoodPlacementPrompt(false, false);

        private void OnDestroy()
        {
            if (system != null) system.NotifyFoodCartDestroyed(this);
        }

        private void OnDrawGizmosSelected()
        {
            if (trackPoints == null || trackPoints.Length < 2) return;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            for (var i = 1; i < trackPoints.Length; i++)
            {
                if (trackPoints[i - 1] == null || trackPoints[i] == null) continue;
                Gizmos.DrawLine(trackPoints[i - 1].position, trackPoints[i].position);
                Gizmos.DrawSphere(trackPoints[i - 1].position, 0.08f);
            }
            Gizmos.DrawSphere(trackPoints[trackPoints.Length - 1].position, 0.08f);
        }
    }
}
