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
            Assert.That(Label(hud,"Goal").text,Does.Contain("未接入"));
            Assert.That(Label(hud,"WaitingWindow").text,Does.Contain("未接入"));
            Assert.That(Find(hud,"ResultPanel").activeSelf,Is.False);
            Assert.That(new UiState(default,LevelState.Running,"old constructor").SlimeCount,Is.EqualTo(-1));
        }
        [Test]
        public void GoalWindowAndRouteDisplayAreReadOnlyAndResultFollowsPhase()
        {
            var hud=InstantiateHud();
            hud.Render(new UiState(default,LevelState.Failed,"objective",12,slimeLimit:20,
                goalText:"终点 2 / 6",waitingText:"剩余 18 秒",routeHint:"L1 类型不匹配",resultText:"公开结果原因"),true,false,true,false);
            Assert.That(Label(hud,"Goal").text,Is.EqualTo("终点 2 / 6"));
            Assert.That(Label(hud,"WaitingWindow").text,Is.EqualTo("剩余 18 秒"));
            Assert.That(Label(hud,"RouteHint").text,Is.EqualTo("L1 类型不匹配"));
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
            foreach(var field in new[]{"hudPanel","pausePanel","resultPanel","objectiveText","countsText","goalText","waitingText","routeHintText","resultText","pauseButton","resumeButton","pauseRestartButton","resultRestartButton"})
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
        public void CatalogPaginationClampsAndOnlyShowsTheCurrentContent()
        {
            var hud=InstantiateHud();
            var catalog=hud.GetComponentInChildren<TapTapCatalogView>(true);
            Assert.IsNotNull(catalog);
            var previous=hud.GetComponentsInChildren<Button>(true).Single(b=>b.name=="CatalogPrevious");
            var next=hud.GetComponentsInChildren<Button>(true).Single(b=>b.name=="CatalogNext");
            catalog.Previous(); Assert.That(catalog.PageIndex,Is.Zero); Assert.That(previous.interactable,Is.False);
            catalog.Next(); Assert.That(catalog.PageIndex,Is.EqualTo(1));
            Assert.That(Find(catalog,"CatalogContent1").activeSelf,Is.False);
            Assert.That(Find(catalog,"CatalogContent2").activeSelf,Is.True);
            catalog.Next(); catalog.Next(); Assert.That(catalog.PageIndex,Is.EqualTo(2));
            Assert.That(next.interactable,Is.False);
            catalog.Previous(); Assert.That(catalog.PageIndex,Is.EqualTo(1)); Assert.That(next.interactable,Is.True);
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
    }
}
