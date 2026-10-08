# 24 UISystem UI 系统设计

系统编号：`UI-01`  
系统名称：`UISystem`  
默认命名空间：`SlimeBattle.UI`  
建议实现目录：`Assets/Game/Scripts/UI/`  
生命周期：应用级界面可跨关卡存在，关卡界面随 `LevelContext` 创建和销毁  
状态：核心架构，界面内容待确认

## 1. 职责

- 显示关卡状态、目标、护送武器状态、史莱姆数量、资源、提示、暂停和结算；
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
ObjectiveUiSnapshot
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
- 史莱姆、敌人、机关、放置和 `ObjectiveSystem` 的只读快照；
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

