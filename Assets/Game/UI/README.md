# 首关彩色 UI 与参考图版式

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

打开背包、图鉴或设置时，通过既有 `UISystem` 命令请求暂停；关闭从运行状态打开的页面会恢复游戏。原本已暂停时，关闭页面保持暂停。Esc 优先关闭辅助页。结果、重开或重新运行会清理旧页面。

2026-10-19：`FirstLevel` 已接通拆除。运行时点击右侧“拆除”，指向玩家放置机关所在格时显示红色预览，点击地图即可回收食物、熔炉或障碍；可连续拆除。点击“取消拆除”、再次点击“拆除”、Esc 或选择放置物退出。暂停、结算与重开清除拆除模式。空格、预置场景机关和已失效对象不可拆除；UI 区域点击不会传到地图。回收通过 `MechanismSystem.RequestRemove` 清理机关和阻挡，并沿用配置冷却，数量显示同步刷新；未添加库存返还规则。

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
