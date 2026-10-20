#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player;
using SlimeBattle.Player.Placement;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.Status;
using SlimeBattle.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SlimeBattle.Tests.PlayerPresentation
{
    public sealed class GameplayFeedbackScenePlayModeTests
    {
        private GameRoot gameRoot;
        private LevelManager manager;
        private FirstLevelPresentationRoot player;
        private LevelContext context;
        private GameplayFeedbackView feedback;
        private Camera camera;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            var deadline = Time.realtimeSinceStartup + 25f;
            while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup < deadline) yield return null;
            gameRoot = Object.FindObjectOfType<GameRoot>();
            Assert.IsNotNull(gameRoot);
            manager = gameRoot.GetLevelManager();
            var error = string.Empty;
            manager.LevelLoadFailed += failure => error = failure.Reason;
            Assert.That(manager.StartNewCampaign(), Is.True);
            gameRoot.RequestEnterLevel(new LevelId("FirstLevel"));
            deadline = Time.realtimeSinceStartup + 30f;
            while (manager.GetLevelState() != LevelState.Running && manager.GetLevelState() != LevelState.Failed &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(manager.GetLevelState(), Is.EqualTo(LevelState.Running), error);
            yield return null; yield return null;
            player = Object.FindObjectOfType<FirstLevelPresentationRoot>();
            context = manager.GetCurrentContext();
            feedback = player.GetComponent<GameplayFeedbackView>();
            camera = player.GetComponentInChildren<Camera>();
            Assert.IsNotNull(feedback);
            // TEST_ONLY: freeze simulation while verifying view geometry and status lifecycle.
            Time.timeScale = 0f;
        }

        [UnityTest]
        public IEnumerator RangesMatchGameplayRadiusAndDisappearWithTheirSource()
        {
            var food = Place(PlacementItemId.Food);
            var furnace = Place(PlacementItemId.Furnace);
            player.Placement.CancelPlacement();
            yield return new WaitForSecondsRealtime(.15f);
            Canvas.ForceUpdateCanvases();
            var ppu = context.SlimeManager.GetAliveSnapshots()[0].PixelsPerUnit;
            foreach (var id in new[] { food, furnace })
            {
                var source = context.MechanismSystem.GetGuideSources().Single(s => s.MechanismId == id);
                var ring = feedback.GetComponentsInChildren<GuideRangeGraphic>().Single(g => g.name == "GuideRange_" + id.Value);
                var expected = Vector2.Distance(camera.WorldToScreenPoint(source.Position),
                    camera.WorldToScreenPoint(source.Position + Vector2.right * (source.AttractionRadiusPixels / ppu))) * 2f;
                Assert.That(ring.rectTransform.rect.width * ring.canvas.scaleFactor, Is.EqualTo(expected).Within(1f));
                Assert.That(ring.canvasRenderer, Is.Not.Null);
                Assert.That(ring.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0));
            }
            var overlay = feedback.transform.Find("GameplayFeedbackCanvas");
            Assert.That(overlay.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), Is.True);
            Assert.That(overlay.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
            Assert.That(context.MechanismSystem.TryGetSnapshot(food, out var placed), Is.True);
            player.Placement.BeginRemoval(); player.Placement.PreviewPlacement(placed.Position);
            Assert.That(player.Placement.ConfirmRemoval().Succeeded, Is.True);
            player.Placement.CancelPlacement();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(feedback.GetComponentsInChildren<GuideRangeGraphic>().Any(g => g.name == "GuideRange_" + food.Value), Is.False);
            player.Placement.BeginPlacement(PlacementItemId.Food);
            player.Placement.PreviewPlacement(placed.Position);
            yield return null; yield return null;
            Assert.That(feedback.GetComponentsInChildren<GuideRangeGraphic>().Any(g => g.name == "GuideRange_Preview"), Is.True);
            player.Placement.CancelPlacement(); yield return null; yield return null;
            Assert.That(feedback.GetComponentsInChildren<GuideRangeGraphic>().Any(g => g.name == "GuideRange_Preview"), Is.False);
        }

        [UnityTest]
        public IEnumerator AllThreeStatesAppearFollowCameraAndClearWithoutChangingRules()
        {
            var slimes = context.SlimeManager.GetAliveSnapshots().Take(3).ToArray();
            Assert.That(slimes.Length, Is.EqualTo(3));
            var statuses = new[] { "Hungry", "Agitated", "Afraid" };
            var words = new[] { "饥饿", "暴躁", "害怕" };
            for (var i = 0; i < 3; i++)
                Assert.That(context.StatusManager.ApplyStatus(new StatusApplicationRequest(slimes[i].ActorId,
                    slimes[i].ActorId, new StatusId(statuses[i]), 30f)), Is.True);
            var focus = (slimes[0].Position + slimes[1].Position + slimes[2].Position) / 3f;
            player.GetComponentInChildren<CameraSystem>().SubmitCommand(new CameraCommand(CameraCommandKind.Focus, focus));
            Place(PlacementItemId.Food); Place(PlacementItemId.Furnace);
            player.Placement.CancelPlacement();
            yield return new WaitForSecondsRealtime(.15f);
            for (var i = 0; i < 3; i++)
            {
                Assert.That(Bubble(slimes[i].ActorId).GetComponentInChildren<Text>().text, Is.EqualTo(words[i]));
                Assert.That(Bubble(slimes[i].ActorId).GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(Bubble(slimes[i].ActorId).GetComponent<CanvasRenderer>().GetMesh().vertexCount, Is.GreaterThan(0));
            }
            Assert.That(feedback.VisibleBubbleCount, Is.EqualTo(3));
            var before = Bubble(slimes[0].ActorId).anchoredPosition;
            player.GetComponentInChildren<CameraSystem>().SubmitCommand(new CameraCommand(CameraCommandKind.Pan, new Vector2(.6f, 0f)));
            yield return null; yield return null;
            Assert.That(Bubble(slimes[0].ActorId).anchoredPosition.x, Is.Not.EqualTo(before.x));
            var range = feedback.GetComponentsInChildren<GuideRangeGraphic>()[0].rectTransform;
            var beforeZoom = range.rect.width;
            player.GetComponentInChildren<CameraSystem>().SubmitCommand(new CameraCommand(CameraCommandKind.Zoom, Vector2.zero, 4f));
            yield return null; yield return null;
            Assert.That(range.rect.width, Is.LessThan(beforeZoom));
            var args = System.Environment.GetCommandLineArgs();
            var capture = System.Array.IndexOf(args, "-feedbackScreenshot");
            if (capture >= 0 && capture + 1 < args.Length) Capture(args[capture + 1]);

            Assert.That(context.StatusManager.ApplyStatus(new StatusApplicationRequest(slimes[0].ActorId,
                slimes[0].ActorId, new StatusId("Agitated"), 30f)), Is.True);
            yield return null; yield return null;
            Assert.That(Bubble(slimes[0].ActorId).GetComponentInChildren<Text>().text, Is.EqualTo("饥饿 · 暴躁"));
            for (var i = 0; i < 3; i++)
                Assert.That(context.StatusManager.RemoveStatus(slimes[i].ActorId, new StatusId(statuses[i]), StatusRemoveReason.Dispelled), Is.True);
            Assert.That(context.StatusManager.RemoveStatus(slimes[0].ActorId, new StatusId("Agitated"), StatusRemoveReason.Dispelled), Is.True);
            yield return null; yield return null;
            Assert.That(feedback.VisibleBubbleCount, Is.Zero);
            Assert.That(context.GuardManager.GetAliveSnapshots().Count, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator RestartReusesOneOverlayAndDiscardsOldStateAndRanges()
        {
            var id = Place(PlacementItemId.Food);
            var slime = context.SlimeManager.GetAliveSnapshots()[0];
            Assert.That(context.StatusManager.ApplyStatus(new StatusApplicationRequest(slime.ActorId,
                slime.ActorId, new StatusId("Hungry"), 30f)), Is.True);
            yield return null; yield return null;
            var oldContext = context;
            manager.RequestRestartCurrentLevel();
            var deadline = Time.realtimeSinceStartup + 20f;
            while ((manager.GetCurrentContext() == oldContext || manager.GetLevelState() != LevelState.Running) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; yield return null;
            player = Object.FindObjectOfType<FirstLevelPresentationRoot>();
            feedback = player.GetComponent<GameplayFeedbackView>();
            Assert.That(player.GetComponentsInChildren<Canvas>(true).Count(c => c.name == "GameplayFeedbackCanvas"), Is.EqualTo(1));
            Assert.That(feedback.VisibleBubbleCount, Is.Zero);
            Assert.That(feedback.GetComponentsInChildren<GuideRangeGraphic>().Any(g => g.name == "GuideRange_" + id.Value), Is.False);
            Assert.That(player.Placement.GetBudgetSnapshot().FoodRemaining, Is.EqualTo(60));
        }

        private RectTransform Bubble(ActorId id) => (RectTransform)feedback.GetComponentsInChildren<SlimeSpeechBubbleGraphic>()
            .Single(g => g.name == "SlimeStateBubble_" + id).transform;

        private MechanismId Place(PlacementItemId item)
        {
            var kind = item == PlacementItemId.Food ? MechanismKind.Food : MechanismKind.Furnace;
            Assert.That(context.MechanismSystem.TryGetPlayerConfigId(kind, out var configId), Is.True);
            Assert.That(context.MapManager.TryGetMapBounds(out var bounds), Is.True);
            var grid = player.GetComponentInChildren<PlacementGridAuthoring>();
            grid.GetCoveredRange(bounds, out var min, out var max);
            player.Placement.BeginPlacement(item);
            for (var y = min.y; y <= max.y; y++)
                for (var x = min.x; x <= max.x; x++)
                {
                    var position = grid.GetCellCenter(new Vector3Int(x, y, 0));
                    var viewport = camera.WorldToViewportPoint(position);
                    if (viewport.x < .15f || viewport.x > .62f || viewport.y < .22f || viewport.y > .70f) continue;
                    if (!context.MechanismSystem.CanPlace(configId, position, out _) ||
                        !player.Placement.PreviewPlacement(position).IsValid) continue;
                    var target = camera.WorldToViewportPoint(player.Placement.GetCurrentPreview().WorldCenter);
                    if (target.x < .28f || target.x > .62f || target.y < .30f || target.y > .68f) continue;
                    var result = player.Placement.ConfirmPlacement();
                    if (result.Succeeded) return result.MechanismId;
                }
            Assert.Fail("The production viewport must provide a legal cell for " + item);
            return default;
        }

        private void Capture(string path)
        {
            var canvases = player.GetComponentsInChildren<Canvas>(true).Where(c => c.isRootCanvas).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var cameras = canvases.Select(c => c.worldCamera).ToArray();
            var distances = canvases.Select(c => c.planeDistance).ToArray();
            var target = new RenderTexture(1280, 720, 24);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1f; }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                for (var i = 0; i < canvases.Length; i++)
                { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                target.Release(); Object.Destroy(target); Object.Destroy(texture);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (gameRoot != null) { manager.Dispose(); Object.Destroy(gameRoot.gameObject); }
            yield return null;
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "FirstLevel" || scene.name == "MainMenu" || scene.name == "Bootstrap")
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("FeedbackTestCleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
