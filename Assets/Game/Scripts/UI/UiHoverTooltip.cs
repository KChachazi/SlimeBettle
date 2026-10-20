using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Prefab-owned, non-blocking help that stays dismissed until the pointer re-enters.</summary>
    [DisallowMultipleComponent]
    public sealed class UiHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private Text body;
        [SerializeField] private float preferredWidth = 280f;
        private readonly Vector3[] sourceCorners = new Vector3[4];
        private bool available;
        public bool IsVisible => tooltip != null && tooltip.gameObject.activeInHierarchy;

        public void SetAvailable(bool value)
        {
            available = value;
            if (!value) Hide();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!available || tooltip == null || body == null) return;
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
            Position();
        }

        public void OnPointerExit(PointerEventData eventData) { Hide(); }
        public void OnPointerDown(PointerEventData eventData) { Hide(); }
        public void OnPointerClick(PointerEventData eventData) { Hide(); }
        public void OnSubmit(BaseEventData eventData) { Hide(); }
        private void OnEnable() { Hide(); }
        private void OnDisable() { Hide(); }
        private void LateUpdate() { if (IsVisible) Position(); }
        private void Hide() { if (tooltip != null) tooltip.gameObject.SetActive(false); }

        private void Position()
        {
            var parent = tooltip.parent as RectTransform;
            var source = transform as RectTransform;
            if (parent == null || source == null) return;
            const float margin = 10f;
            const float gap = 16f;
            var width = Mathf.Min(preferredWidth, Mathf.Max(1f, parent.rect.width - margin * 2f));
            tooltip.sizeDelta = new Vector2(width, tooltip.sizeDelta.y);
            var height = Mathf.Max(116f, body.preferredHeight + 72f);
            tooltip.sizeDelta = new Vector2(width, height);
            source.GetWorldCorners(sourceCorners);
            var bottomLeft = parent.InverseTransformPoint(sourceCorners[0]);
            var topRight = parent.InverseTransformPoint(sourceCorners[2]);
            var x = bottomLeft.x - gap - width;
            if (x < parent.rect.xMin + margin) x = topRight.x + gap;
            x = Mathf.Clamp(x, parent.rect.xMin + margin, parent.rect.xMax - margin - width);
            var y = Mathf.Clamp((bottomLeft.y + topRight.y) * 0.5f,
                parent.rect.yMin + margin + height * 0.5f, parent.rect.yMax - margin - height * 0.5f);
            tooltip.localPosition = new Vector3(x, y, 0f);
        }
    }
}
