# 26 Core 关卡结果规则（原 ObjectiveSystem 文档）

系统编号：`CORE-04`  
实现类型：`LevelResultRules`
默认命名空间：`SlimeBattle.Core`
建议实现目录：`Assets/Game/Scripts/Core/`
生命周期：随当前 `LevelContext` 创建、订阅、结算和销毁
状态：已由 `LevelContext` 创建；支持 `CaveOccupancy` 与 `EscortWeapon` 两类目标。正式 `FirstLevel` / `Castle` 当前选择护送武器目标。

> 为保持已有链接，文件名暂保留 `26-ObjectiveSystem.md`。当前 `LevelContext` 不创建旧 `ObjectiveSystem`；其源码仍在仓库中，但不属于正式关卡结果运行时。

## 1. 定位

Core 关卡结果规则组件只聚合当前关卡已经发生的结果事件，并确认本局成功或失败。它不是一个拥有玩法对象的独立系统，不保存武器、史莱姆、守卫或机关的运行时副本。

结果模式由 `LevelConfig.ObjectiveKind` 选择。当前配置中 `FirstLevel` 与 `Castle` 均为 `EscortWeapon`；旧洞穴配置和未序列化目标类型的测试配置使用 `CaveOccupancy`。

- **护送武器模式：**武器抵达终点成功；武器被摧毁或史莱姆耗尽失败。若同一帧同时收到抵达与失败事实，代码先判定抵达成功。
- **洞穴人数模式：**以 `SlimeGoalOccupancyChangedEvent.AliveInsideCount` 表示当前洞穴内存活数量；第一只史莱姆进入后开启等待窗口，人数达到阈值成功；史莱姆耗尽立即失败；窗口到期仍不足阈值则失败。若同一帧达到人数阈值并收到耗尽事实，代码先判定成功。
- 每局只确认一次结果。`LevelManager` 订阅 `ResultDecided` 并推进关卡阶段，不复制条件。

洞穴模式的阈值默认为 6、等待时长默认为 10 秒。当前 `LevelContext` 只传 `LevelConfig.ObjectiveKind`，没有读取 `ObjectiveConfig` 内的结果参数。此处说明实际代码，不替代后续策划对数值的修改。

武器的生命、位置、运输载体、固定路线进度和抵达状态完全属于 `MechanismSystem`。

## 2. 职责

- 订阅当前 `LevelContext` 的 `LevelEventBus`；
- 保存所选目标模式、洞穴计数/等待计时以及最终 `LevelResult`；
- 按所选模式订阅 `SlimeGoalOccupancyChangedEvent`，或 `WeaponReachedGoalEvent` / `WeaponDestroyedEvent`，并在两种模式下订阅 `SlimeDepletedEvent`；
- 通过 `ResultDecided(LevelResult)` C# 事件通知 `LevelManager`；
- 在暂停、重开和卸载时清理订阅与本局结果状态；
- 为 `LevelManager`、UI 和测试提供只读结果查询。

## 3. 不负责的内容

- 不创建、保存或修改护送武器运行时数据；
- 不创建或统计史莱姆、敌人和守卫的运行时集合；
- 不直接计算伤害、移动、运输、碰撞、状态或机关触发；
- 不判断敌人是否攻击武器；
- 不执行场景加载、重开、下一关跳转或 UI 动画；
- 不从 UI、动画或场景对象的显示状态推断成功/失败。

## 4. 核心数据

```text
LevelObjectiveKind
LevelResult
LevelResultRules
```

`LevelResultRules` 保存目标模式、目标数量和等待计时，并公开 `HasResult`、`Result`、`GoalOccupantCount`、`RequiredCount`、`IsWaiting`、`RemainingSeconds` 与 `ResultDecided`。它不保存 `WeaponRuntimeData`、`SlimeRuntimeData` 或其他对象的第二份可写状态。

## 5. 当前接口与生命周期

```csharp
LevelResultRules(ILevelEventBus bus, LevelObjectiveKind objectiveKind,
                 int requiredCount = 6, float waitSeconds = 10f);
void EndFrame(float deltaTime);
void Dispose();
event Action<LevelResult> ResultDecided;
bool HasResult { get; }
LevelResult Result { get; }
int GoalOccupantCount { get; }
int RequiredCount { get; }
bool IsWaiting { get; }
float RemainingSeconds { get; }
```

`LevelContext.Initialize()` 创建规则组件；`LevelContext.Tick()` 在帧末调用 `EndFrame(deltaTime)`；`LevelManager` 订阅 `ResultDecided`。构造器直接订阅本局 EventBus，`Dispose()` 解除订阅。没有公开的手动提交成功/失败命令。

## 6. 对外事件

```text
ResultDecided(LevelResult) // LevelResultRules C# event
```

输入事实事件由各自所有者发布：

| 事件 | 发布者 | 含义 |
| --- | --- | --- |
| `SlimeGoalOccupancyChangedEvent` | `SlimeManager` | 洞穴内存活史莱姆数量发生变化；进入/离开和死亡均按当前占用更新 |
| `WeaponDamagedEvent` | `MechanismSystem` | 护送武器已经完成一次受击/伤害结算，供 UI/守卫等观察 |
| `WeaponDestroyedEvent` | `MechanismSystem` | 护送武器生命归零并确认摧毁；护送模式下判负 |
| `WeaponReachedGoalEvent` | `MechanismSystem` | 护送武器到达指定终点；护送模式下判胜 |
| `SlimeDepletedEvent` | `SlimeManager` | 当前关卡最后一只史莱姆已经确认死亡 |
| `ResultDecided(LevelResult)` | `LevelResultRules` | 本局成功或失败已经确认；`LevelManager` 接收并推进阶段 |

结果事件只携带只读快照或结果原因，不携带可写的运行时对象。

## 7. 依赖与信息流

```text
LevelConfig.ObjectiveKind
  └─ LevelContext 创建 LevelResultRules
       ├─ CaveOccupancy：SlimeGoalOccupancyChangedEvent + SlimeDepletedEvent
       └─ EscortWeapon：WeaponReachedGoalEvent + WeaponDestroyedEvent + SlimeDepletedEvent
             ↓ 当前 LevelEventBus
       ResultDecided(LevelResult)
             ↓
       LevelManager → UI / 结算流程
```

`GameRoot` 不持有本局 EventBus，也不参与关卡结果判定。`GuardManager` 查询 `MechanismSystem` 的武器快照来执行防守，不通过结果组件获取或修改武器。

## 8. 关键不变量

1. 结果规则组件不拥有武器、史莱姆、守卫或机关运行时数据。
2. 洞穴人数事实由 `SlimeManager` 发布；武器事实由 `MechanismSystem` 发布；结果组件只消费所选模式的事实。
3. Core 结果规则组件每局最多发布一次成功或失败结果。
4. `LevelManager` 不复制结果条件，不从 UI 或表现回调推断结果。
5. `Dispose` 后旧事件、计时器和结果不能作用于新关卡。
6. 同帧裁决顺序由当前代码确定：护送模式抵达优先于摧毁/史莱姆耗尽；洞穴模式达标优先于史莱姆耗尽，耗尽优先于等待超时。

## 9. 待确认项

- `ObjectiveConfig` 的结果参数是否应传给构造器；当前阈值/等待时长仍用默认值 6/10 秒；
- 多终点规则；`LevelContext` 当前只允许一个 Goal 区域；
- 多武器、多终点或后续关卡是否需要可配置结果规则；
- `Level_test` 的默认 `CaveOccupancy` 是否仍是期望设置；
- `ObjectiveSystem` 旧源码与 `ObjectiveConfig` 资产的清理/迁移时机；
- 配置中的 Castle 奖励 ID `SecondLevel` 与实际 Level ID `Castle` 的映射。

## 10. 验收场景

- 当前配置为 `EscortWeapon` 的关卡：武器抵达判胜、武器摧毁或史莱姆耗尽判负，且只确认一次；
- `CaveOccupancy`：当前存活人数达标判胜，首只进入后开始计时，耗尽立即判负，超时不足判负；
- 关卡重开后旧 `LevelResultRules` 订阅被释放，不保留上一局结果；
- UI、动画或场景重开回调不能重复确认结果；
- `LevelManager` 能消费最终结果并进入结算/重开流程，结果组件本身不加载场景；
- 重开后不存在上一局结果快照、事件订阅或武器运行时引用。

## 历史记录：结果规则与场景组合（2026-10-14）

以下内容记录当时的设计目标，具体接口和当前关卡结果配置已由上文更新。

集成测试场景可以通过 `[SerializeField] private` 引用 UI、测试入口和具体适配器，但不能把场景按钮、Installer 或 UI 回调作为正式结果来源。结果规则组件仍由当前 `LevelContext` 创建，消费本局 EventBus 上的事实事件，并向 `LevelManager`/UI 提供只读结果快照或最终结果事件。

使用 Mock 结果或直接调用 `LevelManager.ReportResult(...)` 只允许存在于明确标记的 `TEST_ONLY` 夹具中，用于验证暂停、结算和 UI 清理；不能作为“至少 6 只史莱姆进入终点洞穴 + 等待窗口”正式结果链路通过的证据。场景组合通过后仍需单独验证真实终点事实、重开和卸载清理。
