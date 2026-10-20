using System.Linq;
using NUnit.Framework;
using SlimeBattle.Core;
using SlimeBattle.Player;
using SlimeBattle.Player.Placement;
using SlimeBattle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.Tests.PlayerPresentation
{
    public sealed class FirstLevelUiEditModeTests
    {
        private const string Folder="Assets/Game/UI/Prefabs/";
        private GameObject instance;
        [TearDown] public void TearDown() { if(instance!=null) Object.DestroyImmediate(instance); }

        [TestCase("PF_FirstLevelHUD.prefab")]
        [TestCase("PF_FirstLevelPlacementSelection.prefab")]
        [TestCase("PF_FirstLevelPlayer.prefab")]
        public void PrefabsHaveNoMissingScriptsAndUseBundledChineseFont(string file)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+file); Assert.IsNotNull(prefab,file);
            foreach(var transform in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),Is.Zero,transform.name);
            foreach(var label in prefab.GetComponentsInChildren<Text>(true))
            {
                Assert.IsNotNull(label.font,label.name);
                Assert.That(AssetDatabase.GetAssetPath(label.font),Does.StartWith("Assets/Game/UI/Fonts/"));
                Assert.That(label.font.HasCharacter('设'),Is.True,"Bundled font must cover Chinese UI labels.");
                Assert.That(label.raycastTarget,Is.False,label.name);
            }
            foreach(var icon in prefab.GetComponentsInChildren<FirstLevelUiIcon>(true))
                Assert.IsNotNull(icon.GetComponent<CanvasRenderer>(),icon.name);
        }
        [Test]
        public void HudMissingDataDoesNotBecomeZeroOrDeclareAResult()
        {
            var hud=InstantiateHud();
            hud.Render(new UiState(default,LevelState.Running,"测试",0,weaponText:"weapon destroyed"),true,false,false,false);
            Assert.That(Label(hud,"Counts").text,Is.EqualTo("0 / --"));
            foreach(var removed in new[]{"LegacyWeapon","Goal","WaitingWindow","RouteHint"})
                Assert.That(hud.GetComponentsInChildren<Transform>(true).Any(t=>t.name==removed),Is.False,removed);
            Assert.That(Find(hud,"ResultPanel").activeSelf,Is.False);
            Assert.That(new UiState(default,LevelState.Running,"old constructor").SlimeCount,Is.EqualTo(-1));
        }
        [Test]
        public void MissionOnlyShowsVictoryConditionAndResultFollowsPhase()
        {
            var hud=InstantiateHud();
            hud.Render(new UiState(default,LevelState.Failed,"objective",12,slimeLimit:20,
                goalText:"终点 2 / 6",waitingText:"剩余 18 秒",routeHint:"L1 类型不匹配",resultText:"公开结果原因"),true,false,true,false);
            var mission=Find(hud,"MissionCard");
            Assert.That(mission.GetComponentsInChildren<Text>(),Has.Length.EqualTo(1));
            hud.Render(new UiState(default,LevelState.Failed,"Escort the weapon to the goal.",12,
                goalText:"终点 2 / 6",waitingText:"剩余 18 秒"),true,false,true,false);
            Assert.That(Label(hud,"Objective").text,Is.EqualTo("胜利条件：护送箱子到达终点"));
            Assert.That(Label(hud,"Objective").fontSize,Is.GreaterThanOrEqualTo(22));
            Assert.That(Label(hud,"Result").text,Does.StartWith("再试一次吧"));
            Assert.That(Label(hud,"ResultCounts").text,Does.Contain("终点 2 / 6"));
            hud.Render(new UiState(default,LevelState.Succeeded,"objective"),true,false,true,true);
            Assert.That(Label(hud,"Result").text,Does.StartWith("引导成功"));
            Assert.That(Find(hud,"ResultPanel").GetComponent<Canvas>().sortingOrder,Is.GreaterThan(21));
            Assert.That(Find(hud,"ResultPanel").GetComponentsInChildren<Button>().All(b=>!b.interactable),Is.True);
            var reason=string.Concat(Enumerable.Repeat("公开结果原因需要完整保留。",80));
            hud.Render(new UiState(default,LevelState.Failed,"objective",resultText:reason),true,false,true,false);
            var scroll=hud.GetComponentsInChildren<ScrollRect>(true).Single();
            Assert.That(scroll.GetComponent<Image>().raycastTarget,Is.True);
            Assert.That(scroll.content.GetComponent<Text>().text,Is.EqualTo(reason));
            Assert.That(scroll.content.GetComponent<ContentSizeFitter>().verticalFit,Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
        }
        [Test]
        public void InspectorBindingsAreCompleteAndPlayerHasOneOfEachView()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PF_FirstLevelPlayer.prefab");
            var first=new SerializedObject(prefab.GetComponent<FirstLevelPresentationRoot>());
            foreach(var field in new[]{"cameraSystem","grid","placementBackend","statusSnapshotSource","selectionInstance"})
                Assert.IsNotNull(first.FindProperty(field).objectReferenceValue,field);
            Assert.That(first.FindProperty("placementBackend").objectReferenceValue,Is.InstanceOf<IPlacementBackend>());
            Assert.That(prefab.GetComponentsInChildren<BattleHudView>(true).Length,Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<PlacementSelectionView>(true).Length,Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<PlacementInputAdapter>(true).Length,Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<PlacementGridView>(true).Length,Is.EqualTo(1));
            var hud=new SerializedObject(prefab.GetComponentInChildren<BattleHudView>(true));
            foreach(var field in new[]{"hudPanel","pausePanel","resultPanel","objectiveText","countsText","resultText","pauseButton","resumeButton","pauseRestartButton","resultRestartButton"})
                Assert.IsNotNull(hud.FindProperty(field).objectReferenceValue,field);
        }

        [TestCase("PF_FirstLevelPlacementSelection.prefab")]
        [TestCase("PF_FirstLevelHUD.prefab")]
        public void FirstLevelScreenSpaceCanvasRootsKeepScreenSpaceInteractionEnabled(string file)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+file);
            Assert.IsNotNull(prefab,file);
            Assert.That(prefab.transform.localScale,Is.EqualTo(Vector3.one),
                "The screen-space canvas must not collapse its UI hit area: "+file);
        }
        [Test]
        public void ReinitializingCompositionReusesInspectorComponentsAndEmbeddedViews()
        {
            instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PF_FirstLevelPlayer.prefab"));
            var managerObject=new GameObject("TEST_ONLY_Manager",typeof(LevelManager)); managerObject.transform.SetParent(instance.transform);
            var manager=managerObject.GetComponent<LevelManager>();
            var first=instance.GetComponent<FirstLevelPresentationRoot>();
            using(var backend=new TestPlacementBackend())
            {
                for(var i=0;i<2;i++) first.Initialize(manager,instance.GetComponent<SlimeBattle.Player.Input.InputSystem>(),
                    instance.GetComponentInChildren<SlimeBattle.Presentation.Camera.CameraSystem>(),instance.GetComponentInChildren<PlacementGridAuthoring>(),
                    backend,PlacementConfirmationMode.Single);
                Assert.That(instance.GetComponents<PlacementInputAdapter>().Length,Is.EqualTo(1));
                Assert.That(instance.GetComponents<PlacementGridView>().Length,Is.EqualTo(1));
                Assert.That(instance.GetComponentsInChildren<BattleHudView>(true).Length,Is.EqualTo(1));
                Assert.That(instance.GetComponentsInChildren<PlacementSelectionView>(true).Length,Is.EqualTo(1));
            }
        }
        private BattleHudView InstantiateHud()
        { instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PF_FirstLevelHUD.prefab")); return instance.GetComponent<BattleHudView>(); }
        private static Text Label(Component root,string name)=>root.GetComponentsInChildren<Text>(true).Single(t=>t.name==name);
        private static GameObject Find(Component root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name).gameObject;
        [Test]
        public void UnimplementedLevelsCannotBeStartedByTheSelectionButton()
        {
            instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PF_ReferenceMainMenu.prefab"));
            var menu=instance.GetComponent<ReferenceMainMenuView>(); menu.OpenLevels();
            var select=instance.GetComponentsInChildren<Button>(true).Single(b=>b.name=="SelectLevelButton");
            Assert.That(select.gameObject.activeSelf,Is.False);
            menu.SelectLevel(2); Assert.That(select.interactable,Is.False);
            menu.SelectLevel(1); Assert.That(select.interactable,Is.True,"Castle already has a production scene and entry.");
            menu.SelectLevel(0); Assert.That(select.interactable,Is.True);
            menu.Back(); Assert.That(Find(menu,"LevelSelection").activeSelf,Is.False);
        }

        [Test]
        public void HudContainsSettingsWithoutBagOrCatalogPages()
        {
            var hud=InstantiateHud();
            var names=hud.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
            foreach(var removed in new[]{"BagButton","BagPanel","CatalogButton","CatalogPanel"})
                Assert.That(names,Does.Not.Contain(removed));
            Assert.That(names,Does.Contain("SettingsPanel"));
            var pages=new SerializedObject(hud.GetComponent<ReferenceUiView>()).FindProperty("pages");
            Assert.That(pages.arraySize,Is.EqualTo(4));
            for(var i=0;i<pages.arraySize;i++) Assert.IsNotNull(pages.GetArrayElementAtIndex(i).objectReferenceValue);
        }

        [Test]
        public void MenuReturnCommandsUseTheCurrentBindingAndBlockPendingTransitions()
        {
            var hud=InstantiateHud(); var pages=hud.GetComponent<ReferenceUiView>();
            var received=0;
            pages.Initialize(command=>{ Assert.That(command,Is.EqualTo(UiCommand.ReturnToMainMenu)); received++; });
            foreach(var phase in new[]{LevelState.Paused,LevelState.Succeeded,LevelState.Failed})
            {
                hud.Render(new UiState(default,phase,""),true,phase==LevelState.Paused,phase!=LevelState.Paused,false);
                var name=phase==LevelState.Paused?"SettingsReturnToMenuButton":"ResultReturnToMenuButton";
                var button=Find(hud,name).GetComponent<Button>();
                Assert.That(button.interactable,Is.True);
                Assert.That(button.onClick.GetPersistentTarget(0),Is.SameAs(pages));
                Assert.That(button.onClick.GetPersistentMethodName(0),Is.EqualTo(nameof(ReferenceUiView.ReturnToMainMenu)));
                pages.ReturnToMainMenu();
                hud.Render(new UiState(default,phase,""),true,false,phase!=LevelState.Paused,true);
                Assert.That(button.interactable,Is.False);
                pages.ReturnToMainMenu();
            }
            Assert.That(received,Is.EqualTo(3),"Pending returns cannot submit another command.");
            hud.Unbind(); pages.SetTransitionPending(false); pages.ReturnToMainMenu();
            Assert.That(received,Is.EqualTo(3),"An unbound view must not retain the previous owner.");
        }

        [TestCase("PF_FirstLevelPlacementSelection.prefab","FoodButton")]
        [TestCase("PF_FirstLevelPlacementSelection.prefab","FurnaceButton")]
        [TestCase("PF_FirstLevelPlacementSelection.prefab","ObstacleButton")]
        [TestCase("PF_FirstLevelHUD.prefab","RemoveButton")]
        public void IllustratedButtonsKeepTheirHitAreasOnTheRightToolbar(string file,string name)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+file);
            var button=prefab.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
            var rect=(RectTransform)button.transform;
            Assert.IsNotNull(button.image.sprite);
            Assert.That(rect.anchorMin.x,Is.EqualTo(1));
            Assert.That(rect.anchoredPosition.x-rect.sizeDelta.x/2,Is.GreaterThan(-350),
                "Transparent export margins must not create a full-screen clickable button.");
            Assert.That(rect.sizeDelta.x,Is.LessThan(340));
            Assert.That(rect.sizeDelta.y,Is.LessThan(180));
        }

        [TestCase("PF_FirstLevelPlacementSelection.prefab",3)]
        [TestCase("PF_FirstLevelHUD.prefab",1)]
        public void TooltipsAreBoundHiddenAndNeverBlockPointerInput(string file,int count)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+file);
            var tooltips=prefab.GetComponentsInChildren<UiHoverTooltip>(true);
            Assert.That(tooltips,Has.Length.EqualTo(count));
            foreach(var tooltip in tooltips)
            {
                var data=new SerializedObject(tooltip);
                var card=(RectTransform)data.FindProperty("tooltip").objectReferenceValue;
                var body=(Text)data.FindProperty("body").objectReferenceValue;
                Assert.IsNotNull(card); Assert.IsNotNull(body);
                Assert.That(card.gameObject.activeSelf,Is.False);
                Assert.That(body.fontSize,Is.GreaterThanOrEqualTo(17));
                Assert.That(card.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
                Assert.That(card.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            }
            if(file.Contains("PlacementSelection"))
            {
                Assert.That(prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Budget"),Is.False);
                Assert.That(Label(prefab.transform,"Hint").text,Is.EqualTo(FirstLevelUiText.ControlsHint));
            }
        }

        [TestCase(1280,720)]
        [TestCase(1024,768)]
        [TestCase(1920,1080)]
        [TestCase(1920,820)]
        public void GuidanceStaysInsideViewportAndClearOfOtherControls(int width,int height)
        {
            instance=new GameObject("TEST_ONLY_GuidanceLayout");
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PF_FirstLevelHUD.prefab"),instance.transform);
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PF_FirstLevelPlacementSelection.prefab"),instance.transform);
            var cameraObject=new GameObject("TEST_ONLY_UiCamera",typeof(Camera));
            cameraObject.transform.SetParent(instance.transform);
            var camera=cameraObject.GetComponent<Camera>();
            var target=new RenderTexture(width,height,24);
            try
            {
                camera.targetTexture=target;
                foreach(var scaler in instance.GetComponentsInChildren<CanvasScaler>(true))
                {
                    var canvas=scaler.GetComponent<Canvas>();
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1f;
                    scaler.enabled=false; scaler.enabled=true;
                }
                Canvas.ForceUpdateCanvases(); camera.Render();
                var hint=Label(instance.transform,"Hint");
                var hintBounds=ScreenBounds(hint.rectTransform,camera);
                var cardBounds=ScreenBounds((RectTransform)hint.transform.parent,camera);
                Assert.That(cardBounds.center.x,Is.EqualTo(width/2f).Within(1f),"The panel must be centered in the viewport.");
                Assert.That(hintBounds.center.x,Is.EqualTo(cardBounds.center.x).Within(1f));
                Assert.That(hintBounds.center.y,Is.EqualTo(cardBounds.center.y).Within(1f));
                Assert.That(hint.alignment,Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(cardBounds.yMin,Is.GreaterThan(0));
                Assert.That(cardBounds.yMax,Is.LessThan(height*0.12f),"Keep the controls hint close to the bottom edge.");
                var mission=(RectTransform)Find(instance.transform,"MissionCard").transform;
                Assert.That(cardBounds.Overlaps(ScreenBounds(mission,camera)),Is.False,"The victory condition must remain readable.");
                var cancel=(RectTransform)Find(instance.transform,"CancelButton").transform;
                Assert.That(cardBounds.Overlaps(ScreenBounds(cancel,camera)),Is.False,"The cancel button must remain separate while placing.");
                foreach(var name in new[]{"FoodButton","FurnaceButton","ObstacleButton"})
                {
                    var button=Find(instance.transform,name).transform;
                    // Include the largest normal hover scale when checking the count badge.
                    button.localScale=Vector3.one*1.025f;
                    var badge=(RectTransform)button.Find("RemainingBadge");
                    var bounds=ScreenBounds(badge,camera);
                    Assert.That(bounds.xMin,Is.GreaterThan(0),name);
                    Assert.That(bounds.xMax,Is.LessThan(width-8f),name+" must leave room before the viewport edge.");
                    Assert.That(bounds.yMin,Is.GreaterThan(0),name);
                    Assert.That(bounds.yMax,Is.LessThan(height),name);
                }
                var objective=Label(instance.transform,"Objective");
                Assert.That(objective.cachedTextGenerator.fontSizeUsedForBestFit,Is.GreaterThanOrEqualTo(16),"Keep the rendered victory condition readable even at 1024 pixels wide.");
                Assert.That(objective.cachedTextGenerator.lineCount,Is.EqualTo(1),"The complete victory condition must fit on one line.");
                Assert.That(objective.cachedTextGenerator.characterCountVisible,Is.GreaterThanOrEqualTo(objective.text.Length));
            }
            finally { camera.targetTexture=null; target.Release(); Object.DestroyImmediate(target); }
        }

        private static Rect ScreenBounds(RectTransform rect,Camera camera)
        {
            var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
            var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
