# 21 InputSystem 输入系统设计

系统编号：`PLAYER-01`  
系统名称：`InputSystem`  
默认命名空间：`SlimeBattle.Player.Input`  
建议实现目录：`Assets/Game/Scripts/Player/`  
生命周期：应用级输入设备存在，关卡级语义输入由当前上下文接收  
状态：核心架构，Action Map 待确认

## 1. 职责

- 使用新版 Unity Input System Package 读取触摸、鼠标、键盘或手柄输入；
- 将设备输入转换为与设备无关的语义输入；
- 提供输入快照和输入事件；
- 管理输入启用、禁用和 UI/游戏模式切换；
- 不让下游玩法系统依赖具体设备回调。

## 2. 不负责的内容

- 不直接移动角色；
- 不直接创建或销毁机关；
- 不直接修改 UI 数值和游戏状态；
- 不在输入层决定放置是否合法；
- 不在输入层实现守卫或史莱姆的行为规则。

## 3. 核心数据

```text
InputMode
PlayerInputSnapshot
PointerInput
WorldInput
PlayerCommand
```

`PlayerInputSnapshot` 只描述当前输入事实，例如指针位置、按下、拖动、释放和取消；世界坐标转换由 `CameraSystem` 完成。

## 4. 必须接口

```csharp
void SetInputMode(InputMode mode);
InputMode GetInputMode();
PlayerInputSnapshot ReadSnapshot();
SubscriptionToken Subscribe<TInputEvent>(Action<TInputEvent> handler);
void Unsubscribe(SubscriptionToken token);
void Enable();
void Disable();
```

## 5. 对外事件

```text
PointerPressedEvent
PointerMovedEvent
PointerReleasedEvent
CancelInputEvent
PlayerCommandSubmittedEvent
```

事件只表达输入事实或语义指令，具体是放置、镜头拖动还是守卫控制由下游系统决定。

## 6. 依赖

- Unity Input System Package；
- `LevelContext` 的输入生命周期；
- `CameraSystem` 提供屏幕/世界坐标转换；
- `PlacementSystem`、`GuardManager` 或 UI 消费语义输入。

## 7. 关键不变量

1. 输入系统禁用时不产生新的游戏指令。
2. 输入模式切换不能遗留上一模式的按下或拖动状态。
3. 编辑器鼠标和移动端触摸可以映射到相同的语义接口。

## 8. 待确认项

- Action Map 名称和 Binding 命名；
- 是否仅支持触摸和鼠标；
- 多指触控是否进入首个版本；
- UI 输入与游戏输入的屏蔽规则。

