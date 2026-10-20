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
using System.Linq;
using UnityEditor.U2D.Sprites;

namespace SlimeBattle.Tests.PlayerPresentation.Editor
{
    /// <summary>Explicit, reproducible asset authoring. Never touches shared scenes or Build Settings.</summary>
    public static class FirstLevelUiPrefabBuilder
    {
        public const string Folder = "Assets/Game/UI/Prefabs";
        public const string HudPath = Folder + "/PF_FirstLevelHUD.prefab";
        public const string SelectionPath = Folder + "/PF_FirstLevelPlacementSelection.prefab";
        public const string PlayerPath = Folder + "/PF_FirstLevelPlayer.prefab";
        public const string SceneryPath = Folder + "/PF_FirstLevelScenery.prefab";
        public const string AtlasPath = "Assets/Game/UI/Art/StorybookIcons-v1.png";
        public const string ForestPath = "Assets/Game/UI/Art/ForestClearing-v1.png";
        public const string CaptureFolder = "UserSettings/CodexVisualPolish";
        public const string ScenePath = "Assets/Game/Tests/PlayerPresentation/Test_FirstLevelUI.unity";


        [MenuItem("SlimeBattle/UI/Rebuild First Level Prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Exit Play Mode before authoring prefabs."); }
            System.IO.Directory.CreateDirectory(Folder);
            ImportArt();
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/UI/Fonts/NotoSansCJKsc-Regular.otf");
            if (font == null) { throw new InvalidOperationException("Import the bundled Noto Chinese font first."); }
            var selection = BuildSelection();
            PrefabUtility.SaveAsPrefabAsset(selection.gameObject, SelectionPath);
            UnityEngine.Object.DestroyImmediate(selection.gameObject);
            var hud = BuildHud();
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, HudPath);
            UnityEngine.Object.DestroyImmediate(hud.gameObject);
            BuildPlayer();
            BuildScenery();
            ReferenceUiPrefabBuilder.BuildMainMenu();
            AssetDatabase.SaveAssets();
            Debug.Log("First-level UI prefabs authored successfully.");
        }

        private static PlacementSelectionView BuildSelection() => ReferenceUiPrefabBuilder.BuildSelection();
        private static BattleHudView BuildHud() => ReferenceUiPrefabBuilder.BuildHud();
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
            Set(first,"anchorMapDefinition",AssetDatabase.LoadAssetAtPath<FirstLevelMapDefinition>("Assets/Game/Configs/Maps/FirstLevel/MAP_FirstLevel.asset"));
            Set(first,"placementBackend",backend); Set(first,"statusSnapshotSource",status);
            Set(first,"selectionInstance",selection.GetComponent<PlacementSelectionView>());
            PrefabUtility.SaveAsPrefabAsset(root,PlayerPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void ImportArt()
        {
            AssetDatabase.ImportAsset(ForestPath,ImportAssetOptions.ForceSynchronousImport);
            var forest=(TextureImporter)AssetImporter.GetAtPath(ForestPath);
            forest.textureType=TextureImporterType.Sprite; forest.spriteImportMode=SpriteImportMode.Single;
            forest.spritePixelsPerUnit=100; forest.mipmapEnabled=false; forest.maxTextureSize=2048;
            forest.filterMode=FilterMode.Bilinear;
            forest.textureCompression=TextureImporterCompression.Uncompressed; forest.SaveAndReimport();
            AssetDatabase.ImportAsset(AtlasPath,ImportAssetOptions.ForceSynchronousImport);
            var atlas=(TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            atlas.textureType=TextureImporterType.Sprite; atlas.spriteImportMode=SpriteImportMode.Multiple;
            atlas.spritePixelsPerUnit=100; atlas.mipmapEnabled=false; atlas.alphaIsTransparency=true;
            atlas.maxTextureSize=2048; atlas.textureCompression=TextureImporterCompression.Uncompressed;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath); var w=(float)texture.width; var h=(float)texture.height;
            atlas.SaveAndReimport();
            var factory=new SpriteDataProviderFactories(); factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(atlas); provider.InitSpriteEditorDataProvider();
            var previous=provider.GetSpriteRects();
            var slices=new[] {
                SpriteData("Food",new UnityEngine.Rect(0,h/2,w/2,h/2)),
                SpriteData("Furnace",new UnityEngine.Rect(w/2,h/2,w/2,h/2)),
                SpriteData("Obstacle",new UnityEngine.Rect(0,0,w*0.55f,h/2)),
                SpriteData("Slime",new UnityEngine.Rect(w*0.55f,0,w*0.45f,h/2))
            };
            foreach(var slice in slices)
            {
                var existing=previous.FirstOrDefault(s=>s.name==slice.name);
                if(existing!=null) slice.spriteID=existing.spriteID;
            }
            provider.SetSpriteRects(slices);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
            provider.Apply();
            atlas.SaveAndReimport();
        }
        private static SpriteRect SpriteData(string name,UnityEngine.Rect rect)
        { return new SpriteRect { name=name,rect=rect,pivot=Vector2.one*0.5f,alignment=SpriteAlignment.Center,spriteID=GUID.Generate() }; }
        private static Sprite Art(string name)
        { return AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().Single(s=>s.name==name); }
        private static void BuildScenery()
        {
            var root=new GameObject("PF_FirstLevelScenery",typeof(SpriteRenderer),typeof(FirstLevelSceneryView));
            var renderer=root.GetComponent<SpriteRenderer>(); renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(ForestPath);
            renderer.sortingOrder=-1000;
            root.transform.position=new Vector3(0,0,2);
            root.transform.localScale=Vector3.one*(12f/renderer.sprite.bounds.size.y);
            PrefabUtility.SaveAsPrefabAsset(root,SceneryPath); UnityEngine.Object.DestroyImmediate(root);
        }

        [MenuItem("SlimeBattle/UI/Create First Level UI Test Scene")]
        public static void CreateTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            Build();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var fixture = new GameObject("TEST_ONLY_FirstLevelUI",typeof(FirstLevelUiFixture)).GetComponent<FirstLevelUiFixture>();
            StyleGrid(fixture.gameObject.AddComponent<PlacementGridView>());
            Set(fixture,"hudPrefab",AssetDatabase.LoadAssetAtPath<BattleHudView>(HudPath));
            Set(fixture,"selectionPrefab",AssetDatabase.LoadAssetAtPath<PlacementSelectionView>(SelectionPath));
            Set(fixture,"sceneryPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(SceneryPath));
            Set(fixture,"foodSprite",Art("Food")); Set(fixture,"furnaceSprite",Art("Furnace")); Set(fixture,"obstacleSprite",Art("Obstacle"));
            EditorSceneManager.SaveScene(scene,ScenePath);
        }

        [MenuItem("SlimeBattle/UI/Prepare Polished UI Preview")]
        public static void PreparePolishedPreview()
        { CreateTestScene(); CaptureLayouts(); }

        [MenuItem("SlimeBattle/UI/Open Reference UI Preview")]
        public static void OpenPolishedPreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPaused=false;
                var gameView=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                if(gameView!=null) EditorWindow.GetWindow(gameView).Focus();
                EditorApplication.EnterPlaymode();
            };
        }

        [MenuItem("SlimeBattle/UI/Capture First Level Layouts")]
        public static void CaptureLayouts()
        {
            Build();
            var preview=EditorSceneManager.NewPreviewScene();
            var owner=new GameObject("TEST_ONLY_UI_LayoutCapture");
            SceneManager.MoveGameObjectToScene(owner,preview);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SceneryPath),owner.transform);
            var hud=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleHudView>(HudPath),owner.transform);
            var selection=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlacementSelectionView>(SelectionPath),owner.transform);
            var gridObject=new GameObject("TEST_ONLY_Grid",typeof(Grid),typeof(PlacementGridAuthoring)); gridObject.transform.SetParent(owner.transform);
            var grid=gridObject.GetComponent<PlacementGridAuthoring>(); grid.Grid.cellSize=new Vector3(1,1,0);
            var backend=new TestPlacementBackend(owner.transform,Art("Food"),Art("Furnace"),Art("Obstacle")); var phase=LevelState.Running;
            backend.RequestPlacement(new PlacementRequest(PlacementItemId.Food,new Vector3Int(-2,0,0),new Vector3(-1.5f,0.5f,0)));
            backend.RequestPlacement(new PlacementRequest(PlacementItemId.Furnace,new Vector3Int(2,1,0),new Vector3(2.5f,1.5f,0)));
            backend.RequestPlacement(new PlacementRequest(PlacementItemId.Obstacle,new Vector3Int(0,-2,0),new Vector3(0.5f,-1.5f,0)));
            var placement=new PlacementSystem(grid,()=>new MapBounds(new Vector2(-7,-4),new Vector2(7,4)),()=>phase,backend,PlacementConfirmationMode.Single,
                FirstLevelUiFixture.CreateAnchorExamples,100f);
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
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1920,1080),new Vector2Int(1920,820)})
                {
                    placement.CancelPlacement();
                    hud.Render(new UiState(default,phase,"引导至少 6 只史莱姆进入终点洞穴",12,slimeLimit:20),true,false,false,false);
                    Capture(camera,size,CaptureFolder+"/UI-Idle-"+size.x+"x"+size.y+".png");
                    phase=LevelState.Running; placement.BeginPlacement(PlacementItemId.Food); placement.PreviewPlacement(new Vector3(1.3f,0.2f));
                    hud.Render(new UiState(default,phase,"引导至少 6 只史莱姆进入终点洞穴",12,slimeLimit:20),true,false,false,false);
                    Capture(camera,size,CaptureFolder+"/UI-Running-"+size.x+"x"+size.y+".png");
                }
                foreach(var state in new[]{LevelState.Paused,LevelState.Succeeded,LevelState.Failed})
                {
                    phase=state; placement.CancelPlacement();
                    hud.Render(new UiState(default,phase,"引导至少 6 只史莱姆进入终点洞穴",12,resultText:"TEST_ONLY · 模拟上游结果",slimeLimit:20),true,
                        state==LevelState.Paused,state!=LevelState.Paused,false);
                    Capture(camera,new Vector2Int(1280,720),CaptureFolder+"/UI-"+state+".png");
                }
                phase=LevelState.Running; placement.BeginPlacement(PlacementItemId.Obstacle);
                placement.PreviewPlacement(new Vector3(-3f,1.8f));
                hud.Render(new UiState(default,phase,"TEST_ONLY · Anchor 布局验证",12,slimeLimit:20),true,false,false,false);
                Capture(camera,new Vector2Int(1280,720),CaptureFolder+"/UI-Anchor.png");
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
                camera.transform.parent.GetComponentInChildren<FirstLevelSceneryView>().FitToCamera(camera);
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

        private static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
        { var so=new SerializedObject(owner); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetBool(UnityEngine.Object owner,string field,bool value)
        { var so=new SerializedObject(owner); so.FindProperty(field).boolValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void StyleGrid(PlacementGridView view)
        {
            var so=new SerializedObject(view); so.FindProperty("lineWidth").floatValue=0.022f;
            so.FindProperty("gridColor").colorValue=new Color(0.22f,0.32f,0.13f,0.28f);
            so.FindProperty("freeColor").colorValue=new Color(0.84f,1f,0.28f,0.42f);
            so.FindProperty("occupiedColor").colorValue=new Color(0.98f,0.34f,0.20f,0.48f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
