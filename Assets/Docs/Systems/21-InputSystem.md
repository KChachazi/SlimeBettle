# 21 InputSystem 输入系统设计

系统编号：`PLAYER-01`  
系统名称：`InputSystem`  
默认命名空间：`SlimeBattle.Player.Input`  
建议实现目录：`Assets/Game/Scripts/Player/`  
生命周期：应用级输入设备存在，关卡级语义输入由当前上下文接收  
状态：核心架构，Action Map 待确认；首关放置模式由上层适配接入

P0 实现更新：2026-10-10；负责人：小宸。本文的系统编号已由旧分工中的 17 调整为 21，按系统名称对应职责。

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
- 不在输入层决定放置是否合法；放置合法性由 `PlacementSystem` 根据 Grid、边界和占用事实判断；
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
InputSubscriptionToken Subscribe<TInputEvent>(Action<TInputEvent> handler);
void Unsubscribe(InputSubscriptionToken token);
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

事件只表达输入事实。PrimaryAction 表示玩家在屏幕某位置进行了主要操作；进入放置模式后由放置适配层把它转交给 `PlacementSystem`，输入层仍不规定放置类型、格子是否合法或机关效果。未进入放置模式时不能把 PrimaryAction 解释为直接控制 Actor。

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
- 2026-10-10 按反馈撤回“点击后自动移动”的契约。`PrimaryAction.ScreenPosition` 只表示主要操作发生的屏幕像素位置；一次按下仅发布一次，不选择 Actor，不直接表示移动、攻击或放置。首关放置适配层可在放置模式下经 CameraSystem 投影后调用 PlacementSystem；输入系统本身不创建机关或更新网格占用。
- `P0Player` Action Map 首次 Enable 时创建，销毁时释放。Point / Primary 使用 `<Pointer>/position` / `<Pointer>/press`，统一鼠标与主触点。Cancel 使用 Escape / 手柄 Start；CameraDrag 使用鼠标中键；Zoom 使用滚轮。放置模式使用同一套指针事实，不额外复制一套设备输入。多指与触屏镜头手势待后续确认。
- Move / Attack 保留为预留或调试接口，当前正式玩法不使用。WASD、方向键和手柄摇杆默认不绑定，仅首次 Enable 前显式开启 Enable Directional Debug Input 时绑定；空格/手柄南键仅发布调试 Attack，不造成效果。
- PlayerInputSnapshot 包含帧号、模式、屏幕像素位置/位移、Movement、指针边沿/保持、AttackPressed、CancelPressed、CameraDrag 和 Zoom。每个 Update 采样一次；同帧重复读取不消费边沿，过期帧为空。消费者在 LateUpdate 读取，避免依赖 Update 顺序。
- PlayerCommandSubmittedEvent.Command 包含 Kind、Movement、ScreenPosition。PrimaryAction 与调试 Attack 在按下边沿发布；启用方向调试时非零 Move 每帧发布、归零再发布一次。
- CancelInputEvent.IsUserRequested 区分用户取消（true）与模式、禁用、失焦的生命周期清理（false）。消费者清除待处理交互和测试标记；生命周期取消不切换暂停。
- 使用 EventSystem 即时 GraphicRaycaster 查询屏蔽 UI，避免依赖上一帧 UI 命中缓存。UI 起始手势移出 UI 后也不会变成游戏点击；镜头中键手势必须从非 UI 区开始。uGUI InputField 编辑时不提交游戏指令。
- 指针坐标使用 PassThrough 采样；主操作与中键手势保持发起设备的坐标来源，避免实体鼠标、虚拟鼠标或触点并存时按坐标数值大小选错设备。公开输入接口不变。
- 模式切换、失焦、组件禁用始终清空手势并发布生命周期取消，即使触点已松开；恢复后要求主操作、攻击调试、中键和已绑定方向回到中立状态，避免恢复/重开继承旧输入。
- **接口变更通知（2026-10-10）**：输入使用独立 InputEventChannel，不再使用 Core 的 LevelEventBus。Subscribe 返回 `SlimeBattle.Player.Input.InputSubscriptionToken`，Unsubscribe 接受相同类型；显式声明旧 Core.SubscriptionToken 的调用方须迁移，使用 var 的调用方无需调整。token 仅对创建它的 InputSystem 有效，跨实例、默认和重复解绑无效；Subscribe(null) 抛 ArgumentNullException，销毁后订阅抛 ObjectDisposedException。禁用/销毁时解绑，重开绑定新输入实例；Core 的 token 与事件总线不受影响。

依赖：同步最新 master 的 `com.unity.inputsystem@1.14.2`，现有 `activeInputHandler: 1` 已选择新版输入；本分支不另行降级包。`SlimeBattle.Runtime` 显式引用 `Unity.InputSystem` 与 `UnityEngine.UI`，隔离测试自有 Runtime / Editor 程序集。撤回本次实现时保留 master 的包配置，仅移除本分支新增的输入实现及不再需要的程序集引用。

验收入口、异常场景与跨系统示例见 `Assets/Game/Tests/PlayerPresentation/README.md` 和 `Assets/Docs/P0-小宸接入说明.md`。多指、虚拟摇杆、键位重映射留待后续确认。


### 首关快照补充通知（2026-10-11）

PlayerInputSnapshot 增加只读 PointerBlocked（构造函数尾部可选参数，旧调用兼容），表示 UI 起始手势或等待控件归零期间不可作为地图预览。PlacementInputAdapter 在 LateUpdate 使用该事实，避免 UI 按下后拖出时更新地图预览；不改变 PrimaryAction 语义。
