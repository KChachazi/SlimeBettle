# 21 InputSystem 输入系统设计

系统编号：`PLAYER-01`  
系统名称：`InputSystem`  
默认命名空间：`SlimeBattle.Player.Input`  
建议实现目录：`Assets/Game/Scripts/Player/`  
生命周期：应用级输入设备存在，关卡级语义输入由当前上下文接收  
状态：核心架构，Action Map 待确认

P0 实现更新：2026-10-09；负责人：小宸。本文的系统编号已由旧分工中的 17 调整为 21，按系统名称对应职责。

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

## 9. P0 实现与接入契约

实现：`Assets/Game/Scripts/Player/InputSystem.cs`，数据：`PlayerInputSnapshot.cs`。沿用第 4 节所有接口；`SetInputMode` 返回 `void`，非法枚举抛 `ArgumentOutOfRangeException`。新增数据类型属于首次落实既有概念接口，调用方接入前请审核此契约。

- `InputMode`：`Disabled`、`Gameplay`、`UiOnly`。组件默认不接收输入，由 `PlayerPresentationRoot` 根据 LevelManager 状态启用；仅 Running 接受游戏指令，暂停和结算保留取消键与独立 uGUI 输入。
- `P0Player` Action Map 在本组件首次 `Enable()` 时创建并在销毁时释放。`Point` / `Primary` 使用 `<Pointer>/position` 和 `<Pointer>/press`，统一鼠标、笔和主触点；`Move` 使用 WASD、方向键和手柄左摇杆；`Attack` 使用空格和手柄南键；`Cancel` 使用 Escape 和手柄 Start；`CameraDrag` 使用鼠标中键；`Zoom` 使用滚轮。绑定是本次测试用默认值，不代表最终移动端控制方案。
- `PlayerInputSnapshot` 包含帧号、模式、屏幕像素位置/位移、归一化 Movement、指针按下/保持/释放、AttackPressed、CancelPressed、CameraDrag 和 Zoom。每个 Update 采样一次；同帧多次读取不消费边沿，过期帧返回空快照。消费快照的组件应在 LateUpdate 读取，避免依赖不同组件 Update 的顺序。
- `PlayerCommandSubmittedEvent.Command` 包含 `Kind`、`Movement`、`ScreenPosition`。`Kind` 为 Move / PrimaryAction / Attack。非零 Move 每帧发布，归零时再发布一次；PrimaryAction 和 Attack 只在按下边沿发布。世界位置由 CameraSystem 转换；输入不选择 Actor，不执行移动、攻击或伤害，也不假定放置含义。
- 玩法调用方负责把语义指令交给自己的公开行动入口，并通过 `CancelInputEvent` 清除持续移动。`CancelInputEvent.IsUserRequested` 区分取消键（true）与模式、禁用、失焦造成的生命周期取消（false）；生命周期取消不能被当作暂停切换。
- 使用 EventSystem 即时 GraphicRaycaster 查询屏蔽 UI，避免依赖上一帧 UI 命中缓存。UI 起始手势移出 UI 后也不会变成游戏点击；镜头中键手势必须从非 UI 区开始。uGUI InputField 编辑时不提交游戏指令。
- 模式切换、失焦、组件禁用清空手势；恢复后要求移动/主操作/攻击/中键全部回到中立状态，避免按住键重开或恢复时误操作。
- `Subscribe<T>` 返回 Core 的 `SubscriptionToken`；token 只交回创建它的 InputSystem，不与 LevelEventBus 混用。消费方在禁用时 Unsubscribe，重开后重新绑定本关输入组件。

依赖补齐：添加 `com.unity.inputsystem@1.7.0`（Unity 2022.3 可用版本，现有 `activeInputHandler: 1` 已选择新版输入）。manifest 与 lock 同步；仅增加已有 uielements 依赖，不升级其他包。回退时同时恢复这两个文件并移除本次输入实现，不能只删除依赖而保留引用它的代码。依据：[Unity 官方包说明](https://docs.unity.cn/Manual/com.unity.inputsystem.html)。

验收入口、异常场景与跨系统示例见 `Assets/Game/Tests/PlayerPresentation/README.md` 和 `Assets/Docs/P0-小宸接入说明.md`。多指、虚拟摇杆、键位重映射留待后续确认。

