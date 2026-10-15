#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SlimeBattle.Core;
using SlimeBattle.Player.Placement;
using SlimeBattle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace SlimeBattle.Tests.PlayerPresentation
{
    public sealed class FirstLevelUiPlayModeTests
    {
        private GameObject owner;
        private FirstLevelUiFixture fixture;
        [UnitySetUp] public IEnumerator SetUp()
        {
            owner=new GameObject("TEST_ONLY_UI_PlayMode"); owner.SetActive(false);
            fixture=owner.AddComponent<FirstLevelUiFixture>();
            var so=new SerializedObject(fixture);
            so.FindProperty("hudPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<BattleHudView>("Assets/Game/UI/Prefabs/PF_FirstLevelHUD.prefab");
            so.FindProperty("selectionPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<PlacementSelectionView>("Assets/Game/UI/Prefabs/PF_FirstLevelPlacementSelection.prefab");
            so.ApplyModifiedPropertiesWithoutUndo(); owner.SetActive(true); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown() { Object.Destroy(owner); yield return null; }
        private Button Button(string name)=>owner.GetComponentsInChildren<Button>(true).First(b=>b.name==name);

        [UnityTest] public IEnumerator ThreeSelectionButtonsSubmitOnlyPlacementState()
        {
            foreach(var pair in new[]{("FoodButton",PlacementItemId.Food),("FurnaceButton",PlacementItemId.Furnace),("ObstacleButton",PlacementItemId.Obstacle)})
            {
                Button(pair.Item1).onClick.Invoke(); yield return null;
                Assert.That(fixture.Placement.SelectedItem,Is.EqualTo(pair.Item2));
                Assert.That(fixture.Backend.Requests,Is.Zero);
                Assert.That(fixture.GridView.IsVisible,Is.True);
            }
            Button("CancelButton").onClick.Invoke(); yield return null;
            Assert.That(fixture.Placement.IsPlacing,Is.False); Assert.That(fixture.GridView.IsVisible,Is.False);
        }
        [UnityTest] public IEnumerator RebindingAndReenablingDoNotDuplicateListeners()
        {
            fixture.Selection.Initialize(fixture.Placement); fixture.Selection.Initialize(fixture.Placement);
            fixture.Selection.enabled=false; fixture.Selection.enabled=true;
            var changes=0; fixture.Placement.Changed+=()=>changes++;
            Button("FoodButton").onClick.Invoke();
            Assert.That(changes,Is.EqualTo(2),"Cancel old selection and set new selection exactly once.");
            fixture.Selection.enabled=false; fixture.Placement.CancelPlacement();
            Button("FoodButton").onClick.Invoke(); yield return null;
            Assert.That(fixture.Placement.IsPlacing,Is.False);
            Assert.That(fixture.Selection.IsVisible,Is.False);
        }
        [UnityTest] public IEnumerator PauseResultsAndRestartClearPlacementAndPreserveSingleViews()
        {
            Button("FoodButton").onClick.Invoke(); fixture.Placement.PreviewPlacement(Vector3.zero);
            Assert.That(fixture.Placement.ConfirmPlacement().Succeeded,Is.True);
            Button("ObstacleButton").onClick.Invoke();
            Button("PauseButton").onClick.Invoke(); yield return null;
            Assert.That(fixture.Phase,Is.EqualTo(LevelState.Paused));
            Assert.That(fixture.Selection.IsVisible,Is.False); Assert.That(fixture.GridView.IsVisible,Is.False);
            Button("ResumeButton").onClick.Invoke(); yield return null;
            Assert.That(fixture.Selection.IsVisible,Is.True); Assert.That(fixture.Placement.IsPlacing,Is.False);
            foreach(var phase in new[]{LevelState.Succeeded,LevelState.Failed})
            {
                fixture.SetPhase(phase); yield return null;
                Assert.That(fixture.Selection.IsVisible,Is.False);
                Assert.That(fixture.Hud.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("TEST_ONLY · 模拟上游结果")),Is.True);
                fixture.Restart(); yield return null;
                Assert.That(fixture.Backend.ReadPlacedObjects().Count,Is.Zero);
                Assert.That(owner.GetComponentsInChildren<BattleHudView>(true).Length,Is.EqualTo(1));
                Assert.That(owner.GetComponentsInChildren<PlacementSelectionView>(true).Length,Is.EqualTo(1));
            }
        }
        [UnityTest] public IEnumerator ToolbarAndModalBlockRaycastsWhileMapRemainsClickable()
        {
            Canvas.ForceUpdateCanvases(); yield return null;
            var hits=new List<RaycastResult>();
            var pointer=new PointerEventData(EventSystem.current) { position=RectTransformUtility.WorldToScreenPoint(null,Button("FoodButton").transform.position) };
            EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits.Any(h=>h.module is GraphicRaycaster),Is.True,"Toolbar must block world clicks.");
            pointer.position=fixture.Camera.WorldToScreen(new Vector3(-2,1,0)); hits.Clear();
            EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits.Any(h=>h.module is GraphicRaycaster),Is.False,"Empty map must remain clickable.");
            fixture.SetPhase(LevelState.Paused); Canvas.ForceUpdateCanvases(); yield return null; hits.Clear();
            EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits.Any(h=>h.module is GraphicRaycaster),Is.True,"Modal must block the same map point.");
        }
    }
}
#endif
