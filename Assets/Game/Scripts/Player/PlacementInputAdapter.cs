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
    /// <summary>Existing pointer facts -> camera projection -> immediate placement on a valid map click.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlacementInputAdapter : MonoBehaviour
    {
        private PlayerInputSystem input;
        private CameraSystem cameraSystem;
        private PlacementSystem placement;
        private InputSubscriptionToken primaryToken;
        private InputSubscriptionToken movedToken;
        private InputSubscriptionToken cancelToken;
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
            primaryToken = input.Subscribe<PlayerCommandSubmittedEvent>(OnPrimary);
            movedToken = input.Subscribe<PointerMovedEvent>(OnMoved);
            cancelToken = input.Subscribe<CancelInputEvent>(_ => placement.CancelPlacement());
        }

        private void Unbind()
        {
            if (input != null)
            {
                input.Unsubscribe(primaryToken);
                input.Unsubscribe(movedToken);
                input.Unsubscribe(cancelToken);
            }
            primaryToken = movedToken = cancelToken = default;
            placement?.CancelPlacement();
        }

        private void OnPrimary(PlayerCommandSubmittedEvent data)
        {
            if (data.Command.Kind == PlayerCommandKind.PrimaryAction)
            {
                if (Preview(data.Command.ScreenPosition))
                {
                    if (placement.IsRemoving) { placement.ConfirmRemoval(); }
                    else { placement.ConfirmPlacement(); }
                }
            }
        }
        private void OnMoved(PointerMovedEvent data)
        {
            if (input.ReadSnapshot().CameraDrag == Vector2.zero) { Preview(data.Position); }
        }

        private void LateUpdate()
        {
            if (input == null || placement == null || !placement.IsInteracting) { return; }
            var snapshot = input.ReadSnapshot();
            if (snapshot.Frame == Time.frameCount && snapshot.Mode == InputMode.Gameplay &&
                !snapshot.PointerBlocked && snapshot.CameraDrag == Vector2.zero)
            { Preview(snapshot.PointerPosition); }
        }

        private bool Preview(Vector2 screenPosition)
        {
            if (!isActiveAndEnabled || !placement.IsInteracting || input.GetInputMode() != InputMode.Gameplay)
                return false;
            if (EventSystem.current != null)
            {
                uiHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPosition }, uiHits);
                foreach (var hit in uiHits) { if (hit.module is GraphicRaycaster) { return false; } }
            }
            var result = placement.PreviewPlacement(cameraSystem.ScreenToWorld(screenPosition));
            return result.IsValid;
        }

        private void OnEnable() { if (placement != null && input != null) { Bind(); } }
        private void OnDisable() { Unbind(); }
        private void OnDestroy() { Unbind(); }
    }
}
