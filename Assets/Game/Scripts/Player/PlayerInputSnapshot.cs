using UnityEngine;

namespace SlimeBattle.Player.Input
{
    public enum InputMode
    {
        Disabled,
        Gameplay,
        UiOnly
    }

    public enum PlayerCommandKind
    {
        Move,
        PrimaryAction,
        Attack
    }

    public readonly struct PlayerInputSnapshot
    {
        public PlayerInputSnapshot(int frame, InputMode mode, Vector2 pointerPosition,
            Vector2 pointerDelta, Vector2 movement, bool pointerPressed, bool pointerHeld,
            bool pointerReleased, bool attackPressed, bool cancelPressed,
            Vector2 cameraDrag, float zoom)
        {
            Frame = frame;
            Mode = mode;
            PointerPosition = pointerPosition;
            PointerDelta = pointerDelta;
            Movement = movement;
            PointerPressed = pointerPressed;
            PointerHeld = pointerHeld;
            PointerReleased = pointerReleased;
            AttackPressed = attackPressed;
            CancelPressed = cancelPressed;
            CameraDrag = cameraDrag;
            Zoom = zoom;
        }

        public int Frame { get; }
        public InputMode Mode { get; }
        public Vector2 PointerPosition { get; }
        public Vector2 PointerDelta { get; }
        public Vector2 Movement { get; }
        public bool PointerPressed { get; }
        public bool PointerHeld { get; }
        public bool PointerReleased { get; }
        public bool AttackPressed { get; }
        public bool CancelPressed { get; }
        public Vector2 CameraDrag { get; }
        public float Zoom { get; }
    }

    public readonly struct PlayerCommand
    {
        public PlayerCommand(PlayerCommandKind kind, Vector2 movement, Vector2 screenPosition)
        {
            Kind = kind;
            Movement = movement;
            ScreenPosition = screenPosition;
        }

        public PlayerCommandKind Kind { get; }
        public Vector2 Movement { get; }
        public Vector2 ScreenPosition { get; }
    }

    public readonly struct PlayerCommandSubmittedEvent
    {
        public PlayerCommandSubmittedEvent(PlayerCommand command) { Command = command; }
        public PlayerCommand Command { get; }
    }

    public readonly struct PointerPressedEvent
    {
        public PointerPressedEvent(Vector2 position) { Position = position; }
        public Vector2 Position { get; }
    }

    public readonly struct PointerMovedEvent
    {
        public PointerMovedEvent(Vector2 position, Vector2 delta)
        {
            Position = position;
            Delta = delta;
        }
        public Vector2 Position { get; }
        public Vector2 Delta { get; }
    }

    public readonly struct PointerReleasedEvent
    {
        public PointerReleasedEvent(Vector2 position) { Position = position; }
        public Vector2 Position { get; }
    }

    public readonly struct CancelInputEvent
    {
        public CancelInputEvent(bool isUserRequested) { IsUserRequested = isUserRequested; }
        public bool IsUserRequested { get; }
    }
}
