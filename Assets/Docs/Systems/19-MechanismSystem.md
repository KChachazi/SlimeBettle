# 19 MechanismSystem 环境元素与机关系统设计

系统编号：`MECH-01`  
系统名称：`MechanismSystem`  
默认命名空间：`SlimeBattle.Mechanisms`  
建议实现目录：`Assets/Game/Scripts/Mechanism/`  
生命周期：当前关卡上下文生命周期  
状态：食物/熔炉/障碍已有基础运行时；第一关机关与地图点的静态配置已录入；陷阱/补给触发运行时、首关装配和结果接入仍待实现/联调
逻辑归属：`Map` 领域下辖的动态机关子模块
当前负责人：小智（已确认）

## 1. 职责

`MechanismSystem` 在逻辑上属于 `Map` 领域，但不并入 `MapManager` 的内部实现。`MapManager` 提供静态地图、路线、Anchor 和空间事实；`MechanismSystem` 负责机关实例、动态阻挡注册、触发、替换、回收、运行时快照和机关事件。当前已有机关及本轮首关机关接入统一由小智负责维护，其他成员只能通过公开接口调用或适配。

- 管理场景中的静态环境元素和玩家放置的运行时环境元素；
- 完全拥有已启用护送武器的生命、位置、运输载体、路线进度和抵达状态；该链路目前属于兼容/后续扩展，不得覆盖最新第一关的史莱姆终点统计结果；
- 读取环境元素配置，保存实例 ID、位置、范围、剩余次数、冷却和当前状态；
- 为 Actors 提供食物、熔炉等机制产生的引导源快照；熔炉首关不产生冷热状态；
- 接收 `MovementSystem`、`MapManager` 和场景适配器提交的接触、经过、区域进入和拆除事实；
- 判断环境元素是否满足触发条件、是否处于冷却或已经耗尽；
- 对需要运行时交互的陷阱/补给提供明确的接入边界；在所有权未确认前不得由地图脚本或 UI 直接实例化；
- 将已经确认的效果请求提交给 `StatusManager`、`MovementSystem` 或其他约定的公开接口；
- 发布环境元素状态变化和已经完成的触发结果。

护送武器属于 `MechanismKind.EscortWeapon`，不是 Actor，也不由 Core 结果规则组件保存。若该兼容链路启用，武器沿地图配置的路线事实推进；它不再自动等同于最新第一关的胜利条件。动态通用寻路不在本轮实现。

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
    MechanismKind                 // Food, Furnace, Obstacle, EscortWeapon；Trap/Supply 归属待接口确认
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

当前第一关明确需要 `Food`、`Furnace`、`Obstacle`、路线关联陷阱和补给点；现有 `EscortWeapon` 作为兼容/后续扩展保留。宠物、通用掉落物、背包、通用道具系统不进入本轮闭环。陷阱和补给虽然进入第一关玩法范围，但其最终运行时所有者、激活接口和是否复用本系统仍需在公共接口评审中确认，不能通过临时字段或场景回调静默收敛。`LevelEventBus` 及史莱姆/机关事实事件仍用于跨系统通信。

`MechanismConfig` 保存类型、是否允许玩家放置、吸引范围、占位半径、使用次数和冷却；障碍另保存首关总库存、耐久和碰撞半径。使用次数配置为 `0` 表示不限次数；食物必须配置有限使用次数。暖区吸引半径保存在 `MapAreaAuthoring`，默认设计值为 500px。机关配置资产需纳入当前关卡的 `ConfigRegistry`，场景/预制体的 `MechanismAuthoring` 保存其 `ConfigId`。

首关离线数据位于 `Assets/Game/Configs/Maps/FirstLevel/MAP_FirstLevel.asset`、`Assets/Game/Configs/Mechanisms/PlayerPlaceable/` 和 `Assets/Game/Configs/Mechanisms/Hazards/`。`FirstLevelHazardConfig` 保存 T1-T5 与补给点的静态触发参数；它只定义数据，不创建陷阱对象、不检测接触、不结算效果。地图点的 `ConfigId` 与这些资产的 ID 对应，场景组合时仍须把需要的 ConfigAsset 加入 `LevelConfig`/`ConfigRegistry`。当前首关的食物吸引半径为 400px；熔炉没有首关半径数据，沿用项目已有 500px 配置；障碍数据为总库存 10、耐久 3、放置冷却 1 秒、碰撞半径 55px。

T4 的 0.5 倍减速、3 秒时长只在 `CFG_Trap_T4` 中作为待运行时消费的效果数据。首关沼泽地图区域当前移动倍率为 1，避免陷阱运行时接入后同一减速同时由 MapManager 地形倍率和 StatusManager 状态倍率重复施加。该区域几何仍是无底图阶段的圆形代理，不能视作最终沼泽轮廓。

## 4. 必须接口

```csharp
bool NotifyContact(MechanismTriggerContext context);
bool RequestActivate(MechanismId mechanismId, MechanismActivationRequest request);
bool TryGetSnapshot(MechanismId mechanismId, out MechanismSnapshot snapshot);
IReadOnlyList<MechanismSnapshot> GetActiveSnapshots();
bool TryGetWeaponSnapshot(WeaponId weaponId, out WeaponSnapshot snapshot);
```

`MechanismAuthoring` 为关卡场景和放置预制体提供稳定实例 ID、配置 ID 与碰撞适配。场景机关在 `LevelContext` 创建时自动注册；放置系统先询问 `CanPlace`，再通过 `RequestPlace` 注册已生成的预制体，并通过 `RequestRemove` 拆除。每周可用总数仍由 `PlacementSystem`/关卡库存配置管理，本系统负责同时存在上限、地图占位校验和拆除冷却。

### 运行时机关层级与世界表现

运行时机关实例统一挂到关卡的 `MechanismsRoot`。机关 Prefab 根保留 `MechanismAuthoring` 和碰撞体，世界表现必须放在根下的 `VisualRoot`，由 `SpriteRenderer`、动画和特效组成；UI 图形和 `PlacementGridView` 不能作为机关视觉子物体。当前共享 `Default` Sorting Layer 的约定是地图底图 `-100`、机关 `1`、Actor `0`、护送武器 `2`、放置预览 `100+`。机关根的位置保存世界 XY，`VisualRoot` 局部位置固定为零，替换占位美术时不改变这个层级和运行时接口。

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
- 同时存在的放置物总上限从 `LevelConfig.MaximumActivePlacedMechanisms` 读取，默认关卡配置为 5；
- 引导源快照至少需要包含类型、位置、有效范围、是否可用和所属机制 ID；
- 多个同类引导源的随机选择、状态对应引导源优先级和战斗目标优先级由 Actor 感知/决策规则处理；
- 机关触发产生的生命、状态、击退、移动或目标效果必须通过对应系统的公开接口结算；
- 关卡重开、离开和卸载时，所有运行时机关、冷却、放置数量和订阅必须随 `LevelContext` 一起清理。

## 5.2 当前环境元素方案

策划图示中的环境元素先统一归入本系统，具体预制体和表现不改变其运行时所有权：

| 元素 | 当前规则方向 | 暂定限制 |
| --- | --- | --- |
| 食物 | 吸引范围内的史莱姆；进食后消失，并通过效果请求解除饥饿；本关若采用敌人掉落食物，按局部供给规则接入 | 具体引导范围、使用次数、重生/掉落来源待策划和配置确认 |
| 熔炉 | 首关只作为史莱姆引导源和视觉机关，不产生寒冷/炙热状态 | 吸引范围和重装冷却保留为配置待确认，不在本轮写死 |
| 障碍 | 阻挡史莱姆、魔物和守卫，改变空间事实；普通格子重复放置拒绝；L/SL Anchor 按配置允许替换旧障碍并回收旧对象 | 障碍耐久、同时存在上限和路线阻断后的选择时机待联调 |
| 陷阱/补给 | 第一关策划范围内的路线关联运行时元素；由明确的公共接口触发并发布结果事实 | 最终所有者、激活条件和是否归入本系统待确认 |
| 护送武器 | 现有兼容链路：由史莱姆运输、守卫保护，沿地图路线事实移动；到达终点发布事实 | 不作为最新第一关成功条件；生命、运输人数、载体减速和路线参数待代码/策划联调 |

障碍数量当前按周目暂定为：一周目 8 个、二周目 5 个、三周目 3 个。这里的“周目总可用数量”和“同时存在上限”是两个不同配置字段，不能混为一个计数。

组合放置可以形成路径、阵型和连锁反应，但组合规则必须通过明确的触发事实和结果接口实现，不能由表现层自行推断。首关普通 Grid 放置验证边界和单格占用，特殊 Anchor 额外验证允许类型与替换策略；不提前实现复杂组合规则。

首关熔炉只提供 `GuideSourceSnapshot`，不得调用 `StatusManager` 添加炙热/寒冷状态。未来恢复温度玩法时再补充状态效果契约。

### 5.3 第一关三路线与兼容武器路线

第一关的三条路线由 `MapManager` 提供稳定的只读路线事实，关卡规则根据 L1/L2/L3 障碍和 SL1/SL2/SL3 关联点筛选后续可选路线。MechanismSystem 不拥有路线选择算法，也不直接修改史莱姆目标。已经进入某条路线的 Actor 不因新放置被瞬移或重写历史位置；路线选择时机和同帧优先级由关卡规则/Actor 决策接口确认。

如果兼容护送武器链路启用，`MechanismSystem` 保存武器运输进度，并通过独立的机制运输移动适配向 `MovementSystem` 请求位置变化，`MovementSystem` 执行实际移动和碰撞事实。不得直接把 Actor 专用的移动 API 套用到非 Actor 武器上。首关不实现通用网格寻路、动态绕行或自动重规划。

路线上的障碍占用、陷阱触发和被阻断后的处理必须通过已确认的公开事实接口完成。各实现不得自行把“阻挡即失败”“忽略障碍”或“自动绕行”写成公共规则。

## 5.4 场景引导源与其他环境元素

温暖地区、低温地区、掉落物、固定事件和随机事件可以为史莱姆提供引导或触发条件。它们的边界如下：

- `MapManager` 负责地形、道路、区域归属、可行走空间、三路线和 Anchor 查询；区域进入/离开是空间事实，由 MapManager 发布；MechanismSystem 从引导源快照生成食物/熔炉事实，并用 MapManager 校验放置位置、注册运行时障碍；
- `MechanismSystem` 负责可交互环境元素及其引导源快照；
- `StatusManager` 负责寒冷、炙热、燃烧、冻结等状态和持续效果；
- `MovementSystem` 负责接触、阻挡、位置结果和移动请求；
- MapManager 中的 `DropSpawn`、`Event`、`Trap` 和 `Hindrance` 地图点只表示场景设计位置，不创建动态对象、不做概率判定，也不结算事件效果；本系统不会把这些地图点自动实例化为机关；
- 通用掉落物、固定/随机事件和道具系统不进入本轮；第一关局部食物供给、陷阱和补给的运行时归属及具体接口仍待确认。

Actor 进入或离开低温/温暖区域时，StatusManager 消费 MapManager 的区域事实并按配置申请或解除对应状态。暖区的吸引源快照由 MechanismSystem 从 MapManager 的区域边界和配置范围生成；区域归属本身不等同于吸引范围。`GetGuideSourcesInRange` 将像素半径按 Actor 精灵的 `pixelsPerUnit` 转换为世界距离，供感知层选择。

## 6. 依赖

- `ConfigSystem`；
- `MapManager`（地图事实、路线候选和 Anchor 查询）；
- `MechanismAuthoring` 提供的 2D 触发接触事实；移动阻挡由 `MapManager` 向 `MovementSystem` 暴露；
- `StatusManager` 处理效果；
- `PassageSystem` 处理窄路相关规则。
- `PlacementSystem` 提交玩家确认的环境元素放置结果；普通格子重复放置由 PlacementSystem 拒绝，Anchor 替换由双方公开契约协调。
- `SlimeManager`/`SlimeGroupActor` 查询引导源和元素影响，不直接修改机关状态。
- Core 结果规则组件优先观察第一关史莱姆终点统计/等待窗口事实；兼容武器链路可观察 `WeaponDestroyedEvent`、`WeaponReachedGoalEvent`，但不自动改变第一关成功口径，也不修改机关数据。

## 7. 关键不变量

1. 触发判定只在机关系统完成一次。
2. 冷却、使用次数、存在状态和可用性只能由机关系统修改。
3. 机关效果通过统一效果请求提交，不能绕过 `StatusManager` 或 `MovementSystem`；关卡结果通过事件交给 Core 结果规则组件。
4. 障碍是否阻挡某个 Actor 由地图/移动系统依据机关快照判定，机关系统不直接移动 Actor。
5. 普通网格重复放置必须拒绝；Anchor 替换只能在 Anchor 配置允许时发生，第一关 L/SL 障碍替换必须先保证旧对象可回收/可恢复，再提交新对象。
6. Anchor、路线和陷阱是跨系统事实，不允许由机关预制体或 UI 直接改写 MapManager/Actor 私有数据。
7. 旧关卡的机关请求不能作用于重开的新 `LevelContext`。

## 8. 待确认项

- 放置请求、拆除请求、Anchor 替换和武器受击请求的最终公开接口，以及与 `PlacementSystem`/Core 的 API 表登记；
- 食物进食、熔炉接触和暴躁解除的最终效果映射；
- 食物、障碍和熔炉的精确范围、持续时间、重复使用和销毁条件；首关熔炉不得恢复温度效果；
- 障碍“同时存在上限”和“周目总可用数量”的最终配置字段；
- 第一关陷阱、补给和局部食物供给的最终运行时所有者、触发接口、资源/寿命规则；
- 放置物与地形、群体、敌人和守卫的组合互动；
- 随机事件、固定事件、陷阱、魔法、掉落物和不可放置场景机关的完整列表；
- 机关与 `PassageSystem` 的窄路职责边界；
- 具体表现预制体、动画和音效不应反向成为规则依赖。
- 路线被障碍占用时，后续路线选择、等待、失败、禁止该次放置或其他规则的最终时机；
- 兼容护送武器是否重新进入某一关、运输所需史莱姆数量和载体减速的最终参数。

## 补充：机制 Prefab 与场景组合边界（2026-10-14）

机制 Prefab、`MechanismAuthoring`、配置资产和场景预置点可以由负责人分别交付，再由集成测试场景通过 `[SerializeField] private` 引用组合。场景组合组件或 Placement 适配器不能直接 `Instantiate`、`Destroy` 或修改机制内部运行时数据；正式创建、替换、回收和生命周期仍由 `MechanismSystem` 负责。

`MechanismSystem` 在当前 `LevelContext` 中创建运行时机关并注册地图阻挡/事件。集成测试可以使用 Mock Backend 或占位 Prefab，但必须标记 `TEST_ONLY`，不能将测试对象或手动结果调用当成正式机关创建和第一关结果通过的证据。重开、失败、结算和卸载时，场景组合层只负责清理自己的表现/临时对象，机制运行时由当前 Context 统一 Dispose。

