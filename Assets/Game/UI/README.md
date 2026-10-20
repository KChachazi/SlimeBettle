# TapTap 聚光灯 UI

## 2026-10-21 范围与状态反馈

- 食物和熔炉显示半透明作用范围：食物为绿色、熔炉为橙色，冷却中的源变淡；放置预览使用虚线，不能放置时显示红色。半径直接读取机关快照或选中机关配置，并使用史莱姆的真实 PixelsPerUnit 换算，与游戏的吸引判定一致。
- 饥饿、暴躁和害怕以头顶气泡显示；同一只史莱姆同时具备多个状态时合并显示。气泡跟随对应史莱姆和镜头，邻近气泡自动错开，并保留指向该史莱姆的连线。
- `GameplayFeedbackView` 仅读取公开快照，不改变状态、伤害、吸引规则或库存。覆盖层不挂 GraphicRaycaster，所有图形关闭 raycast；位于游戏 HUD 下层。重开、卸载和解除状态时清理旧提示。
- 该功能随 `FirstLevelPresentationRoot` 接入正式第一关与 Castle，复用已有中文字体，无需重新生成地图或 HUD 预制体。
- `GameplayFeedbackScenePlayModeTests` 从 Bootstrap 进入正式第一关，验证真实半径、缩放、非阻塞输入、移除、三个状态及并存、解除、重开。`../Tests/PlayerPresentation/GameplayFeedbackReview.png` 为测试通过实际 StatusManager 添加三种状态后的运行截图，测试冻结模拟便于对照；正常游戏仍由原有玩法触发状态。
- Unity 2022.3.62f2c1 验证：现有 11 项及新增 3 项 Play Mode 分批通过；UI/Placement、库存与关卡配置 Edit Mode 为 76/77。唯一失败 `SceneBindsProductionContextMapActorsAndCaveRules` 预期 6 个放置锚点、实际 0 个，已在未修改的最新 master `78120e7` 单独复现，相同断言与消息；本轮保留其现有玩法与地图数据。

## 2026-10-20 素材接入

使用工作区 `taptap聚光灯ui/taptap聚光灯ui/` 提供的原图更新界面，取代下文 10 月 16 日的程序绘制版本。

- `Art/TapTap/` 保存 48 张原始 PNG，未修改像素；`source-map.json` 记录原文件、英文资源名和 Sprite 切片矩形。排版参考图不作为游戏贴图导入。
- 主菜单使用森林底图、原始中英标题及三个按钮，悬停/按下使用提供的选择态。章节卡片、返回和选择按钮使用对应素材。第一章进入 `FirstLevel`，第二章进入 `Castle`，第三章仍未开放；无存档时继续按钮停用。
- HUD 使用木质边栏、设置、史莱姆数量牌和三种机关/拆除按钮。食物、障碍按钮右下方显示真实剩余库存，成功放置后扣减，拆除不返还，重开恢复入关库存；库存未接入时显示 `--`。新战役初始食物和食物上限均为 60，障碍初始仍为 5。
- 已移除背包和图鉴的入口、页面与图鉴翻页组件。设置与存档展示页继续使用现有排版。
- 暂停、结果和设置子页复用素材中的纸框，长结果继续支持滚动。原来的暂停恢复、Esc 取消、机关放置/拆除及结果清理逻辑继续使用。
- 设置页底部提供“继续游戏”和“返回主菜单”；暂停及胜利/失败结算页也能返回主菜单。返回经 GameRoot/LevelManager 释放当前关卡，再打开 MainMenu；重新选关会创建新 Context，重复点击在切换期间被拒绝。菜单中的“退出”仍负责退出应用。
- 八张带文字的引导面板与确认图标已经导入备用。`GuideObjective` 写的是旧版护送武器胜利条件，与当前第一关规则不一致，因此没有自动弹出或覆盖后端目标；`ChapterFinish` 也不在没有通关记录时显示。

Unity 菜单 `SlimeBattle > UI > Import and Apply TapTap UI` 可重复导入切片并重建三个 UI Prefab、刷新 MainMenu 中的 UI 实例；不会重建 Player、镜头、地图、玩法 Prefab 或 Build Settings。正式关卡和 Castle 通过嵌套 Prefab 引用获得更新。正式入口仍是 `Bootstrap.unity`。

仅更新游戏 HUD 时使用 `SlimeBattle > UI > Rebuild Gameplay HUD`，同步生成 HUD 和机关选择栏两个预制体。

下文保留早期接入说明与历史验证记录；本次验证以 `TapTapUI-validation.json` 为准。

## 历史版式与接线说明

2026-10-16 根据用户提供的 8 张例图调整版式。用户已明确：**只参考布局，页面仍做成彩色**。森林与四种图标沿用现有彩色插画；面板使用深棕、橄榄绿、奶油纸色与金色选中反馈。灰阶生成草稿未接入项目。

## 资源

| 资源 | 用途 |
| --- | --- |
| `Prefabs/PF_FirstLevelHUD.prefab` | 左上设置、右上数量、右下背包/图鉴，暂停与结果页面 |
| `Prefabs/PF_FirstLevelPlacementSelection.prefab` | 右侧三张机关纸签、配额、放置提示与取消 |
| `Prefabs/PF_ReferenceMainMenu.prefab` | 左侧标题、右下新游戏/继续/退出，三张关卡卡片与放大选择态 |
| `Prefabs/PF_FirstLevelPlayer.prefab` | 输入、镜头、网格、放置与 UI 的预接线组合 |
| `Prefabs/PF_FirstLevelScenery.prefab` | 可选森林装饰背景，不含碰撞、路线或地图数据 |
| `Art/ReferenceBackdrop.shader` / `MAT_ReferenceBackdrop.mat` | 柔焦森林背景，使用现有插画，不是实时镜头抓取 |
| `Fonts/LongCang-Regular.ttf` | 手写标题、按钮与页码，附完整 OFL 许可 |
| `Fonts/NotoSansCJKsc-Regular.otf` | 小字说明、后端反馈与长结果，附完整 OFL 许可 |

`MainMenu.unity` 已接入新主菜单 Prefab；原 `MenuCanvas` 保留为停用对象，原 Bootstrap、GameRoot、场景入口与 EventSystem 保留。正式入口仍从 `Bootstrap.unity` 启动。

布局以 1280×720 为参考，Canvas 使用 Expand；交互内容位于安全区内。文字、插画和装饰关闭 raycast，模态背景拦截地图点击。按钮关闭方向键导航，悬停/按下反馈使用非缩放时间。

## 页面与交互

- 主界面按参考图安排设置、数量、右侧机关纸签、拆除、背包和图鉴，另保留紧凑的目标与后端反馈。
- 背包采用中央外框、3×3 槽位、顶部纸带与史莱姆、左右翻页和右下返回。目前只有一页空槽，不生成或消耗物品。
- 图鉴采用错落旋转相框、大竖相框、底部页码和右下退出。四种已有插画作为展示；缺少插画的位置标记待收录，不包含解锁规则或编造属性。
- 设置采用竖面板、音量/存档/分辨率三行按钮与交错史莱姆。音量支持滑杆；分辨率提供三种桌面尺寸，编辑器内只更新偏好和标签。
- 暂停采用倾斜标题纸条、左侧三只小史莱姆、右下大史莱姆和开始游戏/退出两个主按钮，另保留现有重新开始命令。
- 新游戏进入三卡关卡页；选中卡片放大，出现选择按钮。只有已存在的 `Level_test` 可启动，第 2/3 关标记尚未开放。没有真实存档时继续按钮停用，不显示伪造的 Finish 完成标记。

打开背包、图鉴或设置时，通过既有 `UISystem` 命令请求暂停；关闭从运行状态打开的页面会恢复游戏。原本已暂停时，关闭页面保持暂停。Esc 优先关闭辅助页，其次取消当前放置/拆除模式，空闲时切换暂停。结果、重开或重新运行会清理旧页面。

2026-10-19：`FirstLevel` 已接通拆除。运行时点击右侧“拆除”，指向玩家放置机关所在格时显示红色预览，点击地图即可回收食物、熔炉或障碍；可连续拆除。点击“取消拆除”、再次点击“拆除”、Esc 或选择放置物退出。暂停、结算与重开清除拆除模式。空格、预置场景机关和已失效对象不可拆除；UI 区域点击不会传到地图。回收通过 `MechanismSystem.RequestRemove` 清理机关和阻挡，并沿用配置冷却，数量显示同步刷新；未添加库存返还规则。

开局镜头按当前关卡快照聚焦护送武器/史莱姆，缺少 Actor 快照时使用地图中心，每个新关卡 Context 只初始化一次；不会在普通刷新时打断玩家拖动镜头。放置时仅给当前视野内的道路空格加淡绿色提示，紫色预览表示非道路/越界，提示文字说明中键拖图和滚轮缩放。绿色为道路与占用提示，最终放置仍复核机关后端及冷却。正式场景测试从默认镜头开始，用实际选择按钮和地图点击完成三类机关的放置与拆除，不移动测试镜头寻找可放点。

本轮开局验证见 `Assets/Game/Tests/PlayerPresentation/FirstLevelOpening-validation.json` 及同目录 `FirstLevelOpeningPlacement.png` / `FirstLevelOpeningRemoval.png`：正式关卡与 UI Play Mode 9/9 通过，UI/Placement Edit Mode 65/65 通过；扩大 Edit Mode 回归为 117/126 通过，其余 9 项在相同编辑器场景环境的主分支对照中同名同消息失败，没有新增失败，不代表整个项目测试全绿。

## 集成

将 `PF_FirstLevelPlayer` 拖入集成场景，沿用现有 Bootstrap / LevelManager。默认已包含 `InputSystem`、`UISystem`、`PlayerPresentationRoot`、`FirstLevelPresentationRoot`、`PlacementGridView`、`PlacementInputAdapter`、`FirstLevelMechanismBackend`、`FirstLevelStatusProvider` 和嵌套 HUD/选择栏，不需要再次添加组件。

| Inspector 字段 | 引用 |
| --- | --- |
| `PlayerPresentationRoot.levelManager` | Bootstrap 的 LevelManager |
| `PlayerPresentationRoot.cameraSystem` / `hudView` | 已接线的 GameplayCamera / HUD |
| `PlayerPresentationRoot.createDefaultHud` | false，使用提交的 Prefab |
| `FirstLevelPresentationRoot.levelManager` | 同一个 LevelManager |
| `FirstLevelPresentationRoot.cameraSystem` / `grid` | 已接线的 CameraSystem / PlacementGridAuthoring |
| `FirstLevelPresentationRoot.anchorMapDefinition` | 已接线的 MAP_FirstLevel；必须与地图使用同一设计像素比例 |
| `FirstLevelPresentationRoot.placementBackend` / `statusSnapshotSource` | 已接线的 FirstLevelMechanismBackend / FirstLevelStatusProvider |
| `FirstLevelPresentationRoot.selectionInstance` | 已接线的嵌套选择栏 |
| `FirstLevelMechanismBackend.levelManager` | 同一个 LevelManager |

组合根在未赋值时只查找一次现有 LevelManager，正式装配建议显式引用。已有镜头时关闭 Prefab 自带镜头，将两个组合组件指向现有 CameraSystem。已有新版 EventSystem 时复用，不能并存旧版 StandaloneInputModule。

放置和拆除通过 `PlacementSystem` 和正式后端公开接口；UI 不创建、回收或修改正式机关。放置时指针在地图配置的像素半径内吸附到 Anchor 精确位置，普通格子使用 Grid 中心。网格和 Anchor 编号在放置/拆除中显示，蓝色为可放置，拆除时红色高亮可回收机关；提示文本区分具体原因。已占用 Anchor 的安全替换接口未接入时保留旧机关并拒绝提交。

HUD 读取选中 Anchor 的真实路线/点位关联；有上游 RouteHint 时优先展示上游值。终点人数、等待窗口、库存、完成记录与存档没有来源时显示 `--` 或未接入。新首关集成场景必须引用一致的 Map 定义，并由所属系统提供安全替换和终点/窗口状态源。

## 重建与验证

- `SlimeBattle > UI > Rebuild First Level Prefabs`：重建 HUD、选择栏、Player、背景和新主菜单 Prefab。
- `SlimeBattle > UI > Apply Reference Main Menu`：显式接入 MainMenu 场景。
- `SlimeBattle > UI > Open Reference UI Preview`：打开独立 UI 试玩场景并进入 Play。
- `SlimeBattle > UI > Capture First Level Layouts`：主界面、放置、暂停、成功和失败渲染图输出到 `UserSettings/CodexVisualPolish/`。
- `SlimeBattle > UI > Capture Reference UI Layouts`：辅助页面、主菜单和关卡选择渲染图输出到 `UserSettings/ReferenceUiPreview/`。

独立场景为 `Assets/Game/Tests/PlayerPresentation/Test_FirstLevelUI.unity`。该夹具的数量、机关和结果标记 TEST_ONLY；测试通过不代表正式关卡胜负、库存、存档、Android 触摸或设备安全区已完成。

本轮证据见 `Assets/Game/Tests/PlayerPresentation/ReferenceUI-validation.json`。测试覆盖旧放置与数据边界、Prefab 引用、中文字体、页面暂停恢复、地图点击阻挡、结果清理和关卡选择限制。布局检查包括 1280×720、1024×768、1920×1080、1920×820。
