using System;
using SlimeBattle.Player;
using SlimeBattle.Player.Placement;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SlimeBattle.Core;
using SlimeBattle.World;

namespace SlimeBattle.Tests.PlayerPresentation.Editor
{
    /// <summary>Explicit, reproducible asset authoring. Never touches shared scenes or Build Settings.</summary>
    public static class FirstLevelUiPrefabBuilder
    {
        public const string Folder = "Assets/Game/UI/Prefabs";
        public const string HudPath = Folder + "/PF_FirstLevelHUD.prefab";
        public const string SelectionPath = Folder + "/PF_FirstLevelPlacementSelection.prefab";
        public const string PlayerPath = Folder + "/PF_FirstLevelPlayer.prefab";
        public const string ScenePath = "Assets/Game/Tests/PlayerPresentation/Test_FirstLevelUI.unity";
        private static Font font;
        private static readonly Color Paper = new Color(0.98f, 0.96f, 0.88f);
        private static readonly Color Ink = new Color(0.17f, 0.27f, 0.21f);
        private static readonly Color Muted = new Color(0.40f, 0.46f, 0.36f);
        private static readonly Color Green = new Color(0.72f, 0.85f, 0.62f);

        [MenuItem("SlimeBattle/UI/Rebuild First Level Prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Exit Play Mode before authoring prefabs."); }
            System.IO.Directory.CreateDirectory(Folder);
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/UI/Fonts/NotoSansCJKsc-Regular.otf");
            if (font == null) { throw new InvalidOperationException("Import the bundled Noto Chinese font first."); }
            var selection = BuildSelection();
            PrefabUtility.SaveAsPrefabAsset(selection.gameObject, SelectionPath);
            UnityEngine.Object.DestroyImmediate(selection.gameObject);
            var hud = BuildHud();
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, HudPath);
            UnityEngine.Object.DestroyImmediate(hud.gameObject);
            BuildPlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("First-level UI prefabs authored successfully.");
        }

        private static PlacementSelectionView BuildSelection()
        {
            var canvas = CanvasRoot("PF_FirstLevelPlacementSelection", 21);
            var view = canvas.gameObject.AddComponent<PlacementSelectionView>();
            var safe = Safe(canvas);
            var panel = Rect("PlacementPanel", safe); Stretch(panel);
            var sidebar = Panel("Sidebar", panel, new Vector2(1,0.5f), new Vector2(-126,0), new Vector2(208,390), Paper);
            Label("Heading", sidebar, "布置环境", new Vector2(0,161), new Vector2(172,30), 21, Ink);
            var food = MechanismButton(sidebar, "Food", "食物", "吸引饥饿的史莱姆", 99, FirstLevelUiIcon.IconKind.Food);
            var furnace = MechanismButton(sidebar, "Furnace", "熔炉", "带来温暖的引导源", 11, FirstLevelUiIcon.IconKind.Furnace);
            var obstacle = MechanismButton(sidebar, "Obstacle", "障碍", "布置阻挡，调整路径", -77, FirstLevelUiIcon.IconKind.Obstacle);
            var cancel = Button("CancelButton", sidebar, "取消放置", new Vector2(0,-153), new Vector2(172,42), new Color(0.88f,0.88f,0.78f));
            var budget = Label("Budget", panel, "放置配额未接入", new Vector2(-126,-222), new Vector2(208,28), 14, Paper);
            budget.rectTransform.anchorMin = budget.rectTransform.anchorMax = new Vector2(1,0.5f);
            var hintCard = Panel("PlacementHint", panel, new Vector2(0.5f,0), new Vector2(70,58), new Vector2(500,76), Paper);
            var hint = Label("Hint", hintCard, "选择右侧机关，再点击地图放置", new Vector2(0,9), new Vector2(460,30), 18, Ink);
            Label("Gesture", hintCard, "右键拖动 / 双指平移镜头  ·  滚轮缩放", new Vector2(0,-19), new Vector2(460,22), 13, Muted);
            Set(view, "panel", panel.gameObject); Set(view, "hint", hint); Set(view, "budget", budget);
            Set(view, "food", food); Set(view, "furnace", furnace); Set(view, "obstacle", obstacle); Set(view, "cancel", cancel);
            SetBool(view, "useChineseLabels", true);
            return view;
        }

        private static BattleHudView BuildHud()
        {
            var canvas = CanvasRoot("PF_FirstLevelHUD", 20);
            var view = canvas.gameObject.AddComponent<BattleHudView>();
            var safe = Safe(canvas);
            var hud = Rect("HUD", safe);
            Stretch(hud);
            var pause = Button("PauseButton", hud, "", new Vector2(58,-56), new Vector2(60,60), Paper);
            Anchor(pause.transform, new Vector2(0,1));
            Icon(pause.transform, FirstLevelUiIcon.IconKind.Pause, Vector2.zero, new Vector2(26,26));
            var chapter = Label("Chapter", hud, "第一关  /  林间归途", new Vector2(214,-45), new Vector2(236,28), 22, Paper);
            Anchor(chapter.transform, new Vector2(0,1)); chapter.alignment = TextAnchor.MiddleLeft;
            var tagline = Label("Tagline", hud, "不必命令，只需引导。", new Vector2(214,-73), new Vector2(236,24), 14, Paper);
            Anchor(tagline.transform, new Vector2(0,1)); tagline.alignment = TextAnchor.MiddleLeft;
            var countCard = Panel("SlimeCounter", hud, new Vector2(1,1), new Vector2(-163,-56), new Vector2(282,64), Paper);
            Icon(countCard, FirstLevelUiIcon.IconKind.Slime, new Vector2(-101,0), new Vector2(56,56));
            var counts = Label("Counts", countCard, "史莱姆  -- / --", new Vector2(28,0), new Vector2(198,44), 23, Ink);
            var mission = Panel("MissionCard", hud, new Vector2(0,0), new Vector2(198,102), new Vector2(348,164), Paper);
            var objective = Label("Objective", mission, "引导至少 6 只史莱姆进入终点洞穴", new Vector2(0,52), new Vector2(310,42), 17, Ink);
            var goal = Label("Goal", mission, "到达洞穴  -- / 6 · 未接入", new Vector2(0,12), new Vector2(310,28), 19, Ink);
            var waiting = Label("WaitingWindow", mission, "等待窗口  -- · 未接入", new Vector2(0,-20), new Vector2(310,25), 16, Muted);
            var route = Label("RouteHint", mission, "路线 / Anchor 提示未接入", new Vector2(0,-52), new Vector2(310,25), 13, Muted);
            var weapon = Label("LegacyWeapon", hud, "", new Vector2(0,-122), new Vector2(600,34), 15, Paper);
            Anchor(weapon.transform, new Vector2(0.5f,1)); weapon.gameObject.SetActive(false);
            var hint = Label("FlowHint", hud, "等待关卡数据接入", new Vector2(70,110), new Vector2(500,36), 15, Paper);
            Anchor(hint.transform, new Vector2(0.5f,0));

            var pausePanel = Modal("PausePanel", canvas);
            var pauseCard = Panel("Card", pausePanel.transform, Vector2.one*0.5f, Vector2.zero, new Vector2(500,400), Paper);
            Icon(pauseCard, FirstLevelUiIcon.IconKind.Slime, new Vector2(0,118), new Vector2(76,76));
            Label("Heading", pauseCard, "歇一会儿", new Vector2(0,54), new Vector2(400,44), 30, Ink);
            Label("Description", pauseCard, "史莱姆们会在这里等你", new Vector2(0,12), new Vector2(400,30), 17, Muted);
            var resume = Button("ResumeButton", pauseCard, "继续冒险", new Vector2(0,-54), new Vector2(300,56), Green);
            var pauseRestart = Button("RestartButton", pauseCard, "重新开始", new Vector2(0,-122), new Vector2(300,48), new Color(0.89f,0.88f,0.79f));

            var resultPanel = Modal("ResultPanel", canvas);
            var resultCard = Panel("Card", resultPanel.transform, Vector2.one*0.5f, Vector2.zero, new Vector2(580,460), Paper);
            Icon(resultCard, FirstLevelUiIcon.IconKind.Slime, new Vector2(0,160), new Vector2(86,86));
            var result = Label("Result", resultCard, "", new Vector2(0,87), new Vector2(500,44), 30, Ink);
            var detailViewport = Rect("ResultReasonViewport",resultCard); detailViewport.anchoredPosition=new Vector2(0,16);
            detailViewport.sizeDelta=new Vector2(500,76); detailViewport.gameObject.AddComponent<RectMask2D>();
            detailViewport.gameObject.AddComponent<Image>().color=Color.clear;
            var resultDetail=Label("ResultDetail",detailViewport,"",Vector2.zero,new Vector2(480,76),17,Muted);
            resultDetail.resizeTextForBestFit=false;
            resultDetail.rectTransform.anchorMin=new Vector2(0,1); resultDetail.rectTransform.anchorMax=new Vector2(1,1);
            resultDetail.rectTransform.pivot=new Vector2(0.5f,1); resultDetail.rectTransform.sizeDelta=new Vector2(-16,76);
            resultDetail.alignment=TextAnchor.UpperCenter;
            var fitter=resultDetail.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=detailViewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=detailViewport;
            scroll.content=resultDetail.rectTransform; scroll.horizontal=false; scroll.vertical=true; scroll.scrollSensitivity=24;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            var resultCounts = Label("ResultCounts", resultCard, "", new Vector2(0,-60), new Vector2(500,70), 18, Muted);
            var resultRestart = Button("RestartButton", resultCard, "再来一次", new Vector2(0,-156), new Vector2(300,56), Green);
            Set(view,"hudPanel",hud.gameObject); Set(view,"pausePanel",pausePanel); Set(view,"resultPanel",resultPanel);
            Set(view,"objectiveText",objective); Set(view,"countsText",counts); Set(view,"weaponText",weapon); Set(view,"hintText",hint);
            Set(view,"goalText",goal); Set(view,"waitingText",waiting); Set(view,"routeHintText",route); Set(view,"resultCountsText",resultCounts);
            Set(view,"resultText",result); Set(view,"resultDetailText",resultDetail); Set(view,"pauseButton",pause); Set(view,"resumeButton",resume);
            Set(view,"pauseRestartButton",pauseRestart); Set(view,"resultRestartButton",resultRestart); SetBool(view,"useChineseLabels",true);
            pausePanel.SetActive(false); resultPanel.SetActive(false);
            return view;
        }

        private static void BuildPlayer()
        {
            var root = new GameObject("PF_FirstLevelPlayer");
            var player = root.AddComponent<PlayerPresentationRoot>();
            var first = root.AddComponent<FirstLevelPresentationRoot>();
            StyleGrid(root.AddComponent<PlacementGridView>()); root.AddComponent<PlacementInputAdapter>();
            var backend = root.AddComponent<FirstLevelMechanismBackend>();
            var status = root.AddComponent<FirstLevelStatusProvider>();
            var cameraObject = new GameObject("GameplayCamera", typeof(Camera), typeof(CameraSystem));
            cameraObject.transform.SetParent(root.transform,false); cameraObject.transform.localPosition = new Vector3(0,0,-10);
            cameraObject.GetComponent<Camera>().orthographic = true; cameraObject.GetComponent<Camera>().orthographicSize = 6;
            cameraObject.GetComponent<Camera>().backgroundColor = new Color(0.24f,0.33f,0.24f);
            var gridObject = new GameObject("PlacementGrid",typeof(Grid),typeof(PlacementGridAuthoring));
            gridObject.transform.SetParent(root.transform,false); gridObject.GetComponent<Grid>().cellSize = new Vector3(1,1,0);
            var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath),root.transform);
            var selection = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SelectionPath),root.transform);
            Set(player,"cameraSystem",cameraObject.GetComponent<CameraSystem>()); Set(player,"hudView",hud.GetComponent<BattleHudView>());
            SetBool(player,"createDefaultHud",false);
            Set(first,"cameraSystem",cameraObject.GetComponent<CameraSystem>()); Set(first,"grid",gridObject.GetComponent<PlacementGridAuthoring>());
            Set(first,"placementBackend",backend); Set(first,"statusSnapshotSource",status);
            Set(first,"selectionInstance",selection.GetComponent<PlacementSelectionView>());
            PrefabUtility.SaveAsPrefabAsset(root,PlayerPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        [MenuItem("SlimeBattle/UI/Create First Level UI Test Scene")]
        public static void CreateTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            Build();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var fixture = new GameObject("TEST_ONLY_FirstLevelUI",typeof(FirstLevelUiFixture)).GetComponent<FirstLevelUiFixture>();
            Set(fixture,"hudPrefab",AssetDatabase.LoadAssetAtPath<BattleHudView>(HudPath));
            Set(fixture,"selectionPrefab",AssetDatabase.LoadAssetAtPath<PlacementSelectionView>(SelectionPath));
            EditorSceneManager.SaveScene(scene,ScenePath);
        }

        [MenuItem("SlimeBattle/UI/Capture First Level Layouts")]
        public static void CaptureLayouts()
        {
            Build();
            var preview=EditorSceneManager.NewPreviewScene();
            var owner=new GameObject("TEST_ONLY_UI_LayoutCapture");
            SceneManager.MoveGameObjectToScene(owner,preview);
            var hud=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleHudView>(HudPath),owner.transform);
            var selection=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlacementSelectionView>(SelectionPath),owner.transform);
            var gridObject=new GameObject("TEST_ONLY_Grid",typeof(Grid),typeof(PlacementGridAuthoring)); gridObject.transform.SetParent(owner.transform);
            var grid=gridObject.GetComponent<PlacementGridAuthoring>(); grid.Grid.cellSize=new Vector3(1,1,0);
            var backend=new TestPlacementBackend(); var phase=LevelState.Running;
            var placement=new PlacementSystem(grid,()=>new MapBounds(new Vector2(-7,-4),new Vector2(7,4)),()=>phase,backend,PlacementConfirmationMode.Single);
            var gridView=owner.AddComponent<PlacementGridView>(); StyleGrid(gridView); gridView.Initialize(placement,grid); selection.Initialize(placement);
            var cameraObject=new GameObject("TEST_ONLY_CaptureCamera",typeof(Camera)); cameraObject.transform.SetParent(owner.transform);
            cameraObject.transform.position=new Vector3(0,0,-10); var camera=cameraObject.GetComponent<Camera>();
            camera.scene=preview;
            camera.orthographic=true; camera.orthographicSize=6; camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(0.22f,0.32f,0.23f);
            foreach(var canvas in owner.GetComponentsInChildren<Canvas>(true))
            { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1; canvas.sortingOrder+=1000; }
            try
            {
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1920,1080)})
                {
                    phase=LevelState.Running; placement.BeginPlacement(PlacementItemId.Food); placement.PreviewPlacement(new Vector3(1.3f,0.2f));
                    hud.Render(new UiState(default,phase,"引导至少 6 只史莱姆进入终点洞穴",12,slimeLimit:20),true,false,false,false);
                    Capture(camera,size,"Temp/CodexFirstLevelUI/UI-Running-"+size.x+"x"+size.y+".png");
                }
                foreach(var state in new[]{LevelState.Paused,LevelState.Succeeded,LevelState.Failed})
                {
                    phase=state; placement.CancelPlacement();
                    hud.Render(new UiState(default,phase,"引导至少 6 只史莱姆进入终点洞穴",12,resultText:"TEST_ONLY · 模拟上游结果",slimeLimit:20),true,
                        state==LevelState.Paused,state!=LevelState.Paused,false);
                    Capture(camera,new Vector2Int(1280,720),"Temp/CodexFirstLevelUI/UI-"+state+".png");
                }
            }
            finally
            {
                placement.Dispose(); backend.Dispose(); UnityEngine.Object.DestroyImmediate(owner);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void Capture(Camera camera,Vector2Int size,string path)
        {
            var target=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;
                Canvas.ForceUpdateCanvases();
                camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new UnityEngine.Rect(0,0,size.x,size.y),0,0); texture.Apply();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null; RenderTexture.active=previous;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static RectTransform CanvasRoot(string name, int order)
        {
            var root = new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder = order;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return (RectTransform)root.transform;
        }
        private static RectTransform Safe(Transform parent)
        { var safe = Rect("SafeArea",parent); Stretch(safe); safe.gameObject.AddComponent<UiSafeArea>(); return safe; }
        private static RectTransform Rect(string name,Transform parent)
        { var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); return (RectTransform)go.transform; }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static void Anchor(Transform item,Vector2 anchor)
        { var rect=(RectTransform)item; rect.anchorMin=rect.anchorMax=anchor; }
        private static RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var rect=Rect(name,parent); rect.anchorMin=rect.anchorMax=anchor; rect.anchoredPosition=position; rect.sizeDelta=size;
            var image=rect.gameObject.AddComponent<Image>(); image.color=color;
            image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); image.type=Image.Type.Sliced;
            var shadow=rect.gameObject.AddComponent<Shadow>(); shadow.effectColor=new Color(0.08f,0.13f,0.08f,0.25f); shadow.effectDistance=new Vector2(0,-4);
            return rect;
        }
        private static Text Label(string name,Transform parent,string content,Vector2 position,Vector2 size,int fontSize,Color color)
        {
            var rect=Rect(name,parent); rect.anchorMin=rect.anchorMax=Vector2.one*0.5f; rect.anchoredPosition=position; rect.sizeDelta=size;
            var text=rect.gameObject.AddComponent<Text>(); text.font=font; text.text=content; text.fontSize=fontSize; text.color=color;
            text.alignment=TextAnchor.MiddleCenter; text.raycastTarget=false; text.supportRichText=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate;
            text.resizeTextForBestFit=true; text.resizeTextMinSize=Mathf.Max(12,fontSize-4); text.resizeTextMaxSize=fontSize;
            return text;
        }
        private static Button Button(string name,Transform parent,string label,Vector2 position,Vector2 size,Color color)
        {
            var rect=Panel(name,parent,Vector2.one*0.5f,position,size,color);
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>();
            var colors=button.colors; colors.highlightedColor=new Color(0.94f,1f,0.88f); colors.pressedColor=new Color(0.78f,0.88f,0.70f);
            colors.disabledColor=new Color(0.77f,0.78f,0.71f,0.65f); button.colors=colors;
            var navigation=button.navigation; navigation.mode=Navigation.Mode.None; button.navigation=navigation;
            if (!string.IsNullOrEmpty(label)) Label("Label",rect,label,Vector2.zero,size-new Vector2(12,4),20,Ink);
            return button;
        }
        private static Button MechanismButton(Transform parent,string name,string title,string caption,float y,FirstLevelUiIcon.IconKind icon)
        {
            var button=Button(name+"Button",parent,"",new Vector2(0,y),new Vector2(176,78),Paper);
            Icon(button.transform,icon,new Vector2(-54,7),new Vector2(48,48));
            Label("Label",button.transform,title,new Vector2(24,12),new Vector2(92,30),23,Ink);
            Label("Caption",button.transform,caption,new Vector2(0,-23),new Vector2(162,20),12,Muted);
            return button;
        }
        private static void Icon(Transform parent,FirstLevelUiIcon.IconKind kind,Vector2 position,Vector2 size)
        {
            var rect=Rect("Icon_"+kind,parent); rect.anchorMin=rect.anchorMax=Vector2.one*0.5f; rect.anchoredPosition=position; rect.sizeDelta=size;
            var graphic=rect.gameObject.AddComponent<FirstLevelUiIcon>(); graphic.Icon=kind; graphic.raycastTarget=false;
        }
        private static GameObject Modal(string name,Transform parent)
        {
            var rect=Rect(name,parent); Stretch(rect);
            var canvas=rect.gameObject.AddComponent<Canvas>(); canvas.overrideSorting=true; canvas.sortingOrder=40;
            rect.gameObject.AddComponent<GraphicRaycaster>();
            var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(0.08f,0.14f,0.10f,0.76f);
            return rect.gameObject;
        }
        private static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
        { var so=new SerializedObject(owner); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetBool(UnityEngine.Object owner,string field,bool value)
        { var so=new SerializedObject(owner); so.FindProperty(field).boolValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void StyleGrid(PlacementGridView view)
        {
            var so=new SerializedObject(view); so.FindProperty("lineWidth").floatValue=0.025f;
            so.FindProperty("gridColor").colorValue=new Color(0.69f,0.82f,0.60f,0.35f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
