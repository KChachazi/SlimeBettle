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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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

        [UnityTest] public IEnumerator HoverHelpHidesOnPressExitAndPauseWithoutConsumingToolbarClicks()
        {
            var settings=UnityEngine.InputSystem.InputSystem.settings;
            var previousBehavior=settings.editorInputBehaviorInPlayMode;
            var previousBackground=settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var pointers=UnityEngine.InputSystem.InputSystem.devices.OfType<Pointer>().Where(p=>p.enabled).ToArray();
            foreach(var pointer in pointers) UnityEngine.InputSystem.InputSystem.DisableDevice(pointer);
            var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>();
            try
            {
                foreach(var name in new[]{"FoodButton","FurnaceButton","ObstacleButton","RemoveButton"})
                {
                    var button=Button(name);
                    var tooltip=button.GetComponent<UiHoverTooltip>();
                    Assert.IsNotNull(tooltip,name);
                    var point=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new MouseState { position=point });
                    yield return null; yield return null;
                    Assert.That(tooltip.IsVisible,Is.True,name+" should show help on actual mouse hover.");
                    var data=new SerializedObject(tooltip);
                    var card=(RectTransform)data.FindProperty("tooltip").objectReferenceValue;
                    var body=(Text)data.FindProperty("body").objectReferenceValue;
                    Assert.That(body.preferredHeight,Is.LessThanOrEqualTo(body.rectTransform.rect.height+1f));
                    foreach(var text in card.GetComponentsInChildren<Text>())
                        Assert.That(text.cachedTextGenerator.characterCountVisible,Is.GreaterThan(0),text.name);
                    var corners=new Vector3[4]; card.GetWorldCorners(corners);
                    foreach(var corner in corners)
                    {
                        var screen=RectTransformUtility.WorldToScreenPoint(null,corner);
                        Assert.That(screen.x,Is.InRange(0f,(float)Screen.width),name);
                        Assert.That(screen.y,Is.InRange(0f,(float)Screen.height),name);
                    }
                    var hits=new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position=point },hits);
                    Assert.That(hits.Any(hit=>hit.gameObject.transform.IsChildOf(card)),Is.False);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new MouseState { position=point }.WithButton(MouseButton.Left));
                    yield return null; yield return null;
                    Assert.That(tooltip.IsVisible,Is.False,name+" must hide as soon as the pointer is pressed.");
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new MouseState { position=point });
                    yield return null; yield return null;
                    Assert.That(tooltip.IsVisible,Is.False,"A stationary pointer must not reopen help after clicking.");
                    Assert.That(fixture.Backend.Requests,Is.Zero,"A toolbar click must not place an object on the map.");
                    if(name=="RemoveButton") Assert.That(fixture.Placement.IsRemoving,Is.True);
                    else Assert.That(fixture.Placement.SelectedItem.ToString(),Is.EqualTo(name.Replace("Button","")));
                    var outside=new Vector2(Screen.width*0.4f,Screen.height*0.55f);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new MouseState { position=outside });
                    yield return null; yield return null;
                    Assert.That(tooltip.IsVisible,Is.False);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new MouseState { position=point });
                    yield return null; yield return null;
                    Assert.That(tooltip.IsVisible,Is.True,"Leaving and re-entering must show help again.");
                    fixture.SetPhase(LevelState.Paused); yield return null;
                    Assert.That(owner.GetComponentsInChildren<UiHoverTooltip>(true).All(t=>!t.IsVisible),Is.True);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new MouseState { position=outside });
                    yield return null; yield return null;
                    fixture.Restart(); yield return null;
                    Assert.That(owner.GetComponentsInChildren<UiHoverTooltip>(true).All(t=>!t.IsVisible),Is.True);
                }
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                foreach(var pointer in pointers) UnityEngine.InputSystem.InputSystem.EnableDevice(pointer);
                settings.editorInputBehaviorInPlayMode=previousBehavior;
                settings.backgroundBehavior=previousBackground;
            }
        }

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
        [UnityTest] public IEnumerator RemoveButtonTargetsPlacedObjectsAndClearsOnPauseAndRestart()
        {
            Button("FoodButton").onClick.Invoke();
            fixture.Placement.PreviewPlacement(new Vector3(-1.2f, 0.3f));
            Assert.That(fixture.Placement.ConfirmPlacement().Succeeded, Is.True);
            Button("RemoveButton").onClick.Invoke(); yield return null;
            Assert.That(fixture.Placement.IsRemoving, Is.True);
            Assert.That(fixture.GridView.IsVisible, Is.True);
            Assert.That(Button("CancelButton").GetComponentInChildren<Text>().text, Is.EqualTo("取消拆除"));
            fixture.Placement.PreviewPlacement(new Vector3(-1.2f, 0.3f));
            Assert.That(fixture.Placement.ConfirmRemoval().Succeeded, Is.True);
            Assert.That(fixture.Backend.ReadPlacedObjects(), Is.Empty);
            Button("RemoveButton").onClick.Invoke(); yield return null;
            Assert.That(fixture.Placement.IsInteracting, Is.False);
            Button("RemoveButton").onClick.Invoke();
            Button("CancelButton").onClick.Invoke(); yield return null;
            Assert.That(fixture.Placement.IsInteracting, Is.False);
            Button("RemoveButton").onClick.Invoke();
            fixture.SetPhase(LevelState.Paused); yield return null;
            Assert.That(fixture.Placement.IsRemoving, Is.False);
            Assert.That(fixture.GridView.IsVisible, Is.False);
            Assert.That(Button("RemoveButton").interactable, Is.False);
            fixture.Restart(); Button("RemoveButton").onClick.Invoke();
            Assert.That(fixture.Placement.IsRemoving, Is.True);
            fixture.Restart(); yield return null;
            Assert.That(fixture.Placement.IsRemoving, Is.False);
        }

        [UnityTest] public IEnumerator RemovalInputBlocksToolbarClicksAndRemovesOnMapClick()
        {
            var position = new Vector3(-1.2f, 0.3f);
            Button("FoodButton").onClick.Invoke(); fixture.Placement.PreviewPlacement(position);
            Assert.That(fixture.Placement.ConfirmPlacement().Succeeded, Is.True);
            Button("RemoveButton").onClick.Invoke();
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            var previousInputBehavior = settings.editorInputBehaviorInPlayMode;
            var previousBackgroundBehavior = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var existingPointers = UnityEngine.InputSystem.InputSystem.devices.OfType<Pointer>().Where(device => device.enabled).ToArray();
            foreach (var pointer in existingPointers) UnityEngine.InputSystem.InputSystem.DisableDevice(pointer);
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>();
            try
            {
                var uiPosition = RectTransformUtility.WorldToScreenPoint(null, Button("RemoveButton").transform.position);
                // Movement alone must never remove. A world click behind the toolbar must be blocked.
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = uiPosition });
                yield return null; yield return null;
                Assert.That(fixture.Backend.ReadPlacedObjects(), Has.Count.EqualTo(1));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                    new MouseState { position = uiPosition }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = uiPosition });
                yield return null; yield return null;
                Assert.That(fixture.Backend.RemovalRequests, Is.Zero, "Toolbar clicks must never submit a world removal.");
                if (!fixture.Placement.IsRemoving) Button("RemoveButton").onClick.Invoke();
                var mapPosition = fixture.Camera.WorldToScreen(position);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = mapPosition });
                yield return null; yield return null;
                Assert.That(mouse.position.ReadValue(), Is.EqualTo(mapPosition), "Queued mouse state must reach the Input System.");
                Assert.That(fixture.Input.ReadSnapshot().PointerPosition, Is.EqualTo(mapPosition), "Gameplay input must use the test pointer.");
                Assert.That(fixture.Placement.GetCurrentPreview().Status, Is.EqualTo(PlacementValidationStatus.Removable));
                Assert.That(fixture.Backend.RemovalRequests, Is.Zero);
                // A left-button drag over a removable object must never remove it.
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                    new MouseState { position = mapPosition }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                    new MouseState { position = mapPosition + new Vector2(40f, 20f) }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                    new MouseState { position = mapPosition + new Vector2(40f, 20f) });
                yield return null; yield return null;
                Assert.That(fixture.Backend.RemovalRequests, Is.Zero);
                Assert.That(fixture.Backend.ReadPlacedObjects(), Has.Count.EqualTo(1));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                    new MouseState { position = mapPosition }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = mapPosition });
                yield return null; yield return null;
                Assert.That(fixture.Backend.RemovalRequests, Is.EqualTo(1));
                Assert.That(fixture.Backend.ReadPlacedObjects(), Is.Empty);
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                foreach (var pointer in existingPointers) UnityEngine.InputSystem.InputSystem.EnableDevice(pointer);
                settings.editorInputBehaviorInPlayMode = previousInputBehavior;
                settings.backgroundBehavior = previousBackgroundBehavior;
            }
        }
        [UnityTest] public IEnumerator AnchorViewsSnapToExactMapPositionAndClearOnCancelAndPause()
        {
            Button("ObstacleButton").onClick.Invoke(); yield return null;
            Assert.That(fixture.GridView.VisibleAnchorCount,Is.EqualTo(2));
            fixture.Placement.PreviewPlacement(new Vector3(-3f,1.8f));
            var preview=fixture.Placement.GetCurrentPreview();
            Assert.That(preview.Status,Is.EqualTo(PlacementValidationStatus.AnchorFree));
            Assert.That(preview.WorldCenter,Is.EqualTo(new Vector3(-3.2f,1.7f)));
            var visual=GameObject.Find("PlacementAnchor_L1");
            Assert.That(visual.transform.position,Is.EqualTo(preview.WorldCenter));
            Assert.That(fixture.Placement.ConfirmPlacement().Succeeded,Is.True);
            yield return null;
            Assert.That(fixture.GridView.VisibleAnchorCount,Is.Zero);
            Button("ObstacleButton").onClick.Invoke(); fixture.Placement.PreviewPlacement(new Vector3(-3f,1.8f));
            Assert.That(fixture.Placement.GetCurrentPreview().Status,Is.EqualTo(PlacementValidationStatus.ReplacementUnavailable));
            Assert.That(fixture.Placement.ConfirmPlacement().Succeeded,Is.False);
            Assert.That(fixture.Backend.ReadPlacedObjects().Count,Is.EqualTo(1));
            fixture.SetPhase(LevelState.Paused); yield return null;
            Assert.That(fixture.GridView.VisibleAnchorCount,Is.Zero);
            Assert.That(GameObject.Find("PlacementAnchor_L1"),Is.Null);
            fixture.Restart(); Button("FoodButton").onClick.Invoke();
            fixture.Placement.PreviewPlacement(new Vector3(-3f,1.8f));
            Assert.That(fixture.Placement.GetCurrentPreview().Status,Is.EqualTo(PlacementValidationStatus.TypeMismatch));
            Button("CancelButton").onClick.Invoke(); yield return null;
            Assert.That(fixture.GridView.VisibleAnchorCount,Is.Zero);
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
            foreach(var item in new[]{"FoodButton","FurnaceButton","ObstacleButton"})
                Assert.That(Button(item).GetComponentsInChildren<Text>(true).Single(t=>t.name=="Remaining").text,
                    Is.EqualTo("剩余 --"),"Missing inventory must not be presented as zero stock.");
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
        [UnityTest] public IEnumerator ReferencePagesPauseAndReturnWithoutPlacingOrResumingAnExistingPause()
        {
            var pages=fixture.Hud.GetComponent<ReferenceUiView>();
            foreach(var entry in new[]{("PauseButton","SettingsBackButton",ReferenceUiView.Page.Settings)})
            {
                Button("FoodButton").onClick.Invoke();
                Button(entry.Item1).onClick.Invoke(); yield return null;
                Assert.That(pages.CurrentPage,Is.EqualTo(entry.Item3));
                Assert.That(fixture.Phase,Is.EqualTo(LevelState.Paused));
                Assert.That(fixture.Placement.IsPlacing,Is.False);
                Assert.That(fixture.Backend.Requests,Is.Zero);
                var hits=new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position=new Vector2(Screen.width*0.5f,Screen.height*0.5f) },hits);
                Assert.That(hits.Any(h=>h.module is GraphicRaycaster),Is.True);
                Button(entry.Item2).onClick.Invoke(); yield return null;
                Assert.That(pages.IsOpen,Is.False);
                Assert.That(fixture.Phase,Is.EqualTo(LevelState.Running));
            }
            fixture.SetPhase(LevelState.Paused); pages.OpenSettings();
            pages.OpenVolume(); pages.BackToSettings(); pages.Close(); yield return null;
            Assert.That(fixture.Phase,Is.EqualTo(LevelState.Paused),"Closing settings must preserve a preexisting pause.");
            fixture.SetPhase(LevelState.Succeeded); yield return null;
            Assert.That(pages.IsOpen,Is.False,"An authoritative result must clear auxiliary pages.");
            pages.OpenSettings();
            Assert.That(pages.IsOpen,Is.False,"Auxiliary pages must not cover a result or loading transition.");
        }
    }

}
#endif
