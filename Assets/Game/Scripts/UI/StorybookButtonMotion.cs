using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    [RequireComponent(typeof(Button)), DisallowMultipleComponent]
    public sealed class StorybookButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Button button;
        private bool hovered;
        private bool pressed;
        private Vector3 restScale;
        private void Awake() { button = GetComponent<Button>(); restScale = transform.localScale; }
        private void Update()
        {
            var scale = button.IsInteractable() ? pressed ? 0.965f : hovered ? 1.025f : 1f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, restScale * scale, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
        }
        public void OnPointerEnter(PointerEventData data) { hovered = true; }
        public void OnPointerExit(PointerEventData data) { hovered = pressed = false; }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData data) { pressed = false; }
        private void OnDisable() { hovered = pressed = false; if (restScale != Vector3.zero) transform.localScale = restScale; }
    }
}
