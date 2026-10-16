using UnityEngine;

namespace SlimeBattle.UI
{
    /// <summary>Attach below a full-screen overlay Canvas; decorative/modal layers stay outside.</summary>
    [RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
    public sealed class UiSafeArea : MonoBehaviour
    {
        private Rect previous;
        private Vector2Int size;
        private void OnEnable() { Apply(); }
        private void Update()
        {
            if (previous != Screen.safeArea || size.x != Screen.width || size.y != Screen.height) { Apply(); }
        }
        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) { return; }
            previous = Screen.safeArea;
            size = new Vector2Int(Screen.width, Screen.height);
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(previous.xMin / size.x, previous.yMin / size.y);
            rect.anchorMax = new Vector2(previous.xMax / size.x, previous.yMax / size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
