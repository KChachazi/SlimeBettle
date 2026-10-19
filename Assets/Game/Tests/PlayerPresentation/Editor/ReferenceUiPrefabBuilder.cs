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
    /// <summary>Reference composition with the project's full-color forest and editable uGUI controls.</summary>
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
            backdrop.SetFloat("_Blur",2.5f);
            EditorUtility.SetDirty(backdrop);
        }

        public static PlacementSelectionView BuildSelection()
        {
            LoadAssets();
            var root=CanvasRoot("PF_FirstLevelPlacementSelection",21);
            var view=root.gameObject.AddComponent<PlacementSelectionView>();
            var panel=Rect("PlacementPanel",Safe(root)); Stretch(panel);
            var toolbar=Rect("Toolbar",panel); toolbar.anchorMin=toolbar.anchorMax=new Vector2(1,0.5f);
            toolbar.anchoredPosition=new Vector2(-61,0); toolbar.sizeDelta=new Vector2(122,720);
            var food=Tool(toolbar,"FoodButton","食物",194);
            var furnace=Tool(toolbar,"FurnaceButton","熔炉",83);
            var obstacle=Tool(toolbar,"ObstacleButton","障碍",-29);
            var cancel=Button("CancelButton",panel,"取消放置",new Vector2(-148,47),new Vector2(174,44),Gold,Ink,26);
            Anchor(cancel.transform,new Vector2(1,0));
            var budget=Label("Budget",panel,"放置配额未接入",new Vector2(-283,-74),new Vector2(224,26),13,Ink,false);
            Anchor(budget.transform,new Vector2(1,1));
            var hintCard=Panel("PlacementHint",panel,new Vector2(0.5f,0),new Vector2(0,35),new Vector2(450,40),new Color(Cocoa.r,Cocoa.g,Cocoa.b,0.94f));
            var hint=Label("Hint",hintCard,"选择机关，再点击地图放置",Vector2.zero,new Vector2(426,30),15,Cream,false);
            Set(view,"panel",panel.gameObject); Set(view,"hint",hint); Set(view,"budget",budget);
            Set(view,"food",food); Set(view,"furnace",furnace); Set(view,"obstacle",obstacle); Set(view,"cancel",cancel);
            Set(view,"foodSelected",food.transform.Find("Selected").gameObject);
            Set(view,"furnaceSelected",furnace.transform.Find("Selected").gameObject);
            Set(view,"obstacleSelected",obstacle.transform.Find("Selected").gameObject);
            SetBool(view,"useChineseLabels",true); SetBool(view,"useReferenceLayout",true);
            return view;
        }

        public static BattleHudView BuildHud()
        {
            LoadAssets();
            var root=CanvasRoot("PF_FirstLevelHUD",20);
            var view=root.gameObject.AddComponent<BattleHudView>();
            var pages=root.gameObject.AddComponent<ReferenceUiView>();
            var hud=Rect("HUD",Safe(root)); Stretch(hud);
            var strip=Panel("ToolbarBacking",hud,new Vector2(1,0.5f),new Vector2(-33,0),new Vector2(70,760),Cocoa);
            strip.localRotation=Quaternion.Euler(0,0,3); strip.GetComponent<Image>().raycastTarget=false;
            var pause=Button("PauseButton",hud,"设置",new Vector2(65,-62),new Vector2(94,70),Cocoa,Cream,48);
            Anchor(pause.transform,new Vector2(0,1));
            var counter=Panel("SlimeCounter",hud,new Vector2(1,1),new Vector2(-283,-33),new Vector2(224,48),Cocoa);
            var counts=Label("Counts",counter,"史莱姆数量 -- / --",Vector2.zero,new Vector2(214,42),30,Cream);
            var bag=Button("BagButton",hud,"背包",new Vector2(-215,72),new Vector2(132,124),Moss,Ink,55);
            Anchor(bag.transform,new Vector2(1,0)); bag.transform.localRotation=Quaternion.Euler(0,0,-1);
            Click(bag,pages.OpenBag);
            var catalog=Button("CatalogButton",hud,"图鉴",new Vector2(-65,91),new Vector2(142,62),Cream,Ink,44,StorybookSurface.SurfaceKind.Notched);
            Anchor(catalog.transform,new Vector2(1,0)); Click(catalog,pages.OpenCatalog);
            var remove=Button("RemoveButton",hud,"拆除",new Vector2(-65,203),new Vector2(96,82),Cocoa,Cream,38);
            Anchor(remove.transform,new Vector2(1,0)); Click(remove,pages.ToggleRemoval); Set(pages,"removeButton",remove);
            var hint=Label("FlowHint",hud,"",new Vector2(0,82),new Vector2(520,28),14,Cream,false);
            Anchor(hint.transform,new Vector2(0.5f,0));
            var feedback=Label("FeatureFeedback",hud,"",new Vector2(0,111),new Vector2(520,28),14,Cream,false);
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
            var card=Panel("Card",pausePanel.transform,Vector2.one*0.5f,new Vector2(0,26),new Vector2(556,568),Moss);
            var title=Panel("TitleRibbon",card,Vector2.one*0.5f,new Vector2(-160,251),new Vector2(350,74),Cream);
            title.localRotation=Quaternion.Euler(0,0,9);
            Label("Heading",title,"游戏暂停",Vector2.zero,new Vector2(328,64),53,Ink);
            for(var i=0;i<3;i++) Doodle(card,new Vector2(-286,147-i*70),new Vector2(72,65),Cream,i*12-8);
            Doodle(card,new Vector2(263,-244),new Vector2(150,145),Mint,-8,true);
            var resume=Button("ResumeButton",card,"开始游戏",new Vector2(0,54),new Vector2(388,66),Cocoa,Cream,53);
            var quit=Button("QuitButton",card,"退出",new Vector2(0,-74),new Vector2(388,70),Cocoa,Cream,52);
            Click(quit,pages.Quit);
            // Keep the existing restart command available without displacing the reference's two primary actions.
            var restart=Button("RestartButton",card,"重新开始",new Vector2(0,-185),new Vector2(168,38),Moss,Cream,26);

            var resultPanel=Modal("ResultPanel",root,40);
            var resultCard=Panel("Card",resultPanel.transform,Vector2.one*0.5f,Vector2.zero,new Vector2(566,486),Board);
            Doodle(resultCard,new Vector2(-250,192),new Vector2(105,100),Mint,-12,true);
            var result=Label("Result",resultCard,"",new Vector2(0,163),new Vector2(490,70),52,Cream);
            var detailViewport=Rect("ResultReasonViewport",resultCard); detailViewport.anchoredPosition=new Vector2(0,44); detailViewport.sizeDelta=new Vector2(494,108);
            detailViewport.gameObject.AddComponent<RectMask2D>(); detailViewport.gameObject.AddComponent<Image>().color=Color.clear;
            var detail=Label("ResultDetail",detailViewport,"",Vector2.zero,new Vector2(476,108),17,Cream,false);
            detail.resizeTextForBestFit=false; detail.rectTransform.anchorMin=new Vector2(0,1); detail.rectTransform.anchorMax=new Vector2(1,1);
            detail.rectTransform.pivot=new Vector2(0.5f,1); detail.rectTransform.sizeDelta=new Vector2(-18,108); detail.alignment=TextAnchor.UpperCenter;
            detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=detailViewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=detailViewport; scroll.content=detail.rectTransform;
            scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=24;
            var resultCounts=Label("ResultCounts",resultCard,"",new Vector2(0,-65),new Vector2(500,72),17,Cream,false);
            var resultRestart=Button("RestartButton",resultCard,"再来一次",new Vector2(0,-167),new Vector2(300,58),Gold,Ink,38);

            BuildPages(root,pages);
            WrapModalContent(pausePanel); WrapModalContent(resultPanel);
            Set(view,"hudPanel",hud.gameObject); Set(view,"pausePanel",pausePanel); Set(view,"resultPanel",resultPanel); Set(view,"referencePages",pages);
            Set(view,"objectiveText",objective); Set(view,"countsText",counts); Set(view,"weaponText",weapon); Set(view,"hintText",hint);
            Set(view,"goalText",goal); Set(view,"waitingText",waiting); Set(view,"routeHintText",route); Set(view,"resultCountsText",resultCounts);
            Set(view,"resultText",result); Set(view,"resultDetailText",detail); Set(view,"pauseButton",pause); Set(view,"resumeButton",resume);
            Set(view,"pauseRestartButton",restart); Set(view,"resultRestartButton",resultRestart); SetBool(view,"useChineseLabels",true);
            pausePanel.SetActive(false); resultPanel.SetActive(false);
            return view;
        }

        private static void BuildPages(Transform root,ReferenceUiView view)
        {
            var pages=new GameObject[6];
            for(var i=0;i<pages.Length;i++) pages[i]=Modal(((ReferenceUiView.Page)(i+1))+"Panel",root,45);
            BuildBag(pages[0].transform,view);
            BuildCatalog(pages[1].transform,view);
            var settings=Panel("SettingsCard",pages[2].transform,Vector2.one*0.5f,new Vector2(0,28),new Vector2(398,568),Board);
            var heading=Panel("SettingsTitle",settings,Vector2.one*0.5f,new Vector2(0,264),new Vector2(226,68),Cream);
            Label("Title",heading,"设置",Vector2.zero,new Vector2(208,60),54,Ink);
            var actions=new UnityAction[]{view.OpenVolume,view.OpenSaves,view.OpenResolution};
            var words=new[]{"音量","存档","分辨率"};
            for(var i=0;i<3;i++)
            {
                var y=131-i*127;
                Doodle(settings,new Vector2(i%2==0?-185:175,y),new Vector2(126,122),i==1?Mint:Cream,i*9-4);
                Click(Button(words[i]+"Button",settings,words[i],new Vector2(3,y),new Vector2(254,64),Cocoa,Cream,49),actions[i]);
            }
            Click(Button("SettingsBackButton",settings,"退出",new Vector2(175,-260),new Vector2(106,68),Cream,Ink,43),view.Close);

            var volume=SubCard(pages[3].transform,"音量",view);
            Label("VolumeDescription",volume,"主音量",new Vector2(0,100),new Vector2(330,40),29,Cream);
            var sliderRect=Panel("VolumeSlider",volume,Vector2.one*0.5f,new Vector2(0,10),new Vector2(330,26),Cocoa);
            var slider=sliderRect.gameObject.AddComponent<Slider>(); slider.minValue=0; slider.maxValue=1; slider.value=1;
            var fillArea=Rect("FillArea",sliderRect); Stretch(fillArea); fillArea.offsetMin=new Vector2(10,5); fillArea.offsetMax=new Vector2(-10,-5);
            var fill=Panel("Fill",fillArea,Vector2.zero,Vector2.zero,Vector2.zero,Gold); Stretch(fill);
            var handleArea=Rect("HandleArea",sliderRect); Stretch(handleArea); handleArea.offsetMin=new Vector2(10,0); handleArea.offsetMax=new Vector2(-10,0);
            var handle=Panel("Handle",handleArea,Vector2.one*0.5f,Vector2.zero,new Vector2(24,44),Cream);
            slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<Image>();
            slider.navigation=new Navigation { mode=Navigation.Mode.None }; UnityEventTools.AddPersistentListener(slider.onValueChanged,view.SetVolume);
            Set(view,"volumeSlider",slider); Set(view,"volumeText",Label("VolumeValue",volume,"100 %",new Vector2(0,-63),new Vector2(330,46),37,Cream));

            var saves=SubCard(pages[4].transform,"存档",view);
            for(var i=0;i<3;i++)
            {
                var slot=Panel("SaveSlot"+(i+1),saves,Vector2.one*0.5f,new Vector2(0,105-i*95),new Vector2(390,72),Cream);
                Label("SlotLabel",slot,"空存档",Vector2.zero,new Vector2(350,56),32,Ink);
            }
            Label("SaveStatus",saves,"当前版本尚未接入存档",new Vector2(0,-177),new Vector2(400,32),14,Cream,false);

            var resolution=SubCard(pages[5].transform,"分辨率",view);
            var sizes=new[]{"1280 × 720","1600 × 900","1920 × 1080"};
            for(var i=0;i<3;i++)
            {
                var button=Button("Resolution"+i,resolution,sizes[i],new Vector2(0,105-i*85),new Vector2(340,58),Cream,Ink,32);
                UnityEventTools.AddIntPersistentListener(button.onClick,view.SetResolution,i);
            }
            Set(view,"resolutionText",Label("ResolutionValue",resolution,"1280 × 720",new Vector2(0,-162),new Vector2(430,32),15,Cream,false));
            var so=new SerializedObject(view); var array=so.FindProperty("pages"); array.arraySize=pages.Length;
            for(var i=0;i<pages.Length;i++) { array.GetArrayElementAtIndex(i).objectReferenceValue=pages[i]; WrapModalContent(pages[i]); pages[i].SetActive(false); }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform SubCard(Transform parent,string title,ReferenceUiView view)
        {
            var card=Panel(title+"Card",parent,Vector2.one*0.5f,Vector2.zero,new Vector2(530,500),Board);
            var heading=Panel("TitleRibbon",card,Vector2.one*0.5f,new Vector2(0,230),new Vector2(260,72),Cream,StorybookSurface.SurfaceKind.Ribbon);
            Label("Title",heading,title,Vector2.zero,new Vector2(230,62),48,Ink);
            Click(Button("BackButton",card,"返回",new Vector2(0,-220),new Vector2(164,58),Cream,Ink,38),view.BackToSettings);
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
            var board=Panel("CatalogBoard",parent,Vector2.one*0.5f,Vector2.zero,new Vector2(1280,646),Board);
            var title=Panel("CatalogTitle",board,new Vector2(0,1),new Vector2(115,-25),new Vector2(232,80),Cream);
            Label("Title",title,"图鉴",Vector2.zero,new Vector2(210,74),54,Ink);
            Photo(board,"守卫",new Vector2(-432,112),new Vector2(216,206),-6,null);
            Photo(board,"1",new Vector2(-155,131),new Vector2(216,206),4,Art("Food"));
            Photo(board,"3",new Vector2(154,115),new Vector2(220,206),-13,Art("Furnace"));
            Photo(board,"2",new Vector2(-341,-145),new Vector2(374,240),3,Art("Obstacle"));
            Photo(board,"4",new Vector2(70,-138),new Vector2(290,218),0,null);
            Photo(board,"5",new Vector2(447,-6),new Vector2(254,520),-2,Art("Slime"));
            var left=Button("CatalogPrevious",board,"←",new Vector2(-16,-292),new Vector2(48,42),Board,Cream,38); left.interactable=false;
            Label("CatalogPagination",board,"1 / 1",new Vector2(74,-292),new Vector2(100,40),34,Cream);
            var right=Button("CatalogNext",board,"→",new Vector2(164,-292),new Vector2(48,42),Board,Cream,38); right.interactable=false;
            var back=Button("CatalogBackButton",parent,"退出",new Vector2(-77,35),new Vector2(156,64),Cream,Ink,48); Anchor(back.transform,new Vector2(1,0)); Click(back,view.Close);
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
            var background=Panel("CoverBackground",home,Vector2.one*0.5f,Vector2.zero,new Vector2(1600,1000),new Color(0.94f,0.95f,0.84f));
            Stretch(background);
            background.GetComponent<Image>().raycastTarget=false;
            var doodles=new[]{new Vector3(-316,300,148),new Vector3(-522,103,154),new Vector3(48,216,146),new Vector3(65,-6,160),new Vector3(-394,-237,158),new Vector3(56,-235,148),new Vector3(500,126,148),new Vector3(-104,363,100)};
            var angles=new[]{-9,12,-18,8,14,-8,20,-10};
            for(var i=0;i<doodles.Length;i++) Doodle(home,(Vector2)doodles[i],new Vector2(doodles[i].z,doodles[i].z),i%3==0?Mint:i%3==1?new Color(1f,0.89f,0.62f):Cream,angles[i],i==3);
            var plants=new[]{new Vector3(-435,300,112),new Vector3(-566,-270,150),new Vector3(-244,-247,102),new Vector3(253,-279,120),new Vector3(372,302,170),new Vector3(555,328,170),new Vector3(129,177,102),new Vector3(530,15,110),new Vector3(-583,12,90)};
            for(var i=0;i<plants.Length;i++) Doodle(home,(Vector2)plants[i],new Vector2(plants[i].z,plants[i].z),i%2==0?Moss:new Color(0.51f,0.65f,0.40f),i*19,true,false);
            var title=Panel("CoverTitle",home,Vector2.one*0.5f,new Vector2(-212,42),new Vector2(378,380),Board);
            Label("EnglishTitle",title,"Slime\nParade",new Vector2(3,65),new Vector2(360,250),105,Cream);
            Label("ChineseTitle",title,"巡游史莱姆",new Vector2(0,-100),new Vector2(370,90),57,Gold);
            var actions=Panel("MainActions",home,Vector2.one*0.5f,new Vector2(372,-136),new Vector2(218,276),Board);
            Click(Button("NewGameButton",actions,"新游戏",new Vector2(0,86),new Vector2(164,54),Mint,Ink,41),menu.OpenLevels);
            var continueButton=Button("ContinueButton",actions,"· 继续",new Vector2(0,0),new Vector2(164,54),Mint,Ink,41); continueButton.interactable=false;
            Click(Button("MenuQuitButton",actions,"退出",new Vector2(0,-82),new Vector2(164,54),Mint,Ink,43),menu.Quit);
            Label("NoSaveHint",actions,"暂无存档",new Vector2(0,-38),new Vector2(160,24),11,Cream,false);

            var levels=Modal("LevelSelection",root,20);
            var cards=new RectTransform[3];
            var cardColors=new[]{new Color(0.71f,0.79f,0.55f),new Color(0.84f,0.75f,0.55f),new Color(0.69f,0.79f,0.77f)};
            for(var i=0;i<3;i++)
            {
                var card=Panel("LevelCard"+(i+1),levels.transform,Vector2.one*0.5f,new Vector2(-415+i*420,-28),new Vector2(300,300),Cocoa);
                cards[i]=card;
                var click=card.gameObject.AddComponent<Button>(); click.targetGraphic=card.GetComponent<Image>(); click.navigation=new Navigation { mode=Navigation.Mode.None };
                UnityEventTools.AddIntPersistentListener(click.onClick,menu.SelectLevel,i);
                var inset=Panel("LevelPreview",card,Vector2.one*0.5f,Vector2.zero,new Vector2(262,262),cardColors[i]); inset.GetComponent<Image>().raycastTarget=false;
                if(i==0)
                {
                    var preview=Rect("ForestPreview",inset); Stretch(preview); preview.gameObject.AddComponent<RectMask2D>();
                    var illustration=preview.gameObject.AddComponent<Image>(); illustration.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(FirstLevelUiPrefabBuilder.ForestPath); illustration.raycastTarget=false;
                }
                else { Doodle(inset,Vector2.zero,new Vector2(116,112),Cream,i*12); Label("ComingSoon",inset,"尚未开放",new Vector2(0,-65),new Vector2(210,35),22,Ink); }
                var tag=Panel("LevelLabel",card,Vector2.one*0.5f,new Vector2(0,-115),new Vector2(174,61),Cream);
                tag.localRotation=Quaternion.Euler(0,0,i==0?3:-2); Label("Label",tag,i==0?"关卡 1":(i+1).ToString(),Vector2.zero,new Vector2(158,56),44,Ink);
            }
            var back=Button("LevelBackButton",levels.transform,"返回",new Vector2(106,-59),new Vector2(212,82),Cream,Ink,55); Anchor(back.transform,new Vector2(0,1)); Click(back,menu.Back);
            var select=Button("SelectLevelButton",levels.transform,"选择",new Vector2(0,-297),new Vector2(250,65),Cream,Ink,50); Click(select,menu.StartSelectedLevel);
            var hint=Label("SelectionHint",levels.transform,"",new Vector2(0,-235),new Vector2(400,34),21,Ink);
            Set(menu,"home",home.gameObject); Set(menu,"levels",levels); Set(menu,"selectButton",select.gameObject); Set(menu,"selectionHint",hint); Set(menu,"entry",entry);
            var so=new SerializedObject(menu); var array=so.FindProperty("cards"); array.arraySize=cards.Length;
            for(var i=0;i<cards.Length;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=cards[i]; so.ApplyModifiedPropertiesWithoutUndo();
            WrapModalContent(levels); levels.SetActive(false); select.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,MenuPath); Object.DestroyImmediate(root.gameObject);
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
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath),scene);
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
                    foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1920,820)})
                        Capture(camera,owner,size,CaptureFolder+"/"+page+"-"+size.x+"x"+size.y+".png");
                }
                pages.CloseSilently(); hud.gameObject.SetActive(false); menu.SetActive(true);
                var main=menu.GetComponent<ReferenceMainMenuView>(); main.Back();
                Capture(camera,owner,new Vector2Int(1280,720),CaptureFolder+"/MainMenu.png");
                main.OpenLevels(); Capture(camera,owner,new Vector2Int(1280,720),CaptureFolder+"/Levels.png");
                main.SelectLevel(1); Capture(camera,owner,new Vector2Int(1280,720),CaptureFolder+"/LevelSelected.png");
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
            var image=rect.gameObject.AddComponent<Image>(); image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(FirstLevelUiPrefabBuilder.ForestPath); image.material=backdrop;
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
