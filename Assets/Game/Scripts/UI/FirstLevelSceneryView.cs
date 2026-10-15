using UnityEngine;

namespace SlimeBattle.UI
{
    /// <summary>Decorative scenery only. Cover the viewport without changing map or placement coordinates.</summary>
    [RequireComponent(typeof(SpriteRenderer)), DisallowMultipleComponent]
    public sealed class FirstLevelSceneryView : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        public void FitToCamera(Camera camera)
        {
            targetCamera = camera;
            var renderer = GetComponent<SpriteRenderer>();
            if (camera == null || !camera.orthographic || renderer.sprite == null) return;
            var size = renderer.sprite.bounds.size;
            var height = camera.orthographicSize * 2f;
            var scale = Mathf.Max(height / size.y, height * camera.aspect / size.x);
            transform.localScale = Vector3.one * scale;
        }
        private void LateUpdate() { if (targetCamera != null) FitToCamera(targetCamera); }
    }
}
