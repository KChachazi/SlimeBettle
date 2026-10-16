using System;
using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Placement;
using SlimeBattle.World;
using SlimeBattle.UI;
using UnityEngine;

namespace SlimeBattle.Tests.PlayerPresentation
{
    public sealed class PlacementAnchorEditModeTests
    {
        private GameObject owner;
        private PlacementGridAuthoring grid;
        private TestPlacementBackend backend;
        private PlacementSystem placement;
        private IReadOnlyList<PlacementAnchorSnapshot> anchors;
        private bool sourceFails;
        private readonly Vector2 anchorPosition = new Vector2(0.2f, 0.7f);

        private PlacementAnchorSnapshot Anchor(string id = "L1", Vector2? position = null, bool replace = true,
            MechanismKind kind = MechanismKind.Obstacle)
        {
            return new PlacementAnchorSnapshot(new PlacementAnchorId(id), PlacementAnchorRole.RouteBlock,
                position ?? anchorPosition, 80f, new[] { kind }, replace, new MapRouteId("Route1"), default);
        }
        [SetUp] public void SetUp()
        {
            owner = new GameObject("TEST_ONLY_AnchorGrid", typeof(Grid), typeof(PlacementGridAuthoring));
            grid = owner.GetComponent<PlacementGridAuthoring>(); grid.Grid.cellSize = new Vector3(1f, 1f, 0f);
            backend = new TestPlacementBackend(); anchors = new[] { Anchor() };
            Create();
        }
        private void Create(float pixelsPerUnit = 100f)
        {
            placement?.Dispose();
            placement = new PlacementSystem(grid, () => new MapBounds(Vector2.one * -5f, Vector2.one * 5f),
                () => LevelState.Running, backend, PlacementConfirmationMode.Single,
                () => sourceFails ? throw new InvalidOperationException("TEST source unavailable") : anchors, pixelsPerUnit);
        }
        private PlacementPreview Preview(PlacementItemId item = PlacementItemId.Obstacle, Vector3? pointer = null)
        {
            placement.BeginPlacement(item); placement.PreviewPlacement(pointer ?? (Vector3)anchorPosition);
            return placement.GetCurrentPreview();
        }
        [TearDown] public void TearDown()
        { placement.Dispose(); backend.Dispose(); UnityEngine.Object.DestroyImmediate(owner); }

        [Test] public void AnchorRequestUsesExactMapPositionAndOriginalIdentity()
        {
            var recording = new RecordingBackend(backend);
            placement.Dispose(); placement = new PlacementSystem(grid,
                () => new MapBounds(Vector2.one * -5f, Vector2.one * 5f), () => LevelState.Running,
                recording, PlacementConfirmationMode.Single, () => anchors, 100f);
            var preview = Preview(pointer: new Vector3(0.4f, 0.8f));
            Assert.That(preview.Status, Is.EqualTo(PlacementValidationStatus.AnchorFree));
            Assert.That(preview.WorldCenter, Is.EqualTo((Vector3)anchorPosition));
            Assert.That(preview.Cell, Is.EqualTo(Vector3Int.zero));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.True);
            Assert.That(recording.Last.AnchorId, Is.EqualTo(new PlacementAnchorId("L1")));
            Assert.That(recording.Last.WorldCenter, Is.EqualTo((Vector3)anchorPosition));
        }
        [TestCase(100f, 0.79f, true)]
        [TestCase(100f, 0.81f, false)]
        [TestCase(200f, 0.39f, true)]
        [TestCase(200f, 0.41f, false)]
        public void PixelRadiusUsesExplicitMapScale(float scale, float offset, bool snaps)
        {
            Create(scale); var result = Preview(pointer: (Vector3)(anchorPosition + Vector2.right * offset));
            Assert.That(result.IsAnchor, Is.EqualTo(snaps));
            Assert.That(result.CanConfirm, Is.True);
        }
        [TestCase(PlacementItemId.Food)]
        [TestCase(PlacementItemId.Furnace)]
        public void WrongTypeCannotFallBackToAnOtherwiseFreeGridCell(PlacementItemId item)
        {
            Assert.That(Preview(item).Status, Is.EqualTo(PlacementValidationStatus.TypeMismatch));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(backend.Requests, Is.Zero);
        }
        [TestCase(true, PlacementValidationStatus.ReplacementUnavailable)]
        [TestCase(false, PlacementValidationStatus.ReplacementDenied)]
        public void OccupiedAnchorRejectsWithoutRemovingOrCreating(bool replace, PlacementValidationStatus status)
        {
            anchors = new[] { Anchor(replace: replace) };
            var old = MechanismId.NewId(); backend.Add(old, anchorPosition);
            Assert.That(Preview().Status, Is.EqualTo(status));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(backend.Requests, Is.Zero);
            Assert.That(backend.ReadPlacedObjects()[0].Id, Is.EqualTo(old));
            backend.Remove(old); placement.Refresh();
            Assert.That(placement.GetCurrentPreview().CanConfirm, Is.True);
        }
        [Test] public void DifferentObjectInSameGridCellStillBlocksAnchor()
        {
            backend.Add(MechanismId.NewId(), new Vector3(0.9f, 0.1f));
            Assert.That(Preview().Status, Is.EqualTo(PlacementValidationStatus.Occupied));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
        }
        [Test] public void NearestAnchorWinsAndTiesUseOrdinalIdBeforeTypeValidation()
        {
            anchors = new[] { Anchor("L2", anchorPosition + Vector2.right * 0.4f), Anchor("L1") };
            Assert.That(Preview(pointer: (Vector3)(anchorPosition + Vector2.right * 0.3f)).Anchor.Value.AnchorId.Value, Is.EqualTo("L2"));
            anchors = new[] { Anchor("L2"), Anchor("L1", kind: MechanismKind.Food) };
            var result = Preview();
            Assert.That(result.Anchor.Value.AnchorId.Value, Is.EqualTo("L1"));
            Assert.That(result.Status, Is.EqualTo(PlacementValidationStatus.TypeMismatch));
        }
        [Test] public void ConfirmationRechecksChangedTypeAndLateOccupancy()
        {
            Preview(); anchors = new[] { Anchor(kind: MechanismKind.Food) };
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.TypeMismatch));
            anchors = new[] { Anchor() }; Preview(); backend.Add(MechanismId.NewId(), anchorPosition);
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(backend.Requests, Is.Zero);
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void MissingOrInvalidScaleFailsClosed(float scale)
        {
            Create(scale); Assert.That(Preview().Status, Is.EqualTo(PlacementValidationStatus.Unavailable));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
        }
        [Test] public void SourceFailureAndDuplicateDefinitionsDoNotPermitNormalPlacement()
        {
            Preview(); sourceFails = true;
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(placement.GetAnchors(), Is.Empty);
            sourceFails = false; anchors = new[] { Anchor(), Anchor() };
            Assert.That(Preview(pointer: new Vector3(3f, 3f)).Status, Is.EqualTo(PlacementValidationStatus.Unavailable));
            anchors = null;
            Assert.That(Preview().Status, Is.EqualTo(PlacementValidationStatus.Unavailable));
            Assert.That(backend.Requests, Is.Zero);
        }
        [Test] public void OutOfBoundsPointerCannotSnapBackInsideAndOutsideAnchorIsRejected()
        {
            anchors = new[] { Anchor(position: new Vector2(4.9f, 0f)) };
            Assert.That(Preview(pointer: new Vector3(5.1f, 0f)).Status, Is.EqualTo(PlacementValidationStatus.OutOfBounds));
            anchors = new[] { Anchor(position: new Vector2(5.1f, 0f)) };
            Assert.That(Preview(pointer: new Vector3(4.9f, 0f)).Status, Is.EqualTo(PlacementValidationStatus.OutOfBounds));
        }
        [Test] public void RefreshUpdatesAnchorViewsWithoutPointerAndDisposeClearsSnapshots()
        {
            placement.BeginPlacement(PlacementItemId.Obstacle);
            var changes = 0; placement.Changed += () => changes++;
            anchors = new[] { Anchor("L2") }; placement.Refresh();
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(placement.GetAnchors()[0].AnchorId.Value, Is.EqualTo("L2"));
            placement.Dispose(); Assert.That(placement.GetAnchors(), Is.Empty);
        }
        [Test] public void HudReadsAnchorAssociationWithoutInventingGoalWindowOrRouteState()
        {
            using (var context = new LevelContext(new LevelId("TEST_ONLY")))
            {
                Preview(); var source = new FirstLevelUiSnapshotSource(placement);
                var state = source.ReadSnapshot(context);
                Assert.That(state.RouteHint, Does.Contain("L1").And.Contain("Route1"));
                Assert.That(state.GoalText, Is.Empty); Assert.That(state.WaitingText, Is.Empty);
                placement.CancelPlacement(); Assert.That(source.ReadSnapshot(context).RouteHint, Is.Empty);
                var upstream = new StatusSource(); Preview();
                state = new FirstLevelUiSnapshotSource(placement, upstream).ReadSnapshot(context);
                Assert.That(state.RouteHint, Is.EqualTo("TEST_ONLY upstream route fact"));
            }
        }
        private sealed class StatusSource : IFirstLevelStatusSource
        {
            public FirstLevelStatusSnapshot ReadSnapshot(LevelContext context) =>
                new FirstLevelStatusSnapshot(string.Empty, routeHint: "TEST_ONLY upstream route fact");
        }
        private sealed class RecordingBackend : IPlacementBackend
        {
            private readonly TestPlacementBackend inner;
            public RecordingBackend(TestPlacementBackend inner) { this.inner = inner; }
            public PlacementRequest Last;
            public bool IsAvailable => inner.IsAvailable;
            public PlacementResult RequestPlacement(PlacementRequest request) { Last = request; return inner.RequestPlacement(request); }
            public IReadOnlyList<PlacementObjectSnapshot> ReadPlacedObjects() => inner.ReadPlacedObjects();
            public PlacementBudgetSnapshot GetBudgetSnapshot() => inner.GetBudgetSnapshot();
        }
    }
}
