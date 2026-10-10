# 24 UISystem UI 系统设计

系统编号：`UI-01`  
系统名称：`UISystem`  
默认命名空间：`SlimeBattle.UI`  
建议实现目录：`Assets/Game/Scripts/UI/`  
生命周期：应用级界面可跨关卡存在，关卡界面随 `LevelContext` 创建和销毁  
状态：核心架构，界面内容待确认

P0 实现更新：2026-10-09；负责人：小宸。首关接入范围增加三种机关选择、放置模式状态和结果显示；不实现背包、掉落物、独立玩法事件或通用道具界面，但可以消费当前关卡结果事实事件更新显示。

## 1. 职责

- 显示关卡状态、护送武器状态、史莱姆数量、三种机关选择、放置提示、暂停和结算；
- 主动读取初始快照，并订阅后续状态事件；
- 将按钮、触摸和菜单操作转换为流程或玩家请求；
- 管理 UI 面板开关和界面状态；
- 在核心系统状态发生变化后刷新视图。

## 2. 不负责的内容

- 不拥有史莱姆、敌人和机关运行数据；
- 不直接修改生命、资源、关卡阶段和胜负；
- 不把动画播放作为玩法完成条件；
- 不依赖事件历史来获得唯一的初始状态。

## 3. 核心数据

```text
UiState
UiPanelId
UiCommand
LevelUiSnapshot
ActorUiSnapshot
LevelResultUiSnapshot
WeaponUiSnapshot
```

UI 允许持有用于显示的缓存，但缓存必须能通过重新查询所属系统恢复。

## 4. 必须接口

```csharp
void OpenPanel(UiPanelId panelId);
void ClosePanel(UiPanelId panelId);
void Refresh(UiState state);
UiState GetState();
void SubmitCommand(UiCommand command);
```

对于核心系统而言，UI 主要是事件消费者；除流程按钮和玩家操作外，不要求玩法系统主动调用 UI 方法。

## 5. 对外事件

```text
UiPanelOpenedEvent
UiPanelClosedEvent
UiCommandSubmittedEvent
```

## 6. 依赖

- `LevelManager` 的关卡状态；
- 史莱姆、敌人、机关、放置和 Core 结果规则组件的只读快照；
- `InputSystem`；
- `PresentationSystem` 的表现通知。

## 7. 关键不变量

1. UI 缺失、隐藏或刷新失败不能阻止玩法规则运行。
2. UI 打开时必须先读取一次当前状态，再订阅变化。
3. UI 命令必须经过对应系统的公开接口和校验。

## 8. 待确认项

- 首个可玩版本需要哪些面板；
- UI 是独立场景、场景对象还是运行时创建；
- 屏幕适配、字体和交互区域；
- 是否需要完整的关卡选择和 Meta 界面。

## 9. P0 实现与接入契约

实现：`Assets/Game/Scripts/UI/UISystem.cs`；uGUI 视图：`BattleHudView.cs`；快照与事件：`UiState.cs`。沿用第 4 节接口，SubmitCommand 返回 `void`，接受/拒绝通过 `UiCommandSubmittedEvent.Accepted` 通知。

```csharp
void Initialize(LevelManager manager, InputSystem input,
    IUiSnapshotSource source = null, BattleHudView hudView = null);
// InputSystem 的完整名称为 SlimeBattle.Player.Input.InputSystem。
```

- Initialize 必须传当前应用的 LevelManager；缺失时抛 ArgumentNullException。输入、快照源、视图允许缺失，不阻塞关卡模拟。重复 Initialize 会解除原绑定。
- 启用时先查询当前关卡状态及来源快照，再订阅 LevelManager 的实际 C# 事件 `LevelStateChanged`、`LevelStarted`、`LevelLoadFailed`。禁用时解除订阅与按钮监听；再次启用重新查询，支持结算后才打开 UI。
- UiState 是只读显示快照：LevelId、LevelState、ObjectiveText、SlimeCount、EnemyCount、Hint、ResultText。数量 -1 表示未接入并显示 `--`，不是 0 存活。UI 不根据数量或文本推断胜负。
- `IUiSnapshotSource.ReadSnapshot(LevelContext context)` 是提供给关卡接入层的显示适配接口，不替代 Core 结果规则组件或各领域 API。目标/Actor/机关负责人实现适配器，调用自己的公开快照 API，返回护送武器、史莱姆数量、放置状态和结果原因。UI 在初始化、阶段变化和最多每秒 5 次刷新时查询。LevelId / LevelState 始终使用 LevelManager 的权威状态，忽略来源的同名字段。
- 快照来源缺失时显示 Waiting for level result data；异常时降级并仅记录一次警告。来源不持有旧 Context，重开后从参数查询新一局。UISystem.Refresh 只修改显示缓存，不发布关卡结果。
- UiPanelId 为 Hud / Pause / Result；OpenPanel / ClosePanel 只管理展示，不能代替暂停和胜负判定。UiCommand 为 Pause / Resume / Restart，分别调用 LevelManager 的三个公开流程请求；状态不匹配和重复重开拒绝。重开完成或加载失败解除 pending。
- 只响应 `CancelInputEvent.IsUserRequested == true` 切换暂停/恢复。生命周期取消不会切换关卡状态。加载、初始化、卸载时禁用游戏输入；暂停、结算时保留 UI 操作。
- BattleHudView.Create 可创建最小 HUD，支持指定 Font；默认内置 LegacyRuntime.ttf，默认英文文案避免未导入中文字体时出现方框。Canvas 以 1280×720、宽高折中缩放作为测试值，不作为最终屏幕方向/适配规则。正式中文字体、安全区域与超长文本仍需设备验证。
- 对外面板/指令事件采用 C# event；订阅方在禁用时移除。关卡销毁后不得缓存旧 UI 或来源引用。

场景级组合入口 `SlimeBattle.Player.PlayerPresentationRoot.Initialize(LevelManager, CameraSystem, IUiSnapshotSource = null, BattleHudView = null)` 只组装三个系统并同步输入生命周期，不修改 Core 内部。可在场景引用中指定各组件；无引用时只在 Start 查找一次 Bootstrap 的 LevelManager 与子镜头。不调用全局静态服务。没有 EventSystem 时创建使用 InputSystemUIInputModule 的场景级对象；现有场景须使用新版 UI 模块，不能并存旧 StandaloneInputModule。

首关选择面板可作为独立 uGUI 子面板或注入的 BattleHudView 扩展，由自己的显示适配器管理 Food / Furnace / Obstacle 三种机关选择状态；GraphicRaycaster 会阻止其点击穿透地图。进入放置模式后显示 PlacementGridView，空闲、已占用和越界格子使用不同预览状态；UISystem 不判断格子是否合法，也不创建机关，确认请求交给 PlacementSystem。

验证与边界见 `Assets/Game/Tests/PlayerPresentation/README.md` 和 `Assets/Docs/P0-小宸接入说明.md`。

