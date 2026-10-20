using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Input;
using SlimeBattle.Presentation.Camera;
using UnityEngine;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;

namespace SlimeBattle.Player.Placement
{
    /// <summary>Routes food clicks and cart drags to Castle's dining cart.</summary>
    public sealed class CastleFoodCartInteractionAdapter : MonoBehaviour,
        IPlacementPrimaryActionHandler
    {
        private PlayerInputSystem input;
        private CameraSystem cameraSystem;
        private PlacementSystem placement;
        private MechanismSystem mechanisms;
        private InputSubscriptionToken pressedToken;
        private InputSubscriptionToken movedToken;
        private InputSubscriptionToken releasedToken;
        private InputSubscriptionToken cancelToken;
        private MechanismId draggedCartId;
        private Vector2 dragOffset;
        private string lastMoveFailure;

        public void Initialize(PlayerInputSystem input, CameraSystem cameraSystem,
            PlacementSystem placement, MechanismSystem mechanisms)
        {
            Unbind();
            this.input = input != null ? input : throw new System.ArgumentNullException(nameof(input));
            this.cameraSystem = cameraSystem != null
                ? cameraSystem : throw new System.ArgumentNullException(nameof(cameraSystem));
            this.placement = placement ?? throw new System.ArgumentNullException(nameof(placement));
            this.mechanisms = mechanisms;
            if (isActiveAndEnabled) Bind();
        }

        public bool IsInteractionTarget(Vector2 screenPosition)
        {
            return mechanisms != null && mechanisms.TryGetFoodCartAtPosition(
                cameraSystem.ScreenToWorld(screenPosition), out _);
        }

        public bool TryHandlePrimaryAction(Vector2 screenPosition)
        {
            if (!IsInteractionTarget(screenPosition)) return false;
            if (placement == null || placement.SelectedItem != PlacementItemId.Food)
            {
                placement?.ReportInteractionHint("请先选择食物，再点击餐车装入。");
                return true;
            }

            if (!mechanisms.TryGetFoodCartAtPosition(cameraSystem.ScreenToWorld(screenPosition),
                out var cartId)) return true;
            var result = mechanisms.RequestLoadFoodCart(cartId);
            if (result.Succeeded)
            {
                placement.CancelPlacement();
                placement.ReportInteractionHint("食物已装入餐车，餐车移动时会继续引导史莱姆。");
            }
            else
            {
                placement.ReportInteractionHint(result.Reason);
            }
            RefreshPlacementPrompts(default);
            return true;
        }

        private void Bind()
        {
            if (input == null) return;
            pressedToken = input.Subscribe<PointerPressedEvent>(OnPointerPressed);
            movedToken = input.Subscribe<PointerMovedEvent>(OnPointerMoved);
            releasedToken = input.Subscribe<PointerReleasedEvent>(OnPointerReleased);
            cancelToken = input.Subscribe<CancelInputEvent>(_ => ClearDrag());
            if (placement != null) placement.Changed += OnPlacementChanged;
            RefreshPlacementPrompts(default);
        }

        private void Unbind()
        {
            if (input != null)
            {
                input.Unsubscribe(pressedToken);
                input.Unsubscribe(movedToken);
                input.Unsubscribe(releasedToken);
                input.Unsubscribe(cancelToken);
            }
            if (placement != null) placement.Changed -= OnPlacementChanged;
            pressedToken = movedToken = releasedToken = cancelToken = default;
            ClearDrag();
            RefreshPlacementPrompts(default);
        }

        private void OnPointerPressed(PointerPressedEvent data)
        {
            if (input == null || mechanisms == null || input.GetInputMode() != InputMode.Gameplay)
                return;
            var pointerWorld = (Vector2)cameraSystem.ScreenToWorld(data.Position);
            if (!mechanisms.TryGetFoodCartAtPosition(pointerWorld, out var cartId) ||
                !mechanisms.TryGetFoodCartPosition(cartId, out var cartPosition) ||
                !input.ClaimPointerGesture()) return;
            draggedCartId = cartId;
            dragOffset = cartPosition - pointerWorld;
            lastMoveFailure = string.Empty;
        }

        private void OnPointerMoved(PointerMovedEvent data)
        {
            if (!draggedCartId.IsValid || input == null || !input.IsPointerGestureDragging ||
                mechanisms == null) return;
            var pointerWorld = (Vector2)cameraSystem.ScreenToWorld(data.Position);
            if (!mechanisms.RequestMoveFoodCart(draggedCartId, pointerWorld + dragOffset,
                out var failureReason))
                lastMoveFailure = failureReason;
            else
                lastMoveFailure = string.Empty;
            RefreshPlacementPrompts(default);
        }

        private void OnPointerReleased(PointerReleasedEvent data)
        {
            if (draggedCartId.IsValid && input != null && input.IsPointerGestureDragging &&
                mechanisms != null)
            {
                var pointerWorld = (Vector2)cameraSystem.ScreenToWorld(data.Position);
                if (!mechanisms.RequestMoveFoodCart(draggedCartId,
                    pointerWorld + dragOffset, out var failureReason))
                    lastMoveFailure = failureReason;
            }
            if (draggedCartId.IsValid && !string.IsNullOrEmpty(lastMoveFailure))
                placement?.ReportInteractionHint(lastMoveFailure);
            ClearDrag();
        }

        private void OnPlacementChanged() => RefreshPlacementPrompts(default);

        private void LateUpdate()
        {
            if (input == null || mechanisms == null) return;
            var snapshot = input.ReadSnapshot();
            var hoveredCart = default(MechanismId);
            if (snapshot.Mode == InputMode.Gameplay && !snapshot.PointerBlocked &&
                mechanisms.TryGetFoodCartAtPosition(
                    cameraSystem.ScreenToWorld(snapshot.PointerPosition), out var cartId))
                hoveredCart = cartId;
            RefreshPlacementPrompts(hoveredCart);
        }

        private void RefreshPlacementPrompts(MechanismId hoveredCart)
        {
            if (mechanisms == null) return;
            var wantsFood = placement != null && placement.IsPlacing &&
                placement.SelectedItem == PlacementItemId.Food;
            foreach (var cartId in mechanisms.GetFoodCartIds())
            {
                var visible = wantsFood && mechanisms.CanLoadFoodCart(cartId, out _);
                mechanisms.SetFoodCartPlacementPrompt(cartId, visible,
                    visible && cartId == hoveredCart);
            }
        }

        private void ClearDrag()
        {
            draggedCartId = default;
            dragOffset = default;
            lastMoveFailure = string.Empty;
        }

        private void OnEnable()
        {
            if (input != null) Bind();
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
