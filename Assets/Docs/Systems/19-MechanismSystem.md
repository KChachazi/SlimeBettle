# 19 MechanismSystem 环境元素与机关系统设计

系统编号：`MECH-01`  
系统名称：`MechanismSystem`  
默认命名空间：`SlimeBattle.Mechanisms`  
建议实现目录：`Assets/Game/Scripts/Mechanism/`  
生命周期：当前关卡上下文生命周期  
状态：核心架构，食物、熔炉、障碍和引导源边界已按当前策划确定；具体配置和特殊机关仍待确认

## 1. 职责

- 管理场景中的静态环境元素和玩家放置的运行时环境元素；
- 读取环境元素配置，保存实例 ID、位置、范围、剩余次数、冷却和当前状态；
- 为 Actors 提供食物、温暖等机制产生的引导源快照；
- 接收 `MovementSystem`、`MapManager` 和场景适配器提交的接触、经过、区域进入和拆除事实；
- 判断环境元素是否满足触发条件、是否处于冷却或已经耗尽；
- 将已经确认的效果请求提交给 `StatusManager`、`MovementSystem`、`ObjectiveSystem` 或其他约定的公开接口；
- 发布环境元素状态变化和已经完成的触发结果。

## 2. 不负责的内容

- 不负责玩家输入、放置预算和放置按钮表现；这些属于 `InputSystem`、`PlacementSystem` 和 UI；
- 不直接修改目标生命、位置、状态或 Actor 私有字段；
- 不负责地图拓扑、寻路、窄路通行和碰撞结算；这些属于 `MapManager`、`MovementSystem` 和后续的 `PassageSystem`；
- 不决定史莱姆最终选择哪一个引导源；系统只提供可用引导源和事实，目标选择属于 Actor 决策层；
- 不从动画播放结果推断机关是否成功；
- 不让机关预制体直接调用任意角色内部方法。

## 3. 核心数据

```text
MechanismId
MechanismKind
MechanismConfig
MechanismRuntimeData
MechanismTriggerContext
MechanismSnapshot
MechanismActivationResult
GuideSourceType
GuideSourceSnapshot
FoodRuntimeData
ObstacleRuntimeData
FurnaceRuntimeData
PlacementCooldown
```

当前核心 `MechanismKind` 为 `Food`、`Furnace` 和 `Obstacle`。陷阱、魔法、掉落物、随机事件和固定事件可以沿用本系统的运行时边界，但在规则确定前不得假设为正式实现。

## 4. 必须接口

```csharp
bool NotifyContact(MechanismTriggerContext context);
bool RequestActivate(MechanismId mechanismId, MechanismActivationRequest request);
bool TryGetSnapshot(MechanismId mechanismId, out MechanismSnapshot snapshot);
IReadOnlyList<MechanismSnapshot> GetActiveSnapshots();
```

`PlacementSystem` 需要向本系统提交已经通过玩家输入、数量上限和冷却校验的放置/拆除结果；具体的放置请求和拆除请求类型必须在实现前与全局 API 表统一，不能由 UI 直接操作本系统内部对象。

## 5. 对外事件

```text
MechanismTriggeredEvent
MechanismStateChangedEvent
MechanismExhaustedEvent
```

`MechanismTriggeredEvent` 表示机关规则已经通过并产生结果，不表示仅仅发生了碰撞回调。`MechanismStateChangedEvent` 可以表达放置、拆除、消耗、冷却和可用性变化；事件只传递快照或结果，不暴露可写运行时对象。

## 5.1 共同规则

- 机关配置属于设计时数据，实例位置、剩余次数、冷却和消耗状态属于当前关卡运行时数据；
- 同时存在的放置物总上限暂定为 5，由关卡或周目配置提供，不能在代码中写死；
- 引导源快照至少需要包含类型、位置、有效范围、是否可用和所属机制 ID；
- 多个同类引导源的随机选择、状态对应引导源优先级和战斗目标优先级由 Actor 感知/决策规则处理；
- 机关触发产生的生命、状态、击退、移动或目标效果必须通过对应系统的公开接口结算；
- 关卡重开、离开和卸载时，所有运行时机关、冷却、放置数量和订阅必须随 `LevelContext` 一起清理。

## 5.2 当前环境元素方案

策划图示中的环境元素先统一归入本系统，具体预制体和表现不改变其运行时所有权：

| 元素 | 当前规则方向 | 暂定限制 |
| --- | --- | --- |
| 食物 | 吸引范围内的史莱姆；进食后消失，并通过效果请求解除饥饿；食物或其他引导源可以参与解除暴躁 | 引导范围约 300px；下一次食物放置间隔约 10 秒 |
| 熔炉 | 吸引范围内的史莱姆；核心区域停留约 20 秒后产生炙热；可以重复使用并参与解除暴躁 | 吸引范围约 500px；核心半径约 50px；拆除后约 20 秒才能重新安装 |
| 障碍 | 阻挡史莱姆、魔物和守卫，迫使移动系统重新寻找路线；拆除后消失且不直接复用 | 同时存在受放置上限约束；周目总数量由关卡配置提供 |

障碍数量当前按周目暂定为：一周目 8 个、二周目 5 个、三周目 3 个。这里的“周目总可用数量”和“同时存在上限”是两个不同配置字段，不能混为一个计数。

组合放置可以形成路径、阵型和连锁反应，但组合规则必须通过明确的触发事实和结果接口实现，不能由表现层自行推断。

## 5.3 场景引导源与其他环境元素

温暖地区、低温地区、掉落物、固定事件和随机事件可以为史莱姆提供引导或触发条件。它们的边界如下：

- `MapManager` 负责地形、区域归属、可行走空间和地图查询；
- `MechanismSystem` 负责可交互环境元素及其引导源快照；
- `StatusManager` 负责寒冷、炙热、燃烧、冻结等状态和持续效果；
- `MovementSystem` 负责接触、阻挡、位置结果和移动请求；
- 随机事件、特殊事件、陷阱和魔法的具体列表仍待确认。

## 6. 依赖

- `ConfigSystem`；
- `MapManager`；
- `MovementSystem` 提供的接触事实；
- `StatusManager` 处理效果；
- `PassageSystem` 处理窄路相关规则。
- `PlacementSystem` 提交玩家确认的环境元素放置结果。
- `SlimeManager`/`SlimeGroupActor` 查询引导源和元素影响，不直接修改机关状态。
- `ObjectiveSystem` 接收护送武器或目标相关的已结算效果。

## 7. 关键不变量

1. 触发判定只在机关系统完成一次。
2. 冷却、使用次数、存在状态和可用性只能由机关系统修改。
3. 机关效果通过统一效果请求提交，不能绕过 `StatusManager`、`MovementSystem` 或 `ObjectiveSystem`。
4. 障碍是否阻挡某个 Actor 由地图/移动系统依据机关快照判定，机关系统不直接移动 Actor。
5. 旧关卡的机关请求不能作用于重开的新 `LevelContext`。

## 8. 待确认项

- 放置请求和拆除请求的最终公开接口，以及与 `PlacementSystem` 的 API 表登记；
- 食物进食、熔炉接触和暴躁解除的最终效果映射；
- 食物、障碍和熔炉的精确范围、持续时间、重复使用和销毁条件；
- 障碍“同时存在上限”和“周目总可用数量”的最终配置字段；
- 放置物与地形、群体、敌人和守卫的组合互动；
- 随机事件、固定事件、陷阱、魔法、掉落物和不可放置场景机关的完整列表；
- 机关与 `PassageSystem` 的窄路职责边界；
- 具体表现预制体、动画和音效不应反向成为规则依赖。

