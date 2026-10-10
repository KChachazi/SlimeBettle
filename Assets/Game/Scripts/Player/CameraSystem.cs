using System;
using SlimeBattle.Player.Input;
using UnityEngine;

namespace SlimeBattle.Presentation.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class CameraSystem : MonoBehaviour
    {
        [SerializeField] private float minimumSize = 2f;
        [SerializeField] private float maximumSize = 12f;
        [SerializeField] private float zoomSensitivity = 0.01f;
        [SerializeField] private float worldPlaneZ;
        private UnityEngine.Camera sceneCamera;
        private CameraBounds bounds;
        private bool hasBounds;
        private CameraState lastState;

        public event Action<CameraStateChangedEvent> CameraStateChanged;
        public event Action<CameraBoundsChangedEvent> CameraBoundsChanged;

        private void Awake() { sceneCamera = GetComponent<UnityEngine.Camera>(); }

        public void Initialize()
        {
            if (sceneCamera == null) { sceneCamera = GetComponent<UnityEngine.Camera>(); }
            minimumSize = Mathf.Max(0.01f, minimumSize);
            maximumSize = Mathf.Max(minimumSize, maximumSize);
            sceneCamera.orthographic = true;
            sceneCamera.transform.rotation = Quaternion.identity;
            if (transform.position.z >= worldPlaneZ - sceneCamera.nearClipPlane)
            {
                var position = transform.position;
                position.z = worldPlaneZ - 10f;
                transform.position = position;
            }
            sceneCamera.orthographicSize = Mathf.Clamp(sceneCamera.orthographicSize, minimumSize, maximumSize);
            Constrain();
        }

        /// <summary>Projects screen pixels onto the XY gameplay plane, including camera Z offset.</summary>
        public Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            var ray = sceneCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, worldPlaneZ));
            if (!plane.Raycast(ray, out var distance))
            {
                throw new InvalidOperationException("The camera cannot project onto the gameplay plane.");
            }
            return ray.GetPoint(distance);
        }

        public Vector2 WorldToScreen(Vector3 worldPosition) { return sceneCamera.WorldToScreenPoint(worldPosition); }
        public CameraState GetState() { return new CameraState(transform.position, sceneCamera.orthographicSize); }

        public void SetBounds(CameraBounds cameraBounds)
        {
            if (!cameraBounds.IsValid) { throw new ArgumentException("Camera bounds must be finite and have positive area.", nameof(cameraBounds)); }
            bounds = cameraBounds;
            hasBounds = true;
            Constrain();
            CameraBoundsChanged?.Invoke(new CameraBoundsChangedEvent(bounds));
        }

        public void SubmitCommand(CameraCommand command)
        {
            if (!CameraBounds.IsFinite(command.PositionOrDelta.x) ||
                !CameraBounds.IsFinite(command.PositionOrDelta.y) || !CameraBounds.IsFinite(command.ZoomDelta)) { return; }
            var position = transform.position;
            switch (command.Kind)
            {
                case CameraCommandKind.Pan:
                    position += new Vector3(command.PositionOrDelta.x, command.PositionOrDelta.y, 0f);
                    break;
                case CameraCommandKind.Focus:
                    position.x = command.PositionOrDelta.x;
                    position.y = command.PositionOrDelta.y;
                    break;
                case CameraCommandKind.Zoom:
                    sceneCamera.orthographicSize = Mathf.Clamp(sceneCamera.orthographicSize + command.ZoomDelta, minimumSize, maximumSize);
                    break;
                default: return;
            }
            transform.position = position;
            Constrain();
        }

        internal void ApplyInput(PlayerInputSnapshot input)
        {
            if (input.Mode != InputMode.Gameplay) { return; }
            if (input.CameraDrag != Vector2.zero)
            {
                var worldDelta = ScreenToWorld(input.PointerPosition - input.CameraDrag) - ScreenToWorld(input.PointerPosition);
                SubmitCommand(new CameraCommand(CameraCommandKind.Pan, worldDelta));
            }
            if (input.Zoom != 0f)
            {
                SubmitCommand(new CameraCommand(CameraCommandKind.Zoom, Vector2.zero, -input.Zoom * zoomSensitivity));
            }
        }

        // Re-clamp after aspect/viewport changes; this updates presentation only.
        private void LateUpdate() { if (sceneCamera != null) { Constrain(); } }

        private void Constrain()
        {
            if (hasBounds)
            {
                var halfHeight = sceneCamera.orthographicSize;
                var halfWidth = halfHeight * sceneCamera.aspect;
                var position = transform.position;
                position.x = ClampAxis(position.x, bounds.Minimum.x, bounds.Maximum.x, halfWidth);
                position.y = ClampAxis(position.y, bounds.Minimum.y, bounds.Maximum.y, halfHeight);
                transform.position = position;
            }
            var state = GetState();
            if (state.Position == lastState.Position && Mathf.Approximately(state.OrthographicSize, lastState.OrthographicSize)) { return; }
            lastState = state;
            CameraStateChanged?.Invoke(new CameraStateChangedEvent(state));
        }

        private static float ClampAxis(float position, float minimum, float maximum, float halfExtent)
        {
            return maximum - minimum <= halfExtent * 2f
                ? (minimum + maximum) * 0.5f
                : Mathf.Clamp(position, minimum + halfExtent, maximum - halfExtent);
        }
    }
}
