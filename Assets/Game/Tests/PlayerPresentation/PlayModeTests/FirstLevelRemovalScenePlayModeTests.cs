#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SlimeBattle.Core;
using SlimeBattle.Player;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Placement;
using SlimeBattle.World;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SlimeBattle.Tests.PlayerPresentation
{
    public sealed class FirstLevelRemovalScenePlayModeTests
    {
        private GameRoot gameRoot;
        private Mouse mouse;
        private Keyboard keyboard;
        private Pointer[] existingPointers;
        private InputSettings.EditorInputBehaviorInPlayMode previousInputBehavior;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            previousInputBehavior = settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            existingPointers = UnityEngine.InputSystem.InputSystem.devices.OfType<Pointer>().Where(device => device.enabled).ToArray();
            foreach (var pointer in existingPointers) UnityEngine.InputSystem.InputSystem.DisableDevice(pointer);
            mouse = UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>();
            keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ProductionSceneRemovalReleasesCellAndResetsOnRestart()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            var deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            gameRoot = Object.FindObjectOfType<GameRoot>();
            Assert.IsNotNull(gameRoot);
            var manager = gameRoot.GetLevelManager();
            var menu = Object.FindObjectOfType<ReferenceMainMenuView>();
            Assert.IsNotNull(menu, "Bootstrap must expose the illustrated production menu.");
            var newGame = menu.GetComponentsInChildren<Button>(true).Single(button => button.name == "NewGameButton");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, newGame.transform.position));
            var firstChapter = menu.GetComponentsInChildren<Button>(true).Single(button => button.name == "LevelCard1");
            Assert.That(firstChapter.gameObject.activeInHierarchy, Is.True, "New Game must open chapter selection through real pointer input.");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, firstChapter.transform.position));
            Assert.That(menu.SelectedLevel, Is.Zero);
            var start = menu.GetComponentsInChildren<Button>(true).Single(button => button.name == "SelectLevelButton");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, start.transform.position));
            deadline = Time.realtimeSinceStartup + 30f;
            while (manager.GetLevelState() != LevelState.Running && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(manager.GetLevelState(), Is.EqualTo(LevelState.Running));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("FirstLevel"));
            yield return null; yield return null;
            var root = Object.FindObjectOfType<FirstLevelPresentationRoot>();
            Assert.IsNotNull(root?.Placement);
            var context = manager.GetCurrentContext();
            var grid = root.GetComponentInChildren<PlacementGridAuthoring>();
            var camera = root.GetComponentInChildren<CameraSystem>();
            var sceneCamera = camera.GetComponent<Camera>();
            var initialCameraPosition = camera.GetState().Position;
            Assert.That(context.MapManager.TryGetMapBounds(out var bounds), Is.True);
            grid.GetCoveredRange(bounds, out var minimum, out var maximum);
            var remove = root.GetComponentsInChildren<Button>(true).Single(button => button.name == "RemoveButton");
            var args = System.Environment.GetCommandLineArgs();
            var captureIndex = System.Array.IndexOf(args, "-removalScreenshot");
            var capturePath = System.Environment.GetEnvironmentVariable("SLIME_REMOVAL_SCREENSHOT");
            if (captureIndex >= 0 && captureIndex + 1 < args.Length) capturePath = args[captureIndex + 1];
            foreach (var item in new[] { PlacementItemId.Food, PlacementItemId.Furnace, PlacementItemId.Obstacle })
            {
                var kind = item == PlacementItemId.Food ? MechanismKind.Food :
                    item == PlacementItemId.Furnace ? MechanismKind.Furnace : MechanismKind.Obstacle;
                Assert.That(context.MechanismSystem.TryGetPlayerConfigId(kind, out var configId), Is.True);
                var position = Vector3.zero;
                var foundPosition = false;
                for (var y = minimum.y; y <= maximum.y && !foundPosition; y++)
                    for (var x = minimum.x; x <= maximum.x && !foundPosition; x++)
                    {
                        var candidate = grid.GetCellCenter(new Vector3Int(x, y, 0));
                        var viewport = sceneCamera.WorldToViewportPoint(candidate);
                        if (viewport.z <= 0f || viewport.x < 0.28f || viewport.x > 0.68f ||
                            viewport.y < 0.25f || viewport.y > 0.70f) continue;
                        if (!context.MechanismSystem.CanPlace(configId, candidate, out _)) continue;
                        position = candidate; foundPosition = true;
                    }
                Assert.That(foundPosition, Is.True, "The default opening camera must show a legal cell away from HUD controls, without test camera movement.");
                Assert.That(camera.GetState().Position, Is.EqualTo(initialCameraPosition));
                var select = root.GetComponentsInChildren<Button>(true).Single(button => button.name == item + "Button");
                yield return Click(RectTransformUtility.WorldToScreenPoint(null, select.transform.position));
                Assert.That(root.GetComponent<PlacementGridView>().VisiblePlaceableCellCount, Is.GreaterThan(0));
                var screenPosition = camera.WorldToScreen(position);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPosition });
                yield return null; yield return null;
                Assert.That(root.Placement.GetCurrentPreview().CanConfirm, Is.True);
                if (item == PlacementItemId.Food)
                {
                    var placementCapture = System.Environment.GetEnvironmentVariable("SLIME_PLACEMENT_SCREENSHOT");
                    var placementCaptureIndex = System.Array.IndexOf(args, "-placementScreenshot");
                    if (placementCaptureIndex >= 0 && placementCaptureIndex + 1 < args.Length)
                        placementCapture = args[placementCaptureIndex + 1];
                    if (!string.IsNullOrEmpty(placementCapture)) Capture(sceneCamera, root.gameObject, placementCapture);
                }
                yield return Click(screenPosition);
                Assert.That(context.MechanismSystem.ActivePlacedMechanismCount, Is.EqualTo(1));
                var placed = context.MechanismSystem.GetActiveSnapshots().Single(snapshot => snapshot.IsPlayerPlaced);
                Assert.That(remove.interactable, Is.True);
                yield return Click(RectTransformUtility.WorldToScreenPoint(null, remove.transform.position));
                Assert.That(root.Placement.IsRemoving, Is.True, "The production scene button must bind to its current placement session.");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPosition });
                yield return null; yield return null;
                Assert.That(root.Placement.GetCurrentPreview().RemovalTarget, Is.EqualTo(placed.MechanismId));
                if (item == PlacementItemId.Obstacle && !string.IsNullOrEmpty(capturePath))
                {
                    Capture(camera.GetComponent<Camera>(), root.gameObject, capturePath);
                }
                yield return Click(screenPosition);
                Assert.That(context.MechanismSystem.TryGetSnapshot(placed.MechanismId, out var removed), Is.True);
                Assert.That(removed.State, Is.EqualTo(MechanismState.Removed));
                Assert.That(root.Placement.GetOccupiedCells().Contains(
                    grid.WorldToCell(position)), Is.False);
                Assert.That(context.MapManager.CanOccupy(position, new OccupancyQuery(0.1f)), Is.True,
                    "Removing an obstacle must also unregister its movement blocker.");
                Assert.That(context.MechanismSystem.ActivePlacedMechanismCount, Is.Zero);
                Assert.That(root.Placement.GetBudgetSnapshot().ActiveCount, Is.Zero);
                Assert.That(root.Placement.IsRemoving, Is.True);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                Assert.That(root.Placement.IsInteracting, Is.False, "Escape must exit removal without placing anything.");
                Assert.That(manager.GetLevelState(), Is.EqualTo(LevelState.Running), "Cancelling removal must not also pause the level.");
            }
            // Once the opening flow works, verify that later refresh/pause never snaps a panned view back.
            camera.SubmitCommand(new CameraCommand(CameraCommandKind.Pan, new Vector2(2f, 1f)));
            var pannedCameraPosition = camera.GetState().Position;
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(camera.GetState().Position, Is.EqualTo(pannedCameraPosition));
            remove.onClick.Invoke();
            manager.RequestPause();
            Assert.That(root.Placement.IsRemoving, Is.False);
            Assert.That(root.GetComponent<PlacementGridView>().IsVisible, Is.False);
            manager.RequestResume();
            Assert.That(camera.GetState().Position, Is.EqualTo(pannedCameraPosition));
            remove.onClick.Invoke();
            Assert.That(root.Placement.IsRemoving, Is.True);
            var oldPlacement = root.Placement;
            manager.RequestRestartCurrentLevel();
            deadline = Time.realtimeSinceStartup + 20f;
            while ((manager.GetCurrentContext() == context || manager.GetLevelState() != LevelState.Running) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; yield return null;
            root = Object.FindObjectOfType<FirstLevelPresentationRoot>();
            Assert.That(manager.GetCurrentContext(), Is.Not.SameAs(context));
            Assert.That(root.Placement, Is.Not.SameAs(oldPlacement));
            Assert.That(root.Placement.IsInteracting, Is.False);
            var restartedCamera = root.GetComponentInChildren<CameraSystem>();
            Assert.That(Vector3.Distance(restartedCamera.GetState().Position, initialCameraPosition), Is.LessThan(0.1f),
                "Restart must restore the new context's opening focus instead of keeping the previous pan.");
            remove = root.GetComponentsInChildren<Button>(true).Single(button => button.name == "RemoveButton");
            remove.onClick.Invoke();
            Assert.That(root.Placement.IsRemoving, Is.True, "Restart must bind removal to the new level context.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ReturnToMenuCleansUpPausedAndFinishedLevelsAndAllowsReentry()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            var deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup < deadline) yield return null;
            gameRoot = Object.FindObjectOfType<GameRoot>();
            Assert.IsNotNull(gameRoot);
            var manager = gameRoot.GetLevelManager();
            LevelContext previousContext = null;
            foreach (var phase in new[] { LevelState.Paused, LevelState.Paused, LevelState.Succeeded, LevelState.Failed })
            {
                var menu = Object.FindObjectOfType<ReferenceMainMenuView>();
                Assert.IsNotNull(menu);
                yield return ClickButton(menu, "NewGameButton");
                yield return ClickButton(menu, "LevelCard1");
                yield return ClickButton(menu, "SelectLevelButton");
                deadline = Time.realtimeSinceStartup + 30f;
                while (manager.GetLevelState() != LevelState.Running && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(manager.GetLevelState(), Is.EqualTo(LevelState.Running));
                yield return null; yield return null;
                var context = manager.GetCurrentContext();
                Assert.That(context, Is.Not.Null.And.Not.SameAs(previousContext));
                var hud = Object.FindObjectOfType<BattleHudView>();
                var pages = hud.GetComponent<ReferenceUiView>();
                if (phase == LevelState.Paused)
                {
                    yield return ClickButton(hud, "PauseButton");
                    Assert.That(pages.CurrentPage, Is.EqualTo(ReferenceUiView.Page.Settings));
                    Assert.That(manager.GetLevelState(), Is.EqualTo(LevelState.Paused));
                    yield return ClickButton(hud, "SettingsBackButton");
                    Assert.That(pages.IsOpen, Is.False);
                    Assert.That(manager.GetLevelState(), Is.EqualTo(LevelState.Running), "Continue must resume the game.");
                    // Exercise both the settings entry and the separate pause screen.
                    if (previousContext == null) yield return ClickButton(hud, "PauseButton");
                    else manager.RequestPause();
                }
                else
                    Assert.That(manager.ReportResult(phase == LevelState.Succeeded ? LevelResult.Succeeded : LevelResult.Failed), Is.True);
                yield return null; yield return null;
                var returnName = phase != LevelState.Paused ? "ResultReturnToMenuButton" :
                    previousContext == null ? "SettingsReturnToMenuButton" : "PauseReturnToMenuButton";
                var requested = 0;
                System.Action<ReturnToPreviousFlowRequestedEvent> onReturn = _ => requested++;
                manager.ReturnToPreviousFlowRequested += onReturn;
                yield return ClickButton(hud, returnName);
                manager.RequestReturnToPreviousFlow(); // A second request during/after return must be ignored.
                deadline = Time.realtimeSinceStartup + 20f;
                while ((SceneManager.GetActiveScene().name != "MainMenu" || manager.GetLevelState() != LevelState.Unloaded) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                manager.ReturnToPreviousFlowRequested -= onReturn;
                Assert.That(requested, Is.EqualTo(1));
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(manager.GetLevelState(), Is.EqualTo(LevelState.Unloaded));
                Assert.That(manager.GetCurrentLevelId().IsValid, Is.False);
                Assert.That(manager.GetCurrentContext(), Is.Null);
                Assert.That(context.GetLevelState(), Is.EqualTo(LevelState.Unloaded));
                Assert.That(context.MechanismSystem, Is.Null, "Old mechanisms must be disposed before showing the menu.");
                Assert.That(Object.FindObjectsOfType<GameRoot>(), Has.Length.EqualTo(1));
                Assert.That(Object.FindObjectOfType<GameRoot>(), Is.SameAs(gameRoot));
                Assert.That(Object.FindObjectsOfType<FirstLevelPresentationRoot>(), Is.Empty);
                Assert.That(Object.FindObjectsOfType<BattleHudView>(), Is.Empty);
                previousContext = context;
            }
        }

        private IEnumerator ClickButton(Component owner, string name)
        {
            var button = owner.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, name);
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, button.transform.position));
        }

        private IEnumerator Click(Vector2 position)
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
        }

        private static void Capture(Camera camera, GameObject owner, string path)
        {
            var canvases = owner.GetComponentsInChildren<Canvas>(true).Where(canvas => canvas.isRootCanvas).ToArray();
            var modes = canvases.Select(canvas => canvas.renderMode).ToArray();
            var cameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
            var distances = canvases.Select(canvas => canvas.planeDistance).ToArray();
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                foreach (var canvas in canvases)
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f; }
                Canvas.ForceUpdateCanvases();
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                for (var i = 0; i < canvases.Length; i++)
                { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
                camera.targetTexture = previousTarget; RenderTexture.active = previous;
                target.Release(); Object.Destroy(target); Object.Destroy(texture);
                Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (gameRoot != null) { gameRoot.GetLevelManager().Dispose(); Object.Destroy(gameRoot.gameObject); }
            if (mouse != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            if (keyboard != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
            if (existingPointers != null)
                foreach (var pointer in existingPointers) UnityEngine.InputSystem.InputSystem.EnableDevice(pointer);
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            settings.editorInputBehaviorInPlayMode = previousInputBehavior;
            settings.backgroundBehavior = previousBackgroundBehavior;
            yield return null;
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "FirstLevel" || scene.name == "MainMenu" || scene.name == "Bootstrap")
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("RemovalTestCleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
