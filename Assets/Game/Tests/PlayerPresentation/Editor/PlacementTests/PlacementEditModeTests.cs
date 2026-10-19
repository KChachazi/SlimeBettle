using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Placement;
using SlimeBattle.UI;
using SlimeBattle.World;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.Tests.PlayerPresentation
{
    public sealed class PlacementEditModeTests
    {
        private GameObject owner;
        private PlacementGridAuthoring grid;
        private TestPlacementBackend backend;
        private PlacementSystem placement;
        private LevelState phase;
        private MapBounds? bounds;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("TEST_Grid", typeof(Grid), typeof(PlacementGridAuthoring));
            grid = owner.GetComponent<PlacementGridAuthoring>();
            grid.Grid.cellSize = new Vector3(1f, 1f, 0f);
            bounds = new MapBounds(new Vector2(-5f, -5f), new Vector2(5f, 5f));
            phase = LevelState.Running;
            backend = new TestPlacementBackend();
            placement = NewPlacement(PlacementConfirmationMode.Single);
        }
        private PlacementSystem NewPlacement(PlacementConfirmationMode mode)
        { return new PlacementSystem(grid, () => bounds, () => phase, backend, mode); }
        private void Preview(PlacementItemId item = PlacementItemId.Food, Vector3? point = null)
        { placement.BeginPlacement(item); placement.PreviewPlacement(point ?? new Vector3(0.2f, 0.3f)); }
        [TearDown]
        public void TearDown() { placement.Dispose(); backend.Dispose(); UnityEngine.Object.DestroyImmediate(owner); }

        [TestCase(-0.01f, -1)]
        [TestCase(0f, 0)]
        [TestCase(0.999f, 0)]
        [TestCase(1f, 1)]
        public void GridUsesFloorForPositiveAndNegativeCoordinates(float x, int cell)
        { Assert.That(grid.WorldToCell(new Vector3(x, x, 19f)), Is.EqualTo(new Vector3Int(cell, cell, 0))); }

        [Test]
        public void AuthoredOriginSizeRotationAndScaleRoundTrip()
        {
            grid.transform.position = new Vector3(2f, -3f, 4f);
            grid.transform.rotation = Quaternion.Euler(0f, 0f, 25f);
            grid.transform.localScale = new Vector3(2f, 0.75f, 1f);
            grid.Grid.cellSize = new Vector3(0.5f, 2f, 1f);
            grid.ValidateConfiguration();
            var cell = new Vector3Int(-2, 3, 0);
            Assert.That(grid.WorldToCell(grid.GetCellCenter(cell)), Is.EqualTo(cell));
            Assert.That(grid.GetCellCenter(cell).z, Is.EqualTo(4f));
        }

        [Test]
        public void UnsupportedGridAndNonfinitePointerFailClosed()
        {
            grid.Grid.cellGap = Vector3.right * 0.1f;
            Assert.Throws<ArgumentException>(() => grid.ValidateConfiguration());
            grid.Grid.cellGap = Vector3.zero;
            grid.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            Assert.Throws<ArgumentException>(() => grid.ValidateConfiguration());
            grid.transform.rotation = Quaternion.identity;
            Preview(point: new Vector3(float.NaN, 0f));
            Assert.That(placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.InvalidPosition));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(backend.Requests, Is.Zero);
        }

        [Test]
        public void SelectionNeedsPositionAndConfirmation()
        {
            placement.BeginPlacement(PlacementItemId.Food);
            Assert.That(placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.NoPosition));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            placement.PreviewPlacement(new Vector3(0.2f, 0.8f));
            Assert.That(placement.GetCurrentPreview().WorldCenter, Is.EqualTo(new Vector3(0.5f, 0.5f)));
            Assert.That(backend.Requests, Is.Zero);
        }

        [TestCase(PlacementItemId.Food)]
        [TestCase(PlacementItemId.Furnace)]
        [TestCase(PlacementItemId.Obstacle)]
        public void ThreeTypesSubmitSnappedRequestsAndSingleModeExits(PlacementItemId item)
        {
            Preview(item);
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.True);
            Assert.That(placement.IsPlacing, Is.False);
            Assert.That(backend.ReadPlacedObjects().Single().Position, Is.EqualTo(new Vector3(0.5f, 0.5f)));
            Preview(PlacementItemId.Obstacle);
            Assert.That(placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.Occupied));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(backend.Requests, Is.EqualTo(1));
        }

        [TestCase(-5.01f)]
        [TestCase(5.01f)]
        [TestCase(5f)] // Cell centre lies outside the inclusive map edge.
        public void OutsidePointerOrSnappedCenterCannotConfirm(float x)
        {
            Preview(point: new Vector3(x, 0.3f));
            Assert.That(placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.OutOfBounds));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(backend.Requests, Is.Zero);
        }

        [Test]
        public void OutsidePointerSharingAnInsideCellIsStillRejected()
        {
            bounds = new MapBounds(new Vector2(0.2f, 0f), Vector2.one * 3f);
            Preview(point: new Vector3(0.1f, 0.3f));
            Assert.That(placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.OutOfBounds));
        }

        [Test]
        public void OccupancyIsRecheckedAtConfirmAndRemovalReleasesCell()
        {
            Preview();
            var id = MechanismId.NewId();
            backend.Add(id, new Vector3(0.8f, 0.9f));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(backend.Requests, Is.Zero);
            backend.Remove(id);
            placement.Refresh();
            Assert.That(placement.GetCurrentPreview().CanConfirm, Is.True);
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.True);
        }

        [Test]
        public void BoundsAndPhaseAreRecheckedAtConfirm()
        {
            Preview();
            bounds = new MapBounds(Vector2.one, Vector2.one * 3f);
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            bounds = new MapBounds(-Vector2.one * 5f, Vector2.one * 5f);
            placement.PreviewPlacement(Vector3.zero);
            phase = LevelState.Paused;
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            placement.Refresh();
            Assert.That(placement.IsPlacing, Is.False);
            Assert.That(backend.Requests, Is.Zero);
        }

        [TestCase("rejected")]
        [TestCase("exception")]
        [TestCase("malformed")]
        public void BackendFailureNeverReservesCell(string kind)
        {
            backend.RejectNext = kind == "rejected";
            backend.ThrowOnRequest = kind == "exception";
            backend.ReturnInvalidId = kind == "malformed";
            Preview();
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(placement.GetOccupiedCells(), Is.Empty);
            Assert.That(placement.IsPlacing, Is.True);
            Assert.That(placement.Hint, Is.Not.Empty);
        }

        [Test]
        public void MissingBoundsAndMissingBackendRemainUnavailable()
        {
            bounds = null;
            Preview();
            Assert.That(placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.Unavailable));
            bounds = new MapBounds(-Vector2.one * 5f, Vector2.one * 5f);
            backend.IsAvailable = false;
            placement.Refresh();
            Assert.That(placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.Unavailable));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            Assert.That(placement.GetBudgetSnapshot().HasData, Is.False);
        }

        [Test]
        public void ContinuousModeRetainsSelectionAndTracksOccupiedCell()
        {
            placement.Dispose(); placement = NewPlacement(PlacementConfirmationMode.Continuous);
            Preview();
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.True);
            Assert.That(placement.IsPlacing, Is.True);
            Assert.That(placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.Occupied));
            placement.PreviewPlacement(new Vector3(1.2f, 0.2f));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.True);
            Assert.That(backend.Requests, Is.EqualTo(2));
        }

        [Test]
        public void ReentrantConfirmIsRejectedAndPhaseChangeClearsContinuousSelection()
        {
            placement.Dispose(); placement = NewPlacement(PlacementConfirmationMode.Continuous);
            Preview();
            backend.BeforeRequest = () =>
            {
                Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
                phase = LevelState.Failed;
            };
            placement.ConfirmPlacement();
            Assert.That(backend.Requests, Is.EqualTo(1));
            Assert.That(placement.IsPlacing, Is.False);
        }

        [Test]
        public void DisposeDropsStateAndOldInstanceCannotAffectNewSession()
        {
            Preview(); placement.Dispose();
            Assert.That(placement.IsPlacing, Is.False);
            Assert.That(placement.GetOccupiedCells(), Is.Empty);
            Assert.Throws<ObjectDisposedException>(() => placement.BeginPlacement(PlacementItemId.Food));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.False);
            placement = NewPlacement(PlacementConfirmationMode.Single);
            Preview();
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.True);
        }

        [Test]
        public void SelectionPrefabHasEditableButtonsAndCompleteViewReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Resources/UI/PF_PlacementSelection.prefab");
            Assert.IsNotNull(prefab);
            var view = prefab.GetComponent<PlacementSelectionView>();
            Assert.IsNotNull(view);
            var serialized = new SerializedObject(view);
            foreach (var name in new[] { "panel", "hint", "budget", "food", "furnace", "obstacle", "cancel" })
                Assert.IsNotNull(serialized.FindProperty(name).objectReferenceValue, name);
            Assert.That(prefab.GetComponentsInChildren<Button>(true).Select(button => button.name),
                Is.EquivalentTo(new[] { "PlacementFoodButton", "PlacementFurnaceButton",
                    "PlacementObstacleButton", "PlacementCancelButton" }));
        }

        [Test]
        public void RemovalReleasesCellAndStaysActiveUntilCancelledOrAnotherItemSelected()
        {
            Preview();
            var placed = placement.ConfirmPlacement();
            placement.BeginRemoval();
            Assert.That(placement.IsPlacing, Is.False);
            placement.PreviewPlacement(new Vector3(0.2f, 0.3f));
            Assert.That(placement.GetCurrentPreview().RemovalTarget, Is.EqualTo(placed.MechanismId));
            Assert.That(placement.ConfirmRemoval().Succeeded, Is.True);
            Assert.That(backend.ReadPlacedObjects(), Is.Empty);
            Assert.That(placement.GetOccupiedCells(), Is.Empty);
            Assert.That(placement.IsRemoving, Is.True);
            Assert.That(placement.ConfirmRemoval().Succeeded, Is.False);
            Assert.That(backend.RemovalRequests, Is.EqualTo(1));
            placement.BeginPlacement(PlacementItemId.Obstacle);
            Assert.That(placement.IsRemoving, Is.False);
            placement.PreviewPlacement(new Vector3(0.2f, 0.3f));
            Assert.That(placement.ConfirmPlacement().Succeeded, Is.True);
            placement.BeginRemoval(); placement.CancelPlacement();
            Assert.That(placement.IsInteracting, Is.False);
        }

        [TestCase("empty")]
        [TestCase("outside")]
        [TestCase("paused")]
        [TestCase("expired")]
        [TestCase("replaced")]
        public void RemovalRevalidatesTargetAndPhaseBeforeCallingBackend(string condition)
        {
            var id = MechanismId.NewId();
            backend.Add(id, new Vector3(0.5f, 0.5f));
            placement.BeginRemoval();
            placement.PreviewPlacement(new Vector3(0.2f, 0.3f));
            if (condition == "empty") placement.PreviewPlacement(new Vector3(2f, 2f));
            if (condition == "outside") placement.PreviewPlacement(new Vector3(10f, 10f));
            if (condition == "paused") phase = LevelState.Paused;
            if (condition == "expired" || condition == "replaced") backend.Remove(id);
            if (condition == "replaced") backend.Add(MechanismId.NewId(), new Vector3(0.5f, 0.5f));
            Assert.That(placement.ConfirmRemoval().Succeeded, Is.False);
            Assert.That(backend.RemovalRequests, Is.Zero);
            if (condition == "paused")
            {
                placement.Refresh();
                Assert.That(placement.IsInteracting, Is.False);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovalBackendFailurePreservesOccupancy(bool throws)
        {
            var id = MechanismId.NewId();
            backend.Add(id, new Vector3(0.5f, 0.5f));
            placement.BeginRemoval(); placement.PreviewPlacement(new Vector3(0.2f, 0.3f));
            backend.ThrowOnRequest = throws; backend.RejectNext = !throws;
            Assert.That(placement.ConfirmRemoval().Succeeded, Is.False);
            Assert.That(placement.GetOccupiedCells(), Has.Count.EqualTo(1));
            Assert.That(backend.ReadPlacedObjects().Single().Id, Is.EqualTo(id));
            Assert.That(placement.IsRemoving, Is.True);
        }

        [Test]
        public void RemovalIgnoresPlacementOnlyBlockersAndHandlesReentrantPhaseChange()
        {
            placement.Dispose();
            placement = new PlacementSystem(grid, () => bounds, () => phase, backend,
                PlacementConfirmationMode.Single, canOccupy: _ => false);
            backend.Add(MechanismId.NewId(), new Vector3(0.5f, 0.5f));
            placement.BeginRemoval(); placement.PreviewPlacement(new Vector3(0.2f, 0.3f));
            backend.BeforeRequest = () =>
            {
                Assert.That(placement.ConfirmRemoval().Succeeded, Is.False);
                phase = LevelState.Failed;
            };
            Assert.That(placement.ConfirmRemoval().Succeeded, Is.True);
            Assert.That(backend.RemovalRequests, Is.EqualTo(1));
            Assert.That(placement.IsInteracting, Is.False);
        }

        [Test]
        public void HudDisplaysReadOnlyLimitAndWeaponAndRetainsOldConstructor()
        {
            var oldState = new UiState(default, LevelState.Running, "old objective");
            Assert.That(oldState.SlimeLimit, Is.EqualTo(-1));
            var hud = BattleHudView.Create(owner.transform);
            hud.Render(new UiState(default, LevelState.Running, "escort", 4, 0, slimeLimit: 20, weaponText: "weapon unavailable"),
                true, false, false, false);
            var labels = hud.GetComponentsInChildren<Text>().Select(item => item.text).ToArray();
            Assert.That(labels.Any(text => text.Contains("4 / 20")), Is.True);
            Assert.That(labels.Contains("weapon unavailable"), Is.True);
        }
    }
}
