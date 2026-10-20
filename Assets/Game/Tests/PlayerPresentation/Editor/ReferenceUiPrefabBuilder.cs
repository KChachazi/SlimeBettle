using System.Linq;
using SlimeBattle.Core;
using SlimeBattle.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SlimeBattle.Tests.PlayerPresentation.Editor
{
    /// <summary>TapTap illustrations with prefab-owned uGUI controls and existing gameplay bindings.</summary>
    public static class ReferenceUiPrefabBuilder
    {
        public const string MenuPath="Assets/Game/UI/Prefabs/PF_ReferenceMainMenu.prefab";
        private const string CaptureFolder="UserSettings/ReferenceUiPreview";
        private static Font handwriting;
        private static Font body;
        private static Material backdrop;
        private static readonly Color Cream=new Color(0.98f,0.945f,0.82f);
        private static readonly Color Ink=new Color(0.25f,0.21f,0.15f);
        private static readonly Color Board=new Color(0.30f,0.32f,0.22f);
        private static readonly Color Cocoa=new Color(0.33f,0.25f,0.18f);
        private static readonly Color Moss=new Color(0.46f,0.55f,0.31f);
        private static readonly Color Gold=new Color(1f,0.81f,0.39f);
        private static readonly Color Mint=new Color(0.79f,0.87f,0.63f);

        private static void LoadAssets()
        {
            handwriting=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/UI/Fonts/LongCang-Regular.ttf");
            body=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/UI/Fonts/NotoSansCJKsc-Regular.otf");
            if(handwriting==null || body==null) throw new System.InvalidOperationException("Import bundled UI fonts before building.");
            const string path="Assets/Game/UI/Art/MAT_ReferenceBackdrop.mat";
            backdrop=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(backdrop==null)
            {
                backdrop=new Material(Shader.Find("SlimeBattle/UI/ReferenceBackdrop"));
                backdrop.SetFloat("_Blur",2.5f); AssetDatabase.CreateAsset(backdrop,path);
            }
            if(!Mathf.Approximately(backdrop.GetFloat("_Blur"),2.5f))
            { backdrop.SetFloat("_Blur",2.5f); EditorUtility.SetDirty(backdrop); }
        }

        public static PlacementSelectionView BuildSelection()
        {
            LoadAssets();
            var root=CanvasRoot("PF_FirstLevelPlacementSelection",21);
            var view=root.gameObject.AddComponent<PlacementSelectionView>();
            var panel=Rect("PlacementPanel",Safe(root)); Stretch(panel);
            var toolbar=Rect("Toolbar",panel); Stretch(toolbar);
            var food=IllustratedTool(toolbar,"FoodButton","HudFood");
            var furnace=IllustratedTool(toolbar,"FurnaceButton","HudFurnace");
            var obstacle=IllustratedTool(toolbar,"ObstacleButton","HudObstacle");
            var cancel=Button("CancelButton",panel,"取消放置",new Vector2(-410,34),new Vector2(154,40),Cream,Ink,26);
            Anchor(cancel.transform,new Vector2(1,0));
            var budget=Label("Budget",panel,"放置配额未接入",new Vector2(-430,-127),new Vector2(280,34),13,Ink,false);
            Anchor(budget.transform,new Vector2(1,1));
            var hintCard=Panel("PlacementHint",panel,new Vector2(0.5f,0),new Vector2(-70,91),new Vector2(450,44),new Color(Cocoa.r,Cocoa.g,Cocoa.b,0.94f));
            var hint=Label("Hint",hintCard,"选择机关，再点击地图放置",Vector2.zero,new Vector2(426,38),15,Cream,false);
            Set(view,"panel",panel.gameObject); Set(view,"hint",hint); Set(view,"budget",budget);
            Set(view,"food",food); Set(view,"furnace",furnace); Set(view,"obstacle",obstacle); Set(view,"cancel",cancel);
            Set(view,"foodSelected",food.transform.Find("Selected").gameObject);
            Set(view,"furnaceSelected",furnace.transform.Find("Selected").gameObject);
            Set(view,"obstacleSelected",obstacle.transform.Find("Selected").gameObject);
            SetBool(view,"useChineseLabels",true); SetBool(view,"useReferenceLayout",true);
            SetBool(view,"useIllustratedSprites",true);
            return view;
        }

        public static BattleHudView BuildHud()
        {
            LoadAssets();
            var root=CanvasRoot("PF_FirstLevelHUD",20);
            var view=root.gameObject.AddComponent<BattleHudView>();
            var pages=root.gameObject.AddComponent<ReferenceUiView>();
            var hud=Rect("HUD",Safe(root)); Stretch(hud);
            var strip=SourceArt("ToolbarBacking",hud,"HudBoard",new Vector2(1,0.5f));
            // The wooden rail blocks map clicks, while its individual controls belong to separate canvases.
            strip.GetComponent<Image>().raycastTarget=true;
            var pause=SourceButton("PauseButton",hud,"HudSettings",new Vector2(0,1));
            var counter=SourceArt("SlimeCounter",hud,"HudCounter",new Vector2(1,1));
            var counts=Label("Counts",counter,"-- / --",new Vector2(131,-1),new Vector2(118,43),29,Ink);
            var bag=ArtButton("BagButton",hud,"HudBag",new Vector2(-410,135),new Vector2(124,121));
            Anchor(bag.transform,new Vector2(1,0));
            Click(bag,pages.OpenBag);
            var catalog=SourceButton("CatalogButton",hud,"HudCatalog",new Vector2(1,0.5f));
            Click(catalog,pages.OpenCatalog);
            var remove=SourceButton("RemoveButton",hud,"HudRemove",new Vector2(1,0.5f));
            Click(remove,pages.ToggleRemoval); Set(pages,"removeButton",remove); SetBool(pages,"useIllustratedSprites",true);
            var hint=Label("FlowHint",hud,"",new Vector2(-80,52),new Vector2(460,28),14,Cream,false);
            Anchor(hint.transform,new Vector2(0.5f,0));
            var feedback=Label("FeatureFeedback",hud,"",new Vector2(-80,24),new Vector2(460,28),14,Cream,false);
            Anchor(feedback.transform,new Vector2(0.5f,0)); Set(pages,"feedbackText",feedback);

            // Compact optional facts preserve integration and never occupy the map's center.
            var mission=Panel("MissionCard",hud,new Vector2(0,0),new Vector2(178,83),new Vector2(310,130),new Color(0.98f,0.945f,0.82f,0.96f));
            var objective=Label("Objective",mission,"",new Vector2(0,45),new Vector2(286,31),14,Ink,false);
            var goal=Label("Goal",mission,"",new Vector2(0,15),new Vector2(286,25),17,Ink,false);
            var waiting=Label("WaitingWindow",mission,"",new Vector2(0,-14),new Vector2(286,22),13,Ink,false);
            var route=Label("RouteHint",mission,"",new Vector2(0,-41),new Vector2(286,22),12,Ink,false);
            var weapon=Label("LegacyWeapon",hud,"",new Vector2(0,-110),new Vector2(620,28),14,Ink,false);
            Anchor(weapon.transform,new Vector2(0.5f,1)); weapon.gameObject.SetActive(false);

            var pausePanel=Modal("PausePanel",root,40);
            var card=Illustration("Card",pausePanel.transform,"CatalogFrame4",Vector2.zero,new Vector2(748,462));
            Label("Heading",card,"游戏暂停",new Vector2(0,144),new Vector2(420,66),53,Ink);
            var resume=Button("ResumeButton",card,"继续游戏",new Vector2(0,48),new Vector2(320,59),Cream,Ink,43);
            var returnToMenu=Button("PauseReturnToMenuButton",card,"返回主菜单",new Vector2(0,-35),new Vector2(320,59),Cream,Ink,38);
            Click(returnToMenu,pages.ReturnToMainMenu);
            var restart=Button("RestartButton",card,"重新开始",new Vector2(0,-116),new Vector2(224,42),Cream,Ink,28);

            var resultPanel=Modal("ResultPanel",root,40);
            var resultCard=Illustration("Card",resultPanel.transform,"CatalogFrame4",Vector2.zero,new Vector2(890,550));
            var result=Label("Result",resultCard,"",new Vector2(0,176),new Vector2(510,70),52,Ink);
            var detailViewport=Rect("ResultReasonViewport",resultCard); detailViewport.anchoredPosition=new Vector2(0,62); detailViewport.sizeDelta=new Vector2(550,108);
            detailViewport.gameObject.AddComponent<RectMask2D>(); detailViewport.gameObject.AddComponent<Image>().color=Color.clear;
            var detail=Label("ResultDetail",detailViewport,"",Vector2.zero,new Vector2(532,108),17,Ink,false);
            detail.resizeTextForBestFit=false; detail.rectTransform.anchorMin=new Vector2(0,1); detail.rectTransform.anchorMax=new Vector2(1,1);
            detail.rectTransform.pivot=new Vector2(0.5f,1); detail.rectTransform.sizeDelta=new Vector2(-18,108); detail.alignment=TextAnchor.UpperCenter;
            detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=detailViewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=detailViewport; scroll.content=detail.rectTransform;
            scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=24;
            var resultCounts=Label("ResultCounts",resultCard,"",new Vector2(0,-45),new Vector2(550,72),17,Ink,false);
            var resultRestart=Button("RestartButton",resultCard,"再来一次",new Vector2(-143,-133),new Vector2(248,54),Cream,Ink,36);
            var resultReturn=Button("ResultReturnToMenuButton",resultCard,"返回主菜单",new Vector2(143,-133),new Vector2(248,54),Cream,Ink,34);
            Click(resultReturn,pages.ReturnToMainMenu);

            BuildPages(root,pages);
            var bindings=new SerializedObject(pages);
            var returns=bindings.FindProperty("returnToMenuButtons"); returns.arraySize=3;
            returns.GetArrayElementAtIndex(0).objectReferenceValue=returnToMenu;
            returns.GetArrayElementAtIndex(1).objectReferenceValue=resultReturn;
            returns.GetArrayElementAtIndex(2).objectReferenceValue=root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="SettingsReturnToMenuButton");
            bindings.ApplyModifiedPropertiesWithoutUndo();
            WrapModalContent(pausePanel); WrapModalContent(resultPanel);
            Set(view,"hudPanel",hud.gameObject); Set(view,"pausePanel",pausePanel); Set(view,"resultPanel",resultPanel); Set(view,"referencePages",pages);
            Set(view,"objectiveText",objective); Set(view,"countsText",counts); Set(view,"weaponText",weapon); Set(view,"hintText",hint);
            Set(view,"goalText",goal); Set(view,"waitingText",waiting); Set(view,"routeHintText",route); Set(view,"resultCountsText",resultCounts);
            Set(view,"resultText",result); Set(view,"resultDetailText",detail); Set(view,"pauseButton",pause); Set(view,"resumeButton",resume);
            Set(view,"pauseRestartButton",restart); Set(view,"resultRestartButton",resultRestart); SetBool(view,"useChineseLabels",true);
            SetBool(view,"countsValueOnly",true);
            pausePanel.SetActive(false); resultPanel.SetActive(false);
            return view;
        }

        private static void BuildPages(Transform root,ReferenceUiView view)
        {
            var pages=new GameObject[6];
            for(var i=0;i<pages.Length;i++) pages[i]=Modal(((ReferenceUiView.Page)(i+1))+"Panel",root,45);
            BuildBag(pages[0].transform,view);
            BuildCatalog(pages[1].transform,view);
            var settings=Rect("SettingsCard",pages[2].transform); Stretch(settings);
            SourceArt("SettingsBoard",settings,"SettingsBoard");
            var actions=new UnityAction[]{view.OpenVolume,view.OpenSaves,view.OpenResolution};
            var words=new[]{"音量","存档","分辨率"};
            var art=new[]{"SettingsVolume","SettingsSaves","SettingsResolution"};
            for(var i=0;i<3;i++)
                Click(SourceButton(words[i]+"Button",settings,art[i]),actions[i]);
            Click(Button("SettingsBackButton",settings,"继续游戏",new Vector2(-148,-278),new Vector2(232,52),Cream,Ink,34),view.Close);
            Click(Button("SettingsReturnToMenuButton",settings,"返回主菜单",new Vector2(148,-278),new Vector2(232,52),Cream,Ink,32),view.ReturnToMainMenu);

            var volume=SubCard(pages[3].transform,"音量",view);
            Label("VolumeDescription",volume,"主音量",new Vector2(0,100),new Vector2(330,40),29,Ink);
            var sliderRect=Panel("VolumeSlider",volume,Vector2.one*0.5f,new Vector2(0,10),new Vector2(330,26),Cocoa);
            var slider=sliderRect.gameObject.AddComponent<Slider>(); slider.minValue=0; slider.maxValue=1; slider.value=1;
            var fillArea=Rect("FillArea",sliderRect); Stretch(fillArea); fillArea.offsetMin=new Vector2(10,5); fillArea.offsetMax=new Vector2(-10,-5);
            var fill=Panel("Fill",fillArea,Vector2.zero,Vector2.zero,Vector2.zero,Gold); Stretch(fill);
            var handleArea=Rect("HandleArea",sliderRect); Stretch(handleArea); handleArea.offsetMin=new Vector2(10,0); handleArea.offsetMax=new Vector2(-10,0);
            var handle=Panel("Handle",handleArea,Vector2.one*0.5f,Vector2.zero,new Vector2(24,44),Cream);
            slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<Image>();
            slider.navigation=new Navigation { mode=Navigation.Mode.None }; UnityEventTools.AddPersistentListener(slider.onValueChanged,view.SetVolume);
            Set(view,"volumeSlider",slider); Set(view,"volumeText",Label("VolumeValue",volume,"100 %",new Vector2(0,-63),new Vector2(330,46),37,Ink));

            var saves=SubCard(pages[4].transform,"存档",view);
            for(var i=0;i<3;i++)
            {
                var slot=Panel("SaveSlot"+(i+1),saves,Vector2.one*0.5f,new Vector2(0,105-i*95),new Vector2(390,72),Cream);
                Label("SlotLabel",slot,"空存档",Vector2.zero,new Vector2(350,56),32,Ink);
            }
            Label("SaveStatus",saves,"当前版本尚未接入存档",new Vector2(0,-135),new Vector2(400,32),14,Ink,false);

            var resolution=SubCard(pages[5].transform,"分辨率",view);
            var sizes=new[]{"1280 × 720","1600 × 900","1920 × 1080"};
            for(var i=0;i<3;i++)
            {
                var button=Button("Resolution"+i,resolution,sizes[i],new Vector2(0,105-i*85),new Vector2(340,58),Cream,Ink,32);
                UnityEventTools.AddIntPersistentListener(button.onClick,view.SetResolution,i);
            }
            Set(view,"resolutionText",Label("ResolutionValue",resolution,"1280 × 720",new Vector2(0,-128),new Vector2(430,32),15,Ink,false));
            var so=new SerializedObject(view); var array=so.FindProperty("pages"); array.arraySize=pages.Length;
            for(var i=0;i<pages.Length;i++) { array.GetArrayElementAtIndex(i).objectReferenceValue=pages[i]; WrapModalContent(pages[i]); pages[i].SetActive(false); }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform SubCard(Transform parent,string title,ReferenceUiView view)
        {
            var card=Illustration(title+"Card",parent,"CatalogFrame5",Vector2.zero,new Vector2(738,517));
            Label("Title",card,title,new Vector2(0,185),new Vector2(300,66),48,Ink);
            Click(Button("BackButton",card,"返回",new Vector2(0,-180),new Vector2(164,52),Cream,Ink,38),view.BackToSettings);
            return card;
        }

        private static void BuildBag(Transform parent,ReferenceUiView view)
        {
            var bag=Panel("BagCard",parent,Vector2.one*0.5f,new Vector2(-6,28),new Vector2(556,566),Cocoa,StorybookSurface.SurfaceKind.CutCorner);
            var inset=Panel("SlotInset",bag,Vector2.one*0.5f,new Vector2(0,-22),new Vector2(438,450),new Color(0.66f,0.62f,0.43f));
            for(var row=0;row<3;row++) for(var col=0;col<3;col++)
                Panel("BagSlot_"+row+"_"+col,inset,Vector2.one*0.5f,new Vector2((col-1)*138,(1-row)*137),new Vector2(125,126),Cream);
            Doodle(bag,new Vector2(-32,262),new Vector2(106,104),Mint,-6);
            var title=Panel("BagTitle",bag,Vector2.one*0.5f,new Vector2(166,267),new Vector2(280,72),Cream,StorybookSurface.SurfaceKind.Ribbon);
            title.localRotation=Quaternion.Euler(0,0,-4); Label("Title",title,"背包",Vector2.zero,new Vector2(250,62),52,Ink);
            var previous=Button("BagPrevious",bag,"←",new Vector2(-296,0),new Vector2(84,56),Moss,Cream,43); previous.interactable=false;
            var next=Button("BagNext",bag,"→",new Vector2(266,0),new Vector2(80,56),Moss,Cream,43); next.interactable=false;
            Label("BagPagination",bag,"1 / 1",new Vector2(0,-258),new Vector2(120,42),34,Cream);
            Label("BagEmpty",parent,"背包内容待接入",new Vector2(0,-278),new Vector2(330,26),13,Ink,false);
            var back=Button("BagBackButton",parent,"返回",new Vector2(-110,63),new Vector2(156,68),Cocoa,Cream,48); Anchor(back.transform,new Vector2(1,0)); Click(back,view.Close);
        }

        private static void BuildCatalog(Transform parent,ReferenceUiView view)
        {
            var board=Illustration("CatalogBoard",parent,"CatalogBoard",Vector2.zero,new Vector2(1270,720));
            var catalog=board.gameObject.AddComponent<TapTapCatalogView>();
            var positions=new[]{new Vector2(-367,128),new Vector2(-106,129),new Vector2(150,123),new Vector2(-291,-118),new Vector2(77,-120),new Vector2(419,0)};
            var sizes=new[]{new Vector2(224,217),new Vector2(224,222),new Vector2(252,243),new Vector2(394,244),new Vector2(334,234),new Vector2(270,414)};
            for(var i=0;i<6;i++) Illustration("CatalogFrame"+(i+1),board,"CatalogFrame"+(i+1),positions[i],sizes[i]);
            var content=new GameObject[3];
            var captions=new[]{"待收录","食物","熔炉","障碍","待收录","史莱姆"};
            var icons=new[]{null,Art("Food"),Art("Furnace"),Art("Obstacle"),null,Art("Slime")};
            for(var page=0;page<content.Length;page++)
            {
                var group=Rect("CatalogContent"+(page+1),board); Stretch(group); content[page]=group.gameObject;
                for(var i=0;i<6;i++)
                {
                    if(page==0 && icons[i]!=null)
                    {
                        var icon=Rect("Illustration"+i,group); icon.anchoredPosition=positions[i]+new Vector2(0,12);
                        icon.sizeDelta=new Vector2(sizes[i].x*0.60f,Mathf.Min(135,sizes[i].y*0.60f));
                        var image=icon.gameObject.AddComponent<Image>(); image.sprite=icons[i]; image.preserveAspect=true; image.raycastTarget=false;
                    }
                    Label("Caption"+i,group,page==0?captions[i]:"待收录",positions[i]+new Vector2(0,-sizes[i].y/2+(i==2?29:16)),new Vector2(154,31),25,Ink);
                }
                group.gameObject.SetActive(page==0);
            }
            var pagination=Illustration("CatalogPagination",board,"CatalogPage1",new Vector2(0,-288),new Vector2(248,68)).GetComponent<Image>();
            var left=HitButton("CatalogPrevious",pagination.transform,new Vector2(-83,0),new Vector2(68,60));
            var right=HitButton("CatalogNext",pagination.transform,new Vector2(83,0),new Vector2(68,60));
            Click(left,catalog.Previous); Click(right,catalog.Next);
            Click(ArtButton("CatalogBackButton",board,"CatalogBack",new Vector2(511,-294),new Vector2(240,107)),view.Close);
            Set(catalog,"pagination",pagination); Set(catalog,"previous",left); Set(catalog,"next",right);
            var so=new SerializedObject(catalog); var groups=so.FindProperty("contentPages"); groups.arraySize=3;
            var sprites=so.FindProperty("paginationSprites"); sprites.arraySize=3;
            for(var i=0;i<3;i++)
            {
                groups.GetArrayElementAtIndex(i).objectReferenceValue=content[i];
                sprites.GetArrayElementAtIndex(i).objectReferenceValue=TapTapUiArt.Sprite("CatalogPage"+(i+1));
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Photo(Transform parent,string caption,Vector2 position,Vector2 size,float angle,Sprite sprite)
        {
            var frame=Panel("Photo_"+caption,parent,Vector2.one*0.5f,position,size,Cream);
            frame.localRotation=Quaternion.Euler(0,0,angle);
            var image=Panel("PhotoInset",frame,Vector2.one*0.5f,Vector2.zero,size-new Vector2(34,34),new Color(0.70f,0.76f,0.51f));
            image.GetComponent<Image>().raycastTarget=false;
            if(sprite!=null)
            {
                var art=Rect("Portrait",image); Stretch(art); art.offsetMin=new Vector2(12,18); art.offsetMax=new Vector2(-12,-18);
                var graphic=art.gameObject.AddComponent<Image>(); graphic.sprite=sprite; graphic.preserveAspect=true; graphic.raycastTarget=false;
            }
            else Label("AwaitingArt",image,"待收录",Vector2.zero,new Vector2(size.x-50,44),24,Ink);
            var tag=Panel("CaptionRibbon",frame,Vector2.one*0.5f,new Vector2(0,-size.y/2+5),new Vector2(164,45),Gold,StorybookSurface.SurfaceKind.Ribbon);
            tag.localRotation=Quaternion.Euler(0,0,-angle); Label("Caption",tag,caption,Vector2.zero,new Vector2(132,42),36,Ink);
        }

        public static void BuildMainMenu()
        {
            LoadAssets();
            var root=CanvasRoot("PF_ReferenceMainMenu",10);
            var menu=root.gameObject.AddComponent<ReferenceMainMenuView>(); var entry=root.gameObject.AddComponent<MainMenuLevelEntry>();
            var home=Rect("Home",Safe(root)); Stretch(home);
            var background=Illustration("CoverBackground",root,"HomeBackground",Vector2.zero,new Vector2(1280,720));
            Stretch(background);
            background.GetComponent<Image>().preserveAspect=false;
            var fitter=background.gameObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent; fitter.aspectRatio=16f/9f;
            background.SetAsFirstSibling();
            SourceArt("CoverTitle",home,"HomeTitle");
            var newGame=SourceButton("NewGameButton",home,"HomeNew"); SetHover(newGame,"HomeNewHover"); Click(newGame,menu.OpenLevels);
            var continueButton=SourceButton("ContinueButton",home,"HomeContinue"); SetHover(continueButton,"HomeContinueHover"); continueButton.interactable=false;
            var quit=SourceButton("MenuQuitButton",home,"HomeQuit"); SetHover(quit,"HomeQuitHover"); Click(quit,menu.Quit);
            Label("NoSaveHint",continueButton.transform,"暂无存档",new Vector2(0,-49),new Vector2(160,24),12,Ink,false);

            var levels=Modal("LevelSelection",root,20);
            var cards=new RectTransform[3];
            for(var i=0;i<3;i++)
            {
                var card=Illustration("LevelCard"+(i+1),levels.transform,"Chapter"+(i+1),new Vector2(-414+i*414,-3),new Vector2(362,350));
                cards[i]=card;
                var click=card.gameObject.AddComponent<Button>(); click.targetGraphic=card.GetComponent<Image>(); click.navigation=new Navigation { mode=Navigation.Mode.None };
                card.GetComponent<Image>().raycastTarget=true;
                // Selection owns card scale; do not attach hover motion to the same transform.
                UnityEventTools.AddIntPersistentListener(click.onClick,menu.SelectLevel,i);
                if(i==2) Label("ComingSoon",card,"尚未开放",new Vector2(0,0),new Vector2(230,46),30,Ink);
            }
            var back=ArtButton("LevelBackButton",levels.transform,"ChapterBack",new Vector2(123,-71),new Vector2(212,109)); Anchor(back.transform,new Vector2(0,1)); Click(back,menu.Back);
            var select=ArtButton("SelectLevelButton",levels.transform,"ChapterSelect",new Vector2(0,-260),new Vector2(305,111)); Click(select,menu.StartSelectedLevel);
            var hint=Label("SelectionHint",levels.transform,"",new Vector2(0,-189),new Vector2(400,34),21,Ink);
            Set(menu,"home",home.gameObject); Set(menu,"levels",levels); Set(menu,"selectButton",select.gameObject); Set(menu,"selectionHint",hint); Set(menu,"entry",entry);
            SetBool(menu,"useIllustratedCards",true);
            var so=new SerializedObject(menu); var array=so.FindProperty("cards"); array.arraySize=cards.Length;
            for(var i=0;i<cards.Length;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=cards[i]; so.ApplyModifiedPropertiesWithoutUndo();
            WrapModalContent(levels); levels.SetActive(false); select.gameObject.SetActive(false);
            SaveUiPrefab(root.gameObject,MenuPath); Object.DestroyImmediate(root.gameObject);
        }

        public static void SaveUiPrefab(GameObject root,string path)
        {
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
            // A live overlay canvas can be driven to scale zero when the batch editor has no
            // Game view. Normalize the persistent asset, where canvas layout cannot override it.
            var transform=new SerializedObject(prefab.transform);
            transform.FindProperty("m_LocalScale").vector3Value=Vector3.one;
            transform.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(prefab);
        }

        [MenuItem("SlimeBattle/UI/Apply Reference Main Menu")]
        public static void ApplyMainMenu()
        {
            BuildMainMenu();
            const string path="Assets/Game/Scenes/MainMenu.unity";
            var scene=SceneManager.GetSceneByPath(path);
            var wasLoaded=scene.IsValid() && scene.isLoaded;
            if(wasLoaded && scene.isDirty) throw new System.InvalidOperationException("Save MainMenu before applying the reference layout.");
            if(!wasLoaded) scene=EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                foreach(var go in scene.GetRootGameObjects())
                {
                    foreach(var canvas in go.GetComponentsInChildren<Canvas>(true)) if(canvas.name=="MenuCanvas") canvas.gameObject.SetActive(false);
                    if(go.name=="PF_ReferenceMainMenu") Object.DestroyImmediate(go);
                }
                var menu=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath),scene);
                menu.transform.localScale=Vector3.one;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if(!wasLoaded) EditorSceneManager.CloseScene(scene,true); }
        }

        [MenuItem("SlimeBattle/UI/Capture Reference UI Layouts")]
        public static void CaptureLayouts()
        {
            var preview=EditorSceneManager.NewPreviewScene();
            var owner=new GameObject("TEST_ONLY_ReferenceLayouts"); SceneManager.MoveGameObjectToScene(owner,preview);
            var cameraObject=new GameObject("CaptureCamera",typeof(Camera)); cameraObject.transform.SetParent(owner.transform,false);
            cameraObject.transform.position=new Vector3(0,0,-10);
            var camera=cameraObject.GetComponent<Camera>(); camera.scene=preview; camera.orthographic=true; camera.orthographicSize=6;
            var hud=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleHudView>(FirstLevelUiPrefabBuilder.HudPath),owner.transform);
            var menu=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceUiPrefabBuilder.MenuPath),owner.transform);
            try
            {
                var pages=hud.GetComponent<ReferenceUiView>();
                menu.SetActive(false);
                hud.Render(new UiState(default,LevelState.Paused,"",12,slimeLimit:20),false,false,false,false);
                foreach(ReferenceUiView.Page page in System.Enum.GetValues(typeof(ReferenceUiView.Page)))
                {
                    if(page==ReferenceUiView.Page.None) continue;
                    pages.Open(page);
                    foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1920,1080),new Vector2Int(1920,820)})
                        Capture(camera,owner,size,CaptureFolder+"/"+page+"-"+size.x+"x"+size.y+".png");
                    if(page==ReferenceUiView.Page.Catalog)
                    {
                        var catalog=hud.GetComponentInChildren<TapTapCatalogView>(true);
                        catalog.Next(); Capture(camera,owner,new Vector2Int(1280,720),CaptureFolder+"/CatalogPage2.png");
                        catalog.Next(); Capture(camera,owner,new Vector2Int(1280,720),CaptureFolder+"/CatalogPage3.png");
                    }
                }
                pages.CloseSilently(); hud.gameObject.SetActive(false); menu.SetActive(true);
                var main=menu.GetComponent<ReferenceMainMenuView>(); main.Back();
                Capture(camera,owner,new Vector2Int(1280,720),CaptureFolder+"/MainMenu.png");
                main.OpenLevels(); Capture(camera,owner,new Vector2Int(1280,720),CaptureFolder+"/Levels.png");
                main.SelectLevel(1); Capture(camera,owner,new Vector2Int(1280,720),CaptureFolder+"/LevelSelected.png");
                foreach(var size in new[]{new Vector2Int(1024,768),new Vector2Int(1920,1080),new Vector2Int(1920,820)})
                {
                    main.Back(); Capture(camera,owner,size,CaptureFolder+"/MainMenu-"+size.x+"x"+size.y+".png");
                    main.OpenLevels(); main.SelectLevel(0); Capture(camera,owner,size,CaptureFolder+"/Levels-"+size.x+"x"+size.y+".png");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static void Capture(Camera camera,GameObject owner,Vector2Int size,string path)
        {
            foreach(var canvas in owner.GetComponentsInChildren<Canvas>(true))
            { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1; }
            var target=new RenderTexture(size.x,size.y,24); var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0); texture.Apply(); Directory.CreateDirectory(CaptureFolder); File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally { camera.targetTexture=null; RenderTexture.active=previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static Button Tool(Transform parent,string name,string text,float y)
        {
            var b=Button(name,parent,text,new Vector2(0,y),new Vector2(150,64),Cream,Ink,44,StorybookSurface.SurfaceKind.Notched);
            var selected=Panel("Selected",b.transform,Vector2.one*0.5f,new Vector2(-53,0),new Vector2(7,40),Moss);
            selected.GetComponent<Image>().raycastTarget=false; selected.gameObject.SetActive(false);
            return b;
        }
        private static RectTransform Illustration(string name,Transform parent,string key,Vector2 position,Vector2 size)
        {
            var rect=Rect(name,parent); rect.anchoredPosition=position; rect.sizeDelta=size;
            var image=rect.gameObject.AddComponent<Image>(); image.sprite=TapTapUiArt.Sprite(key);
            image.preserveAspect=true; image.raycastTarget=false;
            return rect;
        }
        private static RectTransform SourceArt(string name,Transform parent,string key,Vector2? anchor=null)
        {
            var entry=TapTapUiArt.Find(key); var point=anchor ?? Vector2.one*0.5f;
            const float scale=2f/3f;
            var position=new Vector2((entry.x+entry.w*0.5f-1920*point.x)*scale,
                (1080*(1-point.y)-entry.y-entry.h*0.5f)*scale);
            var rect=Illustration(name,parent,key,position,new Vector2(entry.w,entry.h)*scale);
            rect.anchorMin=rect.anchorMax=point;
            return rect;
        }
        private static Button ArtButton(string name,Transform parent,string key,Vector2 position,Vector2 size)
            => AddArtButton(Illustration(name,parent,key,position,size));
        private static Button SourceButton(string name,Transform parent,string key,Vector2? anchor=null)
            => AddArtButton(SourceArt(name,parent,key,anchor));
        private static Button AddArtButton(RectTransform rect)
        {
            var image=rect.GetComponent<Image>(); image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            button.navigation=new Navigation { mode=Navigation.Mode.None };
            var colors=button.colors; colors.highlightedColor=new Color(1f,0.97f,0.85f);
            colors.pressedColor=new Color(0.90f,0.80f,0.65f); colors.disabledColor=new Color(0.65f,0.65f,0.65f,0.55f); button.colors=colors;
            rect.gameObject.AddComponent<StorybookButtonMotion>();
            return button;
        }
        private static Button IllustratedTool(Transform parent,string name,string key)
        {
            var button=SourceButton(name,parent,key,new Vector2(1,0.5f));
            var selected=Panel("Selected",button.transform,Vector2.one*0.5f,new Vector2(-94,0),new Vector2(6,56),Gold);
            selected.GetComponent<Image>().raycastTarget=false; selected.gameObject.SetActive(false);
            return button;
        }
        private static Button HitButton(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var rect=Rect(name,parent); rect.anchoredPosition=position; rect.sizeDelta=size;
            var image=rect.gameObject.AddComponent<Image>(); image.color=Color.clear;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            button.navigation=new Navigation { mode=Navigation.Mode.None };
            return button;
        }
        private static void SetHover(Button button,string key)
        {
            button.transition=Selectable.Transition.SpriteSwap;
            var state=button.spriteState; state.highlightedSprite=state.pressedSprite=state.selectedSprite=TapTapUiArt.Sprite(key);
            button.spriteState=state;
            if(button.name=="ContinueButton") button.GetComponent<Image>().color=new Color(1,1,1,0.5f);
        }
        private static Sprite Art(string name) => AssetDatabase.LoadAllAssetsAtPath(FirstLevelUiPrefabBuilder.AtlasPath).OfType<Sprite>().FirstOrDefault(s=>s.name==name);
        private static RectTransform CanvasRoot(string name,int order)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            // Screen-space Canvas roots must keep a unit transform so their child
            // controls remain visible and receive real pointer raycasts after
            // the prefab is saved and instantiated in a gameplay scene.
            go.transform.localScale=Vector3.one;
            var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=order;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            return (RectTransform)go.transform;
        }
        private static RectTransform Safe(Transform parent) { var rect=Rect("SafeArea",parent); Stretch(rect); rect.gameObject.AddComponent<UiSafeArea>(); return rect; }
        private static GameObject Modal(string name,Transform parent,int order)
        {
            var rect=Rect(name,parent); Stretch(rect);
            var canvas=rect.gameObject.AddComponent<Canvas>(); canvas.overrideSorting=true; canvas.sortingOrder=order;
            rect.gameObject.AddComponent<GraphicRaycaster>();
            var image=rect.gameObject.AddComponent<Image>(); image.sprite=TapTapUiArt.Sprite("HomeBackground"); image.material=backdrop;
            var veil=Panel("BackdropWash",rect,Vector2.one*0.5f,Vector2.zero,Vector2.zero,new Color(0.96f,0.94f,0.83f,0.20f)); Stretch(veil); veil.GetComponent<Image>().raycastTarget=false;
            var content=Rect("ModalSafeArea",rect); Stretch(content); content.gameObject.AddComponent<UiSafeArea>();
            return rect.gameObject;
        }
        private static RectTransform Rect(string name,Transform parent)
        { var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); return (RectTransform)go.transform; }
        private static void WrapModalContent(GameObject modal)
        {
            var safe=modal.transform.Find("ModalSafeArea");
            foreach(var child in modal.transform.Cast<Transform>().Where(t=>t!=safe && t.name!="BackdropWash").ToArray())
            {
                var rect=(RectTransform)child; var position=rect.anchoredPosition; var size=rect.sizeDelta;
                child.SetParent(safe,false); rect.anchoredPosition=position; rect.sizeDelta=size;
            }
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static void Anchor(Transform item,Vector2 anchor) { var rect=(RectTransform)item; rect.anchorMin=rect.anchorMax=anchor; }
        private static RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Color color,StorybookSurface.SurfaceKind kind=StorybookSurface.SurfaceKind.Flat)
        {
            var rect=Rect(name,parent); rect.anchorMin=rect.anchorMax=anchor; rect.anchoredPosition=position; rect.sizeDelta=size;
            var graphic=rect.gameObject.AddComponent<StorybookSurface>(); graphic.Surface=kind; graphic.color=color;
            return rect;
        }
        private static Text Label(string name,Transform parent,string content,Vector2 position,Vector2 size,int fontSize,Color color,bool hand=true)
        {
            var rect=Rect(name,parent); rect.anchorMin=rect.anchorMax=Vector2.one*0.5f; rect.anchoredPosition=position; rect.sizeDelta=size;
            var text=rect.gameObject.AddComponent<Text>(); text.font=hand?handwriting:body; text.text=content; text.fontSize=fontSize; text.color=color;
            text.raycastTarget=false; text.supportRichText=false; text.alignment=TextAnchor.MiddleCenter;
            text.resizeTextForBestFit=true; text.resizeTextMinSize=Mathf.Max(10,fontSize/2); text.resizeTextMaxSize=fontSize;
            return text;
        }
        private static Button Button(string name,Transform parent,string text,Vector2 position,Vector2 size,Color fill,Color ink,int fontSize,StorybookSurface.SurfaceKind kind=StorybookSurface.SurfaceKind.Flat)
        {
            var rect=Panel(name,parent,Vector2.one*0.5f,position,size,fill,kind);
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>();
            var colors=button.colors; colors.highlightedColor=new Color(1f,0.95f,0.76f); colors.pressedColor=new Color(0.88f,0.79f,0.56f); colors.disabledColor=new Color(0.78f,0.79f,0.70f,0.60f); button.colors=colors;
            button.navigation=new Navigation { mode=Navigation.Mode.None }; rect.gameObject.AddComponent<StorybookButtonMotion>();
            Label("Label",rect,text,Vector2.zero,size-new Vector2(14,4),fontSize,ink);
            return button;
        }
        private static void Click(Button button,UnityAction action) { UnityEventTools.AddPersistentListener(button.onClick,action); }
        private static void Doodle(Transform parent,Vector2 position,Vector2 size,Color color,float angle,bool sprout=false,bool blob=true)
        {
            var rect=Rect(blob?"SlimeDoodle":"GrassDoodle",parent); rect.anchorMin=rect.anchorMax=Vector2.one*0.5f;
            rect.anchoredPosition=position; rect.sizeDelta=size; rect.localRotation=Quaternion.Euler(0,0,angle);
            var graphic=rect.gameObject.AddComponent<SlimeDoodleGraphic>(); graphic.color=color; graphic.Sprout=sprout; graphic.Grass=!blob; graphic.raycastTarget=false;
        }
        private static void Set(Object owner,string name,Object value) { var so=new SerializedObject(owner); so.FindProperty(name).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetBool(Object owner,string name,bool value) { var so=new SerializedObject(owner); so.FindProperty(name).boolValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
