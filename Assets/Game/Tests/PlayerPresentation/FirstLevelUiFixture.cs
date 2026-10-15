using System.Linq;
using SlimeBattle.Core;
using SlimeBattle.Player.Input;
using SlimeBattle.Player.Placement;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using SlimeBattle.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;

namespace SlimeBattle.Tests.PlayerPresentation
{
    /// <summary>TEST_ONLY UI exercise; mock mechanisms, counts and results are never production facts.</summary>
    public sealed class FirstLevelUiFixture : MonoBehaviour
    {
        [SerializeField] private BattleHudView hudPrefab;
        [SerializeField] private PlacementSelectionView selectionPrefab;
        public BattleHudView Hud { get; private set; }
        public PlacementSelectionView Selection { get; private set; }
        public PlacementSystem Placement { get; private set; }
        public PlacementGridView GridView { get; private set; }
        public PlacementGridAuthoring Grid { get; private set; }
        public CameraSystem Camera { get; private set; }
        public PlayerInputSystem Input { get; private set; }
        public TestPlacementBackend Backend { get; private set; }
        public LevelState Phase { get; private set; } = LevelState.Running;
        private PlacementInputAdapter adapter;
        private void Awake()
        {
            if (EventSystem.current == null)
            { new GameObject("TEST_ONLY_EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).transform.SetParent(transform); }
            var gridObject = new GameObject("TEST_ONLY_Grid",typeof(Grid),typeof(PlacementGridAuthoring));
            gridObject.transform.SetParent(transform,false); Grid=gridObject.GetComponent<PlacementGridAuthoring>();
            Grid.Grid.cellSize=new Vector3(1,1,0);
            var cameraObject=new GameObject("TEST_ONLY_Camera",typeof(Camera),typeof(CameraSystem));
            cameraObject.transform.SetParent(transform,false); Camera=cameraObject.GetComponent<CameraSystem>();
            cameraObject.GetComponent<Camera>().backgroundColor=new Color(0.22f,0.32f,0.23f);
            cameraObject.GetComponent<Camera>().orthographicSize=6; Camera.Initialize();
            Backend=new TestPlacementBackend(transform);
            Placement=new PlacementSystem(Grid,()=>new MapBounds(new Vector2(-7,-4),new Vector2(7,4)),()=>Phase,Backend,PlacementConfirmationMode.Single);
            Input=gameObject.AddComponent<PlayerInputSystem>(); Input.Enable(); Input.SetInputMode(InputMode.Gameplay);
            GridView=gameObject.AddComponent<PlacementGridView>(); GridView.Initialize(Placement,Grid);
            adapter=gameObject.AddComponent<PlacementInputAdapter>(); adapter.Initialize(Input,Camera,Placement);
            Hud=Instantiate(hudPrefab,transform); Selection=Instantiate(selectionPrefab,transform); Selection.Initialize(Placement);
            foreach(var button in Hud.GetComponentsInChildren<Button>(true))
            {
                if(button.name=="PauseButton") button.onClick.AddListener(()=>SetPhase(LevelState.Paused));
                if(button.name=="ResumeButton") button.onClick.AddListener(()=>SetPhase(LevelState.Running));
                if(button.name=="RestartButton") button.onClick.AddListener(Restart);
            }
            CreateTestControls(); Render();
        }
        public void SetPhase(LevelState phase)
        {
            Phase=phase; Input.SetInputMode(phase==LevelState.Running?InputMode.Gameplay:InputMode.UiOnly);
            Placement.CancelPlacement(); Render();
        }
        public void Restart()
        {
            foreach(var item in Backend.ReadPlacedObjects().ToArray()) Backend.Remove(item.Id);
            SetPhase(LevelState.Running);
        }
        private void Update() { Placement?.Refresh(); Render(); }
        private void Render()
        {
            if(Hud==null) return;
            Hud.Render(new UiState(new LevelId("TEST_ONLY"),Phase,"引导至少 6 只史莱姆进入终点洞穴",
                12,-1,string.Empty,"TEST_ONLY · 模拟上游结果",20,"",
                "到达洞穴  TEST_ONLY 2 / 6","等待窗口  TEST_ONLY 18 秒","路线 / Anchor 提示未接入"),true,
                Phase==LevelState.Paused,Phase==LevelState.Succeeded||Phase==LevelState.Failed,false);
        }
        private void CreateTestControls()
        {
            var canvas=new GameObject("TEST_ONLY_Controls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder=50;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            TestButton(canvas.transform,"TEST_ONLY Success",-110,()=>SetPhase(LevelState.Succeeded));
            TestButton(canvas.transform,"TEST_ONLY Failure",110,()=>SetPhase(LevelState.Failed));
        }
        private static void TestButton(Transform parent,string label,float x,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=new Vector2(0.5f,1);
            rect.anchoredPosition=new Vector2(x,-28); rect.sizeDelta=new Vector2(205,32);
            go.GetComponent<Image>().color=new Color(0.13f,0.21f,0.16f); go.GetComponent<Button>().onClick.AddListener(action);
            var text=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>(); text.transform.SetParent(go.transform,false);
            text.rectTransform.anchorMin=Vector2.zero; text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text=label; text.fontSize=14;
            text.alignment=TextAnchor.MiddleCenter; text.raycastTarget=false;
        }
        private void OnDisable()
        {
            if(adapter!=null) adapter.enabled=false;
            Placement?.Dispose(); Backend?.Dispose(); Input?.Disable();
        }
    }
}
