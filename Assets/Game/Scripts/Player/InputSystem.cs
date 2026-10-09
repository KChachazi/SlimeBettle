using System;
using System.Collections.Generic;
using SlimeBattle.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SlimeBattle.Player.Input
{
    /// <summary>Device facts only. Consumers own actor selection and gameplay requests.</summary>
    public sealed class InputSystem : MonoBehaviour
    {
        private readonly LevelEventBus inputEvents = new LevelEventBus();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private InputActionMap actions;
        private InputAction point;
        private InputAction press;
        private InputAction move;
        private InputAction attack;
        private InputAction cancel;
        private InputAction pan;
        private InputAction scroll;
        private Pointer primaryPointer;
        private Pointer panPointer;
        private InputMode mode;
        private PlayerInputSnapshot snapshot;
        private Vector2 previousPosition;
        private Vector2 previousMovement;
        private bool isRequestedEnabled;
        private bool hasFocus = true;
        private bool isPointerCaptured;
        private bool isPanCaptured;
        private bool isPointerBlocked;
        private bool shouldWaitForRelease;

        public void Enable()
        {
            EnsureActions();
            if (isRequestedEnabled) { return; }
            isRequestedEnabled = true;
            shouldWaitForRelease = true;
            if (isActiveAndEnabled && hasFocus) { actions.Enable(); }
        }

        public void Disable()
        {
            isRequestedEnabled = false;
            actions?.Disable();
            ResetGesture();
        }

        public void SetInputMode(InputMode inputMode)
        {
            if (!Enum.IsDefined(typeof(InputMode), inputMode))
            {
                throw new ArgumentOutOfRangeException(nameof(inputMode));
            }
            if (mode == inputMode) { return; }
            mode = inputMode;
            ResetGesture();
        }

        public InputMode GetInputMode() { return mode; }

        // Reading does not consume edges; every consumer sees the same frame.
        public PlayerInputSnapshot ReadSnapshot()
        {
            return snapshot.Frame == Time.frameCount ? snapshot : default;
        }

        public SubscriptionToken Subscribe<TInputEvent>(Action<TInputEvent> handler)
        {
            return inputEvents.Subscribe(handler);
        }

        public void Unsubscribe(SubscriptionToken token) { inputEvents.Unsubscribe(token); }

        private void EnsureActions()
        {
            if (actions != null) { return; }
            actions = new InputActionMap("P0Player");
            point = actions.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position");
            press = actions.AddAction("Primary", InputActionType.Button, "<Pointer>/press");
            move = actions.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            attack = actions.AddAction("Attack", InputActionType.Button, "<Keyboard>/space");
            attack.AddBinding("<Gamepad>/buttonSouth");
            cancel = actions.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/start");
            pan = actions.AddAction("CameraDrag", InputActionType.Button, "<Mouse>/middleButton");
            scroll = actions.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll");
        }

        private void Update()
        {
            if (!isRequestedEnabled || !hasFocus || mode == InputMode.Disabled) { return; }
            if (press.WasPressedThisFrame()) { primaryPointer = press.activeControl?.device as Pointer; }
            if (pan.WasPressedThisFrame()) { panPointer = pan.activeControl?.device as Pointer; }
            // Position magnitudes must not choose a different mouse/touch device from the gesture.
            var pointer = pan.IsPressed() ? panPointer :
                press.IsPressed() || isPointerCaptured || isPointerBlocked ? primaryPointer : null;
            var position = pointer != null ? pointer.position.ReadValue() : point.ReadValue<Vector2>();
            var delta = position - previousPosition;
            previousPosition = position;
            var didCancel = cancel.WasPressedThisFrame();
            if (didCancel)
            {
                ResetGesture(false);
                inputEvents.Publish(new CancelInputEvent(true));
            }

            if (shouldWaitForRelease)
            {
                shouldWaitForRelease = press.IsPressed() || pan.IsPressed() ||
                    attack.IsPressed() || move.ReadValue<Vector2>().sqrMagnitude > 0.001f;
                snapshot = new PlayerInputSnapshot(Time.frameCount, mode, position, Vector2.zero,
                    Vector2.zero, false, false, false, false, didCancel, Vector2.zero, 0f);
                return;
            }

            var isOverUi = IsOverUi(position);
            var isTyping = EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
            var canPlay = mode == InputMode.Gameplay && !isTyping;
            if (press.WasPressedThisFrame()) { isPointerBlocked = !canPlay || isOverUi; }
            var didPress = canPlay && !isPointerBlocked && press.WasPressedThisFrame();
            if (didPress) { isPointerCaptured = true; }
            var didRelease = isPointerCaptured && press.WasReleasedThisFrame();
            var isHeld = isPointerCaptured && press.IsPressed();
            if (pan.WasPressedThisFrame()) { isPanCaptured = canPlay && !isOverUi; }
            var cameraDrag = isPanCaptured && pan.IsPressed() ? delta : Vector2.zero;
            if (pan.WasReleasedThisFrame()) { isPanCaptured = false; panPointer = null; }
            var movement = canPlay ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            var didAttack = canPlay && !isOverUi && !isPointerBlocked && !isHeld && attack.WasPressedThisFrame();
            snapshot = new PlayerInputSnapshot(Time.frameCount, mode, position, delta,
                movement, didPress, isHeld, didRelease, didAttack, didCancel,
                cameraDrag, canPlay && !isOverUi ? scroll.ReadValue<Vector2>().y : 0f);

            if (didPress)
            {
                inputEvents.Publish(new PointerPressedEvent(position));
                Submit(new PlayerCommand(PlayerCommandKind.PrimaryAction, Vector2.zero, position));
            }
            if (isHeld && delta.sqrMagnitude > 0f)
            {
                inputEvents.Publish(new PointerMovedEvent(position, delta));
            }
            if (didRelease)
            {
                isPointerCaptured = false;
                inputEvents.Publish(new PointerReleasedEvent(position));
            }
            if (press.WasReleasedThisFrame()) { isPointerBlocked = false; primaryPointer = null; }
            if (movement != Vector2.zero || previousMovement != Vector2.zero)
            {
                Submit(new PlayerCommand(PlayerCommandKind.Move, movement, position));
            }
            previousMovement = movement;
            if (didAttack) { Submit(new PlayerCommand(PlayerCommandKind.Attack, Vector2.zero, position)); }
        }

        private void Submit(PlayerCommand command)
        {
            if (mode == InputMode.Gameplay && isRequestedEnabled && isActiveAndEnabled)
            {
                inputEvents.Publish(new PlayerCommandSubmittedEvent(command));
            }
        }

        private bool IsOverUi(Vector2 position)
        {
            if (EventSystem.current == null) { return false; }
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            uiHits.Clear();
            EventSystem.current.RaycastAll(pointer, uiHits);
            foreach (var hit in uiHits)
            {
                if (hit.module is GraphicRaycaster) { return true; }
            }
            return false;
        }

        private void ResetGesture(bool notify = true)
        {
            var hadGesture = isPointerCaptured || isPanCaptured || previousMovement != Vector2.zero;
            isPointerCaptured = false;
            isPanCaptured = false;
            isPointerBlocked = false;
            primaryPointer = null;
            panPointer = null;
            previousMovement = Vector2.zero;
            snapshot = default;
            shouldWaitForRelease = true;
            if (notify && hadGesture) { inputEvents.Publish(new CancelInputEvent()); }
        }

        private void OnEnable()
        {
            if (isRequestedEnabled && hasFocus) { actions?.Enable(); }
            shouldWaitForRelease = true;
        }

        private void OnDisable() { actions?.Disable(); ResetGesture(); }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            ResetGesture();
            if (focused && isRequestedEnabled && isActiveAndEnabled) { actions?.Enable(); }
            else { actions?.Disable(); }
        }

        private void OnDestroy()
        {
            actions?.Dispose();
            actions = null;
            inputEvents.Dispose();
        }
    }
}
