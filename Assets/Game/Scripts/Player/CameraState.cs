using UnityEngine;

namespace SlimeBattle.Presentation.Camera
{
    public readonly struct CameraBounds
    {
        public CameraBounds(Vector2 minimum, Vector2 maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }
        public Vector2 Minimum { get; }
        public Vector2 Maximum { get; }
        public bool IsValid => IsFinite(Minimum.x) && IsFinite(Minimum.y) &&
            IsFinite(Maximum.x) && IsFinite(Maximum.y) &&
            Maximum.x > Minimum.x && Maximum.y > Minimum.y;

        internal static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    public readonly struct CameraState
    {
        public CameraState(Vector3 position, float orthographicSize)
        {
            Position = position;
            OrthographicSize = orthographicSize;
        }
        public Vector3 Position { get; }
        public float OrthographicSize { get; }
    }

    public enum CameraCommandKind { Pan, Zoom, Focus }

    public readonly struct CameraCommand
    {
        public CameraCommand(CameraCommandKind kind, Vector2 positionOrDelta, float zoomDelta = 0f)
        {
            Kind = kind;
            PositionOrDelta = positionOrDelta;
            ZoomDelta = zoomDelta;
        }
        public CameraCommandKind Kind { get; }
        public Vector2 PositionOrDelta { get; }
        public float ZoomDelta { get; }
    }

    public readonly struct CameraStateChangedEvent
    {
        public CameraStateChangedEvent(CameraState state) { State = state; }
        public CameraState State { get; }
    }

    public readonly struct CameraBoundsChangedEvent
    {
        public CameraBoundsChangedEvent(CameraBounds bounds) { Bounds = bounds; }
        public CameraBounds Bounds { get; }
    }
}
