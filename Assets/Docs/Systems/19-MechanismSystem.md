# 19 MechanismSystem 环境元素与机关系统设计

系统编号：`MECH-01`  
系统名称：`MechanismSystem`  
默认命名空间：`SlimeBattle.Mechanisms`  
建议实现目录：`Assets/Game/Scripts/Mechanism/`  
生命周期：当前关卡上下文生命周期  
状态：首关机关范围和护送武器归属已确认；运行时实现、武器伤害入口和配置仍待实现/确认

## 1. 职责

- 管理场景中的静态环境元素和玩家放置的运行时环境元素；
- 完全拥有护送武器的生命、位置、运输载体、固定路线进度和抵达状态；
- 读取环境元素配置，保存实例 ID、位置、范围、剩余次数、冷却和当前状态；
- 为 Actors 提供食物、温暖等机制产生的引导源快照；
- 接收 `MovementSystem`、`MapManager` 和场景适配器提交的接触、经过、区域进入和拆除事实；
- 判断环境元素是否满足触发条件、是否处于冷却或已经耗尽；
- 将已经确认的效果请求提交给 `StatusManager`、`MovementSystem` 或其他约定的公开接口；
- 发布环境元素状态变化和已经完成的触发结果。

护送武器属于 `MechanismKind.EscortWeapon`，不是 Actor，也不由 Core 结果规则组件保存。首关由史莱姆运输、守卫保护，武器沿地图配置的固定路线点推进；动态寻路不在本轮实现。

## 2. 不负责的内容

- 不负责玩家输入、放置预算和放置按钮表现；这些属于 `InputSystem`、`PlacementSystem` 和 UI；
- 不直接修改史莱姆、敌人、守卫或其他 Actor 的生命、位置、状态或私有字段；
- 不负责地图拓扑、寻路、窄路通行和碰撞结算；这些属于 `MapManager`、`MovementSystem` 和后续的 `PassageSystem`；
- 不决定史莱姆最终选择哪一个引导源；系统只提供可用引导源和事实，目标选择属于 Actor 决策层；
- 不从动画播放结果推断机关是否成功；
- 不让机关预制体直接调用任意角色内部方法。

## 3. 核心数据

```text
MechanismId
    MechanismKind                 // Food, Furnace, Obstacle, EscortWeapon
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
    WeaponRuntimeData
    WeaponSnapshot
    EscortRouteSnapshot
PlacementCooldown
```

当前首关核心 `MechanismKind` 为 `Food`、`Furnace`、`Obstacle` 和 `EscortWeapon`。宠物、掉落物、背包、独立玩法事件、道具系统以及陷阱/魔法不进入本轮闭环；后续若重新启用，必须重新确认归属和接口。`LevelEventBus` 及武器/史莱姆结果事实事件不属于被放弃的玩法内容，仍用于跨系统通信。

## 4. 必须接口

```csharp
bool NotifyContact(MechanismTriggerContext context);
bool RequestActivate(MechanismId mechanismId, MechanismActivationRequest request);
bool TryGetSnapshot(MechanismId mechanismId, out MechanismSnapshot snapshot);
IReadOnlyList<MechanismSnapshot> GetActiveSnapshots();
bool TryGetWeaponSnapshot(WeaponId weaponId, out WeaponSnapshot snapshot);
```

`PlacementSystem` 需要向本系统提交已经通过玩家输入、数量上限和冷却校验的放置/拆除结果；具体的放置请求和拆除请求类型必须在实现前与全局 API 表统一，不能由 UI 直接操作本系统内部对象。

`TryGetWeaponSnapshot` 只读返回武器状态。武器受伤、销毁和到达终点的请求入口及其最终参数仍需在代码迁移前登记到 99 API 表，不能继续由 `ObjectiveSystem` 私有字段承载。

## 5. 对外事件

```text
MechanismTriggeredEvent
MechanismStateChangedEvent
MechanismExhaustedEvent
WeaponDamagedEvent
WeaponDestroyedEvent
WeaponReachedGoalEvent
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
| 熔炉 | 首关只作为史莱姆引导源和视觉机关，不产生寒冷/炙热状态 | 吸引范围和重装冷却保留为配置待确认，不在本轮写死 |
| 障碍 | 阻挡史莱姆、魔物和守卫，改变空间事实；拆除后消失且不直接复用 | 同时存在受放置上限约束；固定路线被阻挡后的处理待确认 |
| 护送武器 | 由史莱姆运输、守卫保护，沿地图配置的固定路线点移动；到达指定终点发布成功事实 | 生命、运输人数、载体减速和路线参数待代码/策划联调 |

障碍数量当前按周目暂定为：一周目 8 个、二周目 5 个、三周目 3 个。这里的“周目总可用数量”和“同时存在上限”是两个不同配置字段，不能混为一个计数。

组合放置可以形成路径、阵型和连锁反应，但组合规则必须通过明确的触发事实和结果接口实现，不能由表现层自行推断。首关网格放置只验证边界和单格占用，不提前实现复杂组合规则。

首关熔炉只提供 `GuideSourceSnapshot`，不得调用 `StatusManager` 添加炙热/寒冷状态。未来恢复温度玩法时再补充状态效果契约。

### 5.3 护送武器固定路线

`MapManager` 提供场景配置的只读路线点；`MechanismSystem` 保存武器运输进度，并通过待确认的机制运输移动适配向 `MovementSystem` 请求位置变化，`MovementSystem` 执行实际移动和碰撞事实。不得直接把 Actor 专用的移动 API 套用到非 Actor 武器上。首关不实现网格寻路、动态绕行或自动重规划。

路线上的障碍占用以及被阻挡后的处理尚未确认。第一张测试草图在该问题确认前应避免把默认障碍放在运输路线的关键节点上；各实现不得自行把“阻挡即失败”“忽略障碍”或“自动绕行”写成公共规则。

## 5.4 场景引导源与其他环境元素

温暖地区、低温地区、掉落物、固定事件和随机事件可以为史莱姆提供引导或触发条件。它们的边界如下：

- `MapManager` 负责地形、道路、区域归属、可行走空间和地图查询；区域进入/离开是空间事实，由 MapManager 发布；
- `MechanismSystem` 负责可交互环境元素及其引导源快照；
- `StatusManager` 负责寒冷、炙热、燃烧、冻结等状态和持续效果；
- `MovementSystem` 负责接触、阻挡、位置结果和移动请求；
- MapManager 中的 `DropSpawn`、`Event`、`Trap` 和 `Hindrance` 地图点只表示场景设计位置，不创建动态对象、不做概率判定，也不结算事件效果；
- 掉落物、固定/随机事件、陷阱和魔法的运行时系统归属及具体列表仍待确认。

Actor 进入或离开低温/温暖区域时，StatusManager 消费 MapManager 的区域事实并按配置申请或解除对应状态。温暖地区的吸引源快照由 MechanismSystem/Actor 感知层提供；区域归属本身不等同于吸引范围。

## 6. 依赖

- `ConfigSystem`；
- `MapManager`；
- `MovementSystem` 提供的接触事实；
- `StatusManager` 处理效果；
- `PassageSystem` 处理窄路相关规则。
- `PlacementSystem` 提交玩家确认的环境元素放置结果。
- `SlimeManager`/`SlimeGroupActor` 查询引导源和元素影响，不直接修改机关状态。
- Core 结果规则组件观察 `WeaponDestroyedEvent`、`WeaponReachedGoalEvent` 和 `SlimeDepletedEvent`，不修改机关数据。

## 7. 关键不变量

1. 触发判定只在机关系统完成一次。
2. 冷却、使用次数、存在状态和可用性只能由机关系统修改。
3. 机关效果通过统一效果请求提交，不能绕过 `StatusManager` 或 `MovementSystem`；关卡结果通过事件交给 Core 结果规则组件。
4. 障碍是否阻挡某个 Actor 由地图/移动系统依据机关快照判定，机关系统不直接移动 Actor。
5. 旧关卡的机关请求不能作用于重开的新 `LevelContext`。

## 8. 待确认项

- 放置请求、拆除请求和武器受击请求的最终公开接口，以及与 `PlacementSystem`/Core 的 API 表登记；
- 食物进食、熔炉接触和暴躁解除的最终效果映射；
- 食物、障碍和熔炉的精确范围、持续时间、重复使用和销毁条件；首关熔炉不得恢复温度效果；
- 障碍“同时存在上限”和“周目总可用数量”的最终配置字段；
- 放置物与地形、群体、敌人和守卫的组合互动；
- 随机事件、固定事件、陷阱、魔法、掉落物和不可放置场景机关的完整列表；
- 机关与 `PassageSystem` 的窄路职责边界；
- 具体表现预制体、动画和音效不应反向成为规则依赖。
- 路线被障碍占用时是等待、失败、禁止该次放置还是其他处理；
- 武器是否只有一个、运输所需史莱姆数量和载体减速的最终参数。

