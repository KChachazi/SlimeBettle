using System;
using System.Collections.Generic;
using SlimeBattle.Player.Input;
using SlimeBattle.Presentation.Camera;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;

namespace SlimeBattle.Player.Placement
{
    /// <summary>Existing pointer facts -> camera projection -> placement preview. Confirmation is explicit.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlacementInputAdapter : MonoBehaviour
    {
        private PlayerInputSystem input;
        private CameraSystem cameraSystem;
        private PlacementSystem placement;
        private InputSubscriptionToken primaryToken;
        private InputSubscriptionToken movedToken;
        private InputSubscriptionToken cancelToken;
        private bool isTargetLocked;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        public void Initialize(PlayerInputSystem input, CameraSystem cameraSystem, PlacementSystem placement)
        {
            Unbind();
            this.input = input != null ? input : throw new ArgumentNullException(nameof(input));
            this.cameraSystem = cameraSystem != null ? cameraSystem : throw new ArgumentNullException(nameof(cameraSystem));
            this.placement = placement ?? throw new ArgumentNullException(nameof(placement));
            if (isActiveAndEnabled) { Bind(); }
        }

        private void Bind()
        {
            placement.Changed += OnPlacementChanged;
            primaryToken = input.Subscribe<PlayerCommandSubmittedEvent>(OnPrimary);
            movedToken = input.Subscribe<PointerMovedEvent>(OnMoved);
            cancelToken = input.Subscribe<CancelInputEvent>(_ => placement.CancelPlacement());
        }

        private void Unbind()
        {
            if (placement != null) { placement.Changed -= OnPlacementChanged; }
            if (input != null)
            {
                input.Unsubscribe(primaryToken);
                input.Unsubscribe(movedToken);
                input.Unsubscribe(cancelToken);
            }
            primaryToken = movedToken = cancelToken = default;
            isTargetLocked = false;
            placement?.CancelPlacement();
        }

        private void OnPlacementChanged()
        {
            if (!placement.IsPlacing) { isTargetLocked = false; }
        }

        private void OnPrimary(PlayerCommandSubmittedEvent data)
        {
            if (data.Command.Kind == PlayerCommandKind.PrimaryAction)
            { Preview(data.Command.ScreenPosition, true); }
        }
        private void OnMoved(PointerMovedEvent data) { Preview(data.Position); }

        private void LateUpdate()
        {
            if (input == null || placement == null || !placement.IsPlacing || isTargetLocked) { return; }
            var snapshot = input.ReadSnapshot();
            if (snapshot.Frame == Time.frameCount && snapshot.Mode == InputMode.Gameplay &&
                !snapshot.PointerBlocked && snapshot.CameraDrag == Vector2.zero)
            { Preview(snapshot.PointerPosition); }
        }

        private void Preview(Vector2 screenPosition, bool selectTarget = false)
        {
            if (!isActiveAndEnabled || !placement.IsPlacing || input.GetInputMode() != InputMode.Gameplay ||
                isTargetLocked && !selectTarget) { return; }
            if (EventSystem.current != null)
            {
                uiHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPosition }, uiHits);
                foreach (var hit in uiHits) { if (hit.module is GraphicRaycaster) { return; } }
            }
            var result = placement.PreviewPlacement(cameraSystem.ScreenToWorld(screenPosition));
            // Keep the world target while the pointer travels to Confirm. A new map click replaces it.
            if (selectTarget) { isTargetLocked = result.Preview.HasPosition; }
        }

        private void OnEnable() { if (placement != null && input != null) { Bind(); } }
        private void OnDisable() { Unbind(); }
        private void OnDestroy() { Unbind(); }
    }
}
