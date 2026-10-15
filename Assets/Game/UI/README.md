# 首关 UI Prefab 与组件接入

本交付对应 2026-10-15 小宸 Player/UI 分工，视觉结构参考《游戏设计文档.md》第 3 节和《SlimeBettle设计文档-3.docx》的主游戏、暂停草图。沿用左上流程入口、右上史莱姆数量、右侧机关栏，加入首关任务卡和放置提示。纸色面板、绿色选中态与网格反馈可直接替换美术；没有增加背包、拆除、图鉴或其他未确认玩法。

## 交付资产

| 资产 | 用途 |
| --- | --- |
| `Prefabs/PF_FirstLevelHUD.prefab` | 数量、目标、终点统计、等待窗口、路线提示、暂停与结果面板 |
| `Prefabs/PF_FirstLevelPlacementSelection.prefab` | 食物、熔炉、障碍选择与取消；显示后端配额与当前放置反馈 |
| `Prefabs/PF_FirstLevelPlayer.prefab` | Input、Camera、Grid、GridView、放置输入适配、正式机关适配器和上述两个嵌套 UI Prefab |
| `Fonts/NotoSansCJKsc-Regular.otf` | 完整的开源中文字体，附 `OFL.txt`；不依赖开发者电脑字体 |

默认 Canvas 以 1280×720 为参考，使用 Expand 保持横屏布局完整；交互内容放入 `UiSafeArea`，模态遮罩覆盖整个屏幕。按钮不绑定方向键导航，避免游戏输入使焦点误跳。所有装饰文字与图标关闭 raycast，面板和按钮拦截指针，模态 Canvas 排序 40，高于 HUD 20 和选择栏 21。结果原因支持纵向滚动。

## 集成者装配

将 `PF_FirstLevelPlayer` 拖入自己的 `FirstLevelComposition_TEST`，保留项目中现有的 Bootstrap / LevelManager。默认层级如下：

```text
FirstLevelComposition_TEST
  MapRoot                      # 地图负责人交付
  MechanismsRoot               # 机关负责人交付
  PF_FirstLevelPlayer
    GameplayCamera
    PlacementGrid
    PF_FirstLevelHUD
      SafeArea / HUD
      PausePanel
      ResultPanel
    PF_FirstLevelPlacementSelection
      SafeArea / PlacementPanel
```

根对象已有 `InputSystem`、`UISystem`、`PlayerPresentationRoot`、`FirstLevelPresentationRoot`、`PlacementGridView`、`PlacementInputAdapter`、`FirstLevelMechanismBackend`、`FirstLevelStatusProvider`。不需要再次 AddComponent，也不会再生成第二套选择栏或默认 HUD。

| 组件与 Inspector 字段 | 拖拽对象 / 配置 |
| --- | --- |
| `PlayerPresentationRoot.levelManager` | 当前 Bootstrap 的 LevelManager |
| `PlayerPresentationRoot.cameraSystem` | GameplayCamera 上的 CameraSystem（预接线） |
| `PlayerPresentationRoot.hudView` | 嵌套 PF_FirstLevelHUD（预接线） |
| `PlayerPresentationRoot.createDefaultHud` | false（已设），使用提交的 Prefab |
| `FirstLevelPresentationRoot.levelManager` | 与 PlayerPresentationRoot 相同的 LevelManager |
| `FirstLevelPresentationRoot.cameraSystem` | 同一个 CameraSystem（预接线） |
| `FirstLevelPresentationRoot.grid` | PlacementGridAuthoring（预接线） |
| `FirstLevelPresentationRoot.confirmationMode` | Single（当前点击地图即放下）；Continuous 仍可显式选择 |
| `FirstLevelPresentationRoot.placementBackend` | 根对象 FirstLevelMechanismBackend（预接线） |
| `FirstLevelPresentationRoot.statusSnapshotSource` | 根对象 FirstLevelStatusProvider（预接线） |
| `FirstLevelPresentationRoot.selectionInstance` | 嵌套选择栏（预接线）；与 selectionPrefab 二选一 |
| `FirstLevelMechanismBackend.levelManager` | 同一个 LevelManager；只能拖组件，不拖 plain C# 系统 |
| `FirstLevelStatusProvider.showLegacyWeapon` | 默认 false；兼容旧护送链路时可开启 |
| `PlacementGrid.Grid` | 原点 (0,0,0)、XY、1×1、无间隙为交付样例，集成者按地图配置 |

未显式拖入 LevelManager 时，组合根在 Start 查找一次现有实例；正式装配建议明确引用同一个 Manager。已有镜头时，可关闭 Prefab 的 GameplayCamera 并把两个组合组件改指向现有 CameraSystem，避免双镜头渲染。已有新版 EventSystem 时复用它；没有时 PlayerPresentationRoot 创建 InputSystemUIInputModule。不要并存旧版 StandaloneInputModule。

后端只调用当前 Context 的公开 `MechanismSystem` 接口。需要机关负责人配置三种玩家机关 Config/Prefab；缺失时不能创建。重开后跟随新 Context，不继续使用旧机关实例。UI 不直接 Instantiate / Destroy 正式机关，不写地图占用，不修改 Actor 或关卡结果。

## 显示数据边界

`UiState` 和 `FirstLevelStatusSnapshot` 尾部新增可选 `goalText / waitingText / routeHint`，默认空字符串，原构造调用兼容。这些是 UI 内的显示输入，不是新的 Core/Map 数据协议。`UISystem` 转发显示字段，LevelId/LevelState 仍取自 LevelManager；结果面板只按权威阶段显示。

- 存活数与上限取 SlimeManager 公开快照和 MaxAliveCount。未绑定时显示 `--`；不是用 0 代替未知，也不会把实际 30 显示为 20。配置上限不为 20 时显示核对提示。
- 目标文案采用最新任务的“至少 6 只进入终点洞穴”。终点统计与等待窗口没有公开事实来源时分别显示 `-- / 6 · 未接入`、`-- · 未接入`。
- 当前 FirstLevelStatusProvider 不推算终点人数、倒计时、Anchor 类型或路线事实。负责人接口冻结后，通过实现现有 `IFirstLevelStatusSource` 的 MonoBehaviour 提供显示字符串，再拖入 `statusSnapshotSource`。
- 当前 Map 没有冻结的 Snap Anchor 查询/替换契约，因此只保留路线/Anchor 提示位置，未伪造 L/SL Anchor，也未新增内部回收/替换协议。普通网格沿用当前 PlacementSystem。
- 护送武器默认隐藏。开启兼容显示时只展示真实公开武器快照；不根据生命或文字判定最新首关胜负。
- 放置选择不消耗库存。数量/配额只读取后端；拒绝原因保留后端原文，方便联调。已知普通格提示显示中文，选中机关有绿色反馈。

## 验证入口

独立测试场景：`Assets/Game/Tests/PlayerPresentation/Test_FirstLevelUI.unity`。双击进入 Play 即可操作三种机关、网格预览、取消、暂停/恢复和 TEST_ONLY 结果按钮。测试机关为彩色方块；HUD 的 12/20、终点人数和等待时间均是明确标注的模拟值。测试重开清空本夹具对象，不宣称真实 Core 重载或自动胜负链路通过。

- `SlimeBattle > UI > Rebuild First Level Prefabs`：显式重建交付资产；不会启动时自动覆写美术。
- `SlimeBattle > UI > Create First Level UI Test Scene`：仅生成上述独立场景。
- `SlimeBattle > UI > Capture First Level Layouts`：在临时 Preview Scene 中渲染三个横屏尺寸和暂停/成功/失败布局，输出 `Temp/CodexFirstLevelUI/`；不保存当前打开的场景。
- Test Runner / EditMode：`FirstLevelUiEditModeTests` 与既有 `PlacementEditModeTests`。
- Test Runner / PlayMode：`FirstLevelUiPlayModeTests`，覆盖真实 Prefab 的按钮监听、重绑定、选中、取消、暂停/恢复、模拟结果和测试重开清理。

本轮执行结果见 `Assets/Game/Tests/PlayerPresentation/FirstLevelUI-validation.json`。正式终点规则、等待窗口、Anchor/替换、真实关卡重载、Android 真机安全区与触摸设备仍由联调验证；本次没有修改正式场景、Core、World、Mechanism、Actors、Status 或 Build Settings。

## 2026-10-15 验证记录

| 检查 | 结果与范围 |
| --- | --- |
| Unity 2022.3.62f2c1 编译 | 通过；测试运行前导入最新脚本与资产，无 C# 编译错误 |
| EditMode | 32/32：既有放置 24 项 + UI 8 项，包含 Inspector 接线、中文字体、未知值、结果显示、长原因滚动结构与组合根重复初始化 |
| PlayMode | 4/4：真实 UI Prefab、三种选择、取消、监听重绑定、暂停/恢复、模拟结果、测试重开清理、GraphicRaycaster 阻挡及空白地图透传 |
| 布局目视检查 | 1280×720、1024×768、1920×1080；另检查暂停、成功、失败面板；已修复图标缺少 CanvasRenderer |
| 静态范围 | git diff 检查通过；正式场景、共享业务系统与 Build Settings 无变更 |

PlayMode 的按钮操作通过真实 Button 事件执行，指针阻挡通过 EventSystem 的实际 raycast 查询验证；没有声称本轮做过物理鼠标/手机触屏操作。预览图 `Assets/Game/Tests/PlayerPresentation/FirstLevelUI.png` 来自实际 Prefab 的 Unity 渲染，使用测试数量和模拟放置后端，地图仅展示网格。
