using NUnit.Framework;
using System.Reflection;
using SlimeBattle.Player.Input;
using SlimeBattle.Presentation.Camera;
using UnityEditor;
using UnityEngine;

namespace SlimeBattle.Tests
{
    public sealed class CameraZoomEditModeTests
    {
        private GameObject owner;
        private Camera camera;
        private CameraSystem cameraSystem;
        private readonly CameraBounds mapBounds = new CameraBounds(Vector2.zero, Vector2.one * 57.6f);

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("TEST_ONLY_MapZoom", typeof(Camera), typeof(CameraSystem));
            camera = owner.GetComponent<Camera>();
            camera.pixelRect = new Rect(0f, 0f, 1280f, 720f);
            camera.aspect = 16f / 9f;
            camera.orthographicSize = 6f;
            cameraSystem = owner.GetComponent<CameraSystem>();
            cameraSystem.Initialize();
            cameraSystem.SetBounds(mapBounds);
            cameraSystem.SubmitCommand(new CameraCommand(CameraCommandKind.Focus, Vector2.one * 28.8f));
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(owner); }

        [TestCase(16f / 9f)]
        [TestCase(4f / 3f)]
        [TestCase(9f / 16f)]
        public void ZoomOutShowsEveryMapCornerWithMargin(float aspect)
        {
            camera.aspect = aspect;
            cameraSystem.SubmitCommand(new CameraCommand(CameraCommandKind.Focus, new Vector2(2f, 50f)));
            cameraSystem.SubmitCommand(new CameraCommand(CameraCommandKind.Zoom, Vector2.zero, 1000f));

            AssertWholeMapVisible();
            Assert.That(camera.orthographicSize, Is.GreaterThan(12f), "The old fixed limit cannot show this map.");
            Assert.That(camera.transform.position.z, Is.EqualTo(-10f));
        }

        [Test]
        public void OverviewRemainsFittedAfterWindowAspectChangesAndReinitialization()
        {
            cameraSystem.SubmitCommand(new CameraCommand(CameraCommandKind.Zoom, Vector2.zero, 1000f));
            camera.aspect = 9f / 16f;
            TickCamera();
            AssertWholeMapVisible();
            cameraSystem.Initialize();
            AssertWholeMapVisible();
            camera.aspect = 16f / 9f;
            TickCamera();
            AssertWholeMapVisible();
            Assert.That(camera.orthographicSize, Is.LessThan(31f), "Returning to landscape must tighten the overview.");
        }

        [Test]
        public void OneNormalizedWheelNotchZoomsAroundPointerAndCanReachBothLimits()
        {
            var pointer = new Vector2(800f, 420f);
            var pointerWorld = cameraSystem.ScreenToWorld(pointer);
            Wheel(1f, pointer);
            Assert.That(camera.orthographicSize, Is.InRange(5.8f, 5.85f));
            Assert.That(Vector3.Distance(pointerWorld, cameraSystem.ScreenToWorld(pointer)), Is.LessThan(0.001f));
            Wheel(-1f, pointer);
            Assert.That(camera.orthographicSize, Is.EqualTo(6f).Within(0.001f));

            for (var i = 0; i < 200; i++) Wheel(-1f, pointer);
            AssertWholeMapVisible();
            for (var i = 0; i < 200; i++) Wheel(1f, pointer);
            Assert.That(camera.orthographicSize, Is.EqualTo(2f).Within(0.001f));
            Assert.That(camera.transform.position.z, Is.EqualTo(-10f));
        }

        [Test]
        public void DragMovesCameraInWorldUnits()
        {
            var pointer = new Vector2(700f, 400f);
            var drag = new Vector2(80f, 40f);
            var before = camera.transform.position;
            var expected = before + cameraSystem.ScreenToWorld(pointer - drag) - cameraSystem.ScreenToWorld(pointer);
            cameraSystem.ApplyInput(new PlayerInputSnapshot(0, InputMode.Gameplay, pointer, Vector2.zero,
                Vector2.zero, false, false, false, false, false, drag, 0f));
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(0.001f));
        }

        [Test]
        public void BoundsAndInputModeStillProtectCamera()
        {
            cameraSystem.SubmitCommand(new CameraCommand(CameraCommandKind.Zoom, Vector2.zero, -1000f));
            cameraSystem.SubmitCommand(new CameraCommand(CameraCommandKind.Pan, Vector2.one * 1000f));
            Assert.That(camera.transform.position.x + camera.orthographicSize * camera.aspect,
                Is.LessThanOrEqualTo(mapBounds.Maximum.x + 0.001f));
            Assert.That(camera.transform.position.y + camera.orthographicSize,
                Is.LessThanOrEqualTo(mapBounds.Maximum.y + 0.001f));
            var before = cameraSystem.GetState();
            cameraSystem.ApplyInput(new PlayerInputSnapshot(0, InputMode.UiOnly, Vector2.zero, Vector2.zero,
                Vector2.zero, false, false, false, false, false, Vector2.one * 100f, -100f));
            Assert.That(cameraSystem.GetState().Position, Is.EqualTo(before.Position));
            Assert.That(camera.orthographicSize, Is.EqualTo(before.OrthographicSize));
        }

        [TestCase("Assets/Game/UI/Prefabs/PF_FirstLevelPlayer.prefab")]
        [TestCase("Assets/Game/UI/Prefabs/PF_CastlePlayer.prefab")]
        public void ProductionCameraSupportsNormalizedWheelAndMapOverview(string prefabPath)
        {
            Object.DestroyImmediate(owner);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab);
            owner = Object.Instantiate(prefab.GetComponentInChildren<CameraSystem>(true).gameObject);
            camera = owner.GetComponent<Camera>();
            camera.aspect = 16f / 9f;
            cameraSystem = owner.GetComponent<CameraSystem>();
            cameraSystem.Initialize();
            cameraSystem.SetBounds(mapBounds);
            Assert.That(camera.orthographicSize, Is.EqualTo(6f), "Map bounds must preserve the opening close-up.");
            Wheel(1f, camera.pixelRect.center);
            Assert.That(camera.orthographicSize, Is.InRange(5.8f, 5.85f));
            for (var i = 0; i < 200; i++) Wheel(-1f, camera.pixelRect.center);
            AssertWholeMapVisible();
        }

        private void Wheel(float delta, Vector2 pointer)
        {
            cameraSystem.ApplyInput(new PlayerInputSnapshot(0, InputMode.Gameplay, pointer, Vector2.zero,
                Vector2.zero, false, false, false, false, false, Vector2.zero, delta));
        }

        private void TickCamera()
        {
            typeof(CameraSystem).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cameraSystem, null);
        }

        private void AssertWholeMapVisible()
        {
            foreach (var corner in new[]
            {
                mapBounds.Minimum, mapBounds.Maximum,
                new Vector2(mapBounds.Minimum.x, mapBounds.Maximum.y),
                new Vector2(mapBounds.Maximum.x, mapBounds.Minimum.y)
            })
            {
                var viewport = camera.WorldToViewportPoint(corner);
                Assert.That(viewport.x, Is.InRange(0.001f, 0.999f), "Map corner must have horizontal margin.");
                Assert.That(viewport.y, Is.InRange(0.001f, 0.999f), "Map corner must have vertical margin.");
                Assert.That(viewport.z, Is.GreaterThan(0f));
            }
        }
    }
}
