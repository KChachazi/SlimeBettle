# 19 MechanismSystem 环境元素与机关系统设计

系统编号：`MECH-01`  
系统名称：`MechanismSystem`  
默认命名空间：`SlimeBattle.Mechanisms`  
建议实现目录：`Assets/Game/Scripts/Mechanism/`  
生命周期：当前关卡上下文生命周期  
状态：食物、熔炉、障碍、固定路线护送武器和 Anchor 安全替换已有运行时实现；Castle 餐车由 `MechanismSystem` 管理装载/沿轨道移动；宝箱、地刺和滚石仍由关卡场景适配器接线。
逻辑归属：`Map` 领域下辖的动态机关子模块
当前负责人：小智（已确认）

> 本文按 2026-10-28 代码与配置整理。后文带日期的接入记录保留历史；若其中出现“待实现/待确认”与当前代码相冲突，以本节和当前公开 API 为准。静态代码检查不代表 Play Mode 验收。

## 1. 职责

`MechanismSystem` 在逻辑上属于 `Map` 领域，但不并入 `MapManager` 的内部实现。`MapManager` 提供静态地图、路线、Anchor 和空间事实；`MechanismSystem` 负责机关实例、动态阻挡注册、触发、替换、回收、运行时快照和机关事件。当前已有机关及本轮首关机关接入统一由小智负责维护，其他成员只能通过公开接口调用或适配。

- 管理场景中的静态环境元素和玩家放置的运行时环境元素；
- 完全拥有已启用护送武器的生命、位置、运输载体、路线进度和抵达状态；当前 `FirstLevel` 与 `Castle` 的配置均选择护送武器目标；
- 读取环境元素配置，保存实例 ID、位置、范围、剩余次数、冷却和当前状态；
- 为 Actors 提供食物、熔炉等机制产生的引导源快照；熔炉首关不产生冷热状态；
- 接收 `MovementSystem`、`MapManager` 和场景适配器提交的接触、经过、区域进入和拆除事实；
- 判断环境元素是否满足触发条件、是否处于冷却或已经耗尽；
- 通过明确的场景适配器接收宝箱、地刺和滚石的运行时交互；这些适配器当前不消费 `FirstLevelHazardConfig`；
- 将已经确认的效果请求提交给 `StatusManager`、`MovementSystem` 或其他约定的公开接口；
- 发布环境元素状态变化和已经完成的触发结果。

护送武器属于 `MechanismKind.EscortWeapon`，不是 Actor，也不由 Core 结果规则组件保存。`FirstLevel` 与 `Castle` 当前都启用该目标：武器抵达终点即成功；武器被摧毁或史莱姆耗尽则失败。武器沿地图配置的固定路线分段推进，不使用 Actor 的通用 A* 路径。

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

当前第一关使用 `Food`、`Furnace`、`Obstacle` 和 `EscortWeapon`。地刺、宝箱、滚石由 `FirstLevelCompositionRoot` 及其场景组件接线，尚未并入通用机关配置/实例模型。宠物、通用掉落物、背包和通用道具系统不属于当前实现。

`MechanismConfig` 保存类型、是否允许玩家放置、吸引范围、占位半径、使用次数和冷却；障碍另保存首关总库存、耐久和碰撞半径。使用次数配置为 `0` 表示不限次数；食物必须配置有限使用次数。暖区吸引半径保存在 `MapAreaAuthoring`，默认设计值为 500px。机关配置资产需纳入当前关卡的 `ConfigRegistry`，场景/预制体的 `MechanismAuthoring` 保存其 `ConfigId`。

首关配置位于 `Assets/Game/Configs/Maps/FirstLevel/MAP_FirstLevel.asset` 和 `Assets/Game/Configs/Mechanisms/PlayerPlaceable/`。`FirstLevelHazardConfig` 的 T1–T5 与补给资产目前没有进入 Bootstrap 的配置注册列表，运行时适配器也没有读取这些值。地刺伤害当前由 `TrapDamageAuthoring` 按每秒最大生命值 5% 结算；不能把该数值描述成由 `CFG_Trap_*` 驱动。玩家放置的库存由战役资源配置管理，机关同时在场上限由 `LevelConfig` 管理。

T4 的 0.5 倍减速、3 秒时长只在 `CFG_Trap_T4` 中作为待运行时消费的效果数据。首关沼泽地图区域当前移动倍率为 1，避免陷阱运行时接入后同一减速同时由 MapManager 地形倍率和 StatusManager 状态倍率重复施加。该区域几何仍是无底图阶段的圆形代理，不能视作最终沼泽轮廓。

## 4. 必须接口

```csharp
bool NotifyContact(MechanismTriggerContext context);
bool RequestActivate(MechanismId mechanismId, MechanismActivationRequest request);
bool TryGetSnapshot(MechanismId mechanismId, out MechanismSnapshot snapshot);
IReadOnlyList<MechanismSnapshot> GetActiveSnapshots();
bool TryGetWeaponSnapshot(out EscortWeaponSnapshot snapshot);
MechanismPlacementResult RequestPlace(ConfigId configId, Vector2 position, float rotationDegrees = 0f);
MechanismPlacementResult RequestPlaceAtAnchor(ConfigId configId, PlacementAnchorId anchorId, float rotationDegrees = 0f);
bool RequestRemove(MechanismId mechanismId);
```

`MechanismAuthoring` 为关卡场景和放置预制体提供稳定实例 ID、配置 ID 与碰撞适配。场景机关在 `LevelContext` 创建时注册；放置后端通过 `CanPlace` 和 `RequestPlace` 提交普通格，通过 `RequestPlaceAtAnchor` 执行 Anchor 安全替换，通过 `RequestRemove` 拆除。战役库存由 `CampaignResourceLedger` 扣除且拆除不返还；本系统负责同时在场上限、地图占位、碰撞注册、机关冷却和对象生命周期。当前 FirstLevel/Castle 的同时在场上限配置为 20，`Level_test` 为 5。

### 运行时机关层级与世界表现

运行时机关实例统一挂到关卡的 `MechanismsRoot`。机关 Prefab 根保留 `MechanismAuthoring` 和碰撞体，世界表现必须放在根下的 `VisualRoot`，由 `SpriteRenderer`、动画和特效组成；UI 图形和 `PlacementGridView` 不能作为机关视觉子物体。当前共享 `Default` Sorting Layer 的约定是地图底图 `-100`、机关 `1`、Actor `0`、护送武器 `2`、放置预览 `100+`。机关根的位置保存世界 XY，`VisualRoot` 局部位置固定为零，替换占位美术时不改变这个层级和运行时接口。

`TryGetWeaponSnapshot` 只读返回武器状态。武器伤害通过 `RequestWeaponDamage(ActorId, float)` 提交；运输移动与到达事实由固定路线适配器驱动，并发布 `WeaponDestroyedEvent` / `WeaponReachedGoalEvent`。`LevelResultRules` 消费这些事实，不保存武器的第二份运行时状态。

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
- 同时存在的放置物上限从 `LevelConfig.MaximumActivePlacedMechanisms` 读取；当前正式关卡为 20，测试关卡为 5；
- 引导源快照至少需要包含类型、位置、有效范围、是否可用和所属机制 ID；
- 多个同类引导源的随机选择、状态对应引导源优先级和战斗目标优先级由 Actor 感知/决策规则处理；
- 机关触发产生的生命、状态、击退、移动或目标效果必须通过对应系统的公开接口结算；
- 关卡重开、离开和卸载时，所有运行时机关、冷却、放置数量和订阅必须随 `LevelContext` 一起清理。

## 5.2 当前环境元素方案

策划图示中的环境元素先统一归入本系统，具体预制体和表现不改变其运行时所有权：

| 元素 | 当前规则方向 | 暂定限制 |
| --- | --- | --- |
| 食物 | 作为史莱姆引导源；成功进食一次后耗尽/移除，并通过 StatusManager 处理状态效果 | 战役库存由 `CFG_CampaignResources` 管理；当前开局 60、上限 60，进食增殖关闭 |
| 熔炉 | 当前关卡作为史莱姆引导源；不据视觉表现直接添加冷热状态 | 战役开局 3、上限 10；具体效果仍以现有配置/状态接线为准 |
| 障碍 | 运行时占位并注册动态阻挡；普通格重复放置拒绝；配置允许时可通过 Anchor 事务替换；支持拆除 | 战役开局 5、上限 15；正式关卡同时在场机关上限 20；拆除/消耗不返还战役库存 |
| 陷阱/补给 | 首关地刺由场景触发器按 `TrapDamageAuthoring` 请求持续伤害；宝箱增加食物；滚石由场景 Hazard 区域触发并结算伤害 | `FirstLevelHazardConfig` 资产目前未进入 Bootstrap 注册、运行时适配器未读取；相关场景参数不能描述为由这些资产驱动 |
| 护送武器 | `FirstLevel` 与 `Castle` 当前胜负目标；史莱姆搬运，守卫围绕武器防守，武器沿地图固定路线分段移动 | 抵达成功；摧毁或史莱姆耗尽失败；武器路线阻断时不使用史莱姆 A* 自动绕行 |

战役初始值为史莱姆 8/20、食物 60/60、熔炉 3/10、障碍 5/15。配置中的关卡奖励 ID 是 `FirstLevel`、`SecondLevel`、`ThirdLevel`；当前 Castle 的 LevelId 为 `Castle`，精确匹配会使第二关奖励失效，见场景/配置目录说明。

组合放置可以形成路径、阵型和连锁反应，但组合规则必须通过明确的触发事实和结果接口实现，不能由表现层自行推断。首关普通 Grid 放置验证边界和单格占用，特殊 Anchor 额外验证允许类型与替换策略；不提前实现复杂组合规则。

首关熔炉只提供 `GuideSourceSnapshot`，不得调用 `StatusManager` 添加炙热/寒冷状态。未来恢复温度玩法时再补充状态效果契约。

### 5.3 第一关三路线与护送武器路线

第一关路线由 `MapManager` 提供只读事实，史莱姆感知适配器消费分支和引导源并为史莱姆选择路线；玩家放置机关时，`MechanismSystem` 负责地图占位、碰撞注册和 Anchor 事务，不直接改 Actor 的位置。史莱姆自身移动使用 MovementSystem 的 A*，它与岔路路线选择是两层逻辑。

当前护送武器链路启用时，`MechanismSystem` 保存运输进度，并通过 `FixedRouteMovementAdapter` 按配置路点分段移动；Actor 专用的 A* 不套用到非 Actor 武器上。武器路线被障碍阻断时的动态绕行策略尚未实现。

路线上的障碍占用、陷阱触发和被阻断后的处理必须通过已确认的公开事实接口完成。各实现不得自行把“阻挡即失败”“忽略障碍”或“自动绕行”写成公共规则。

## 5.4 场景引导源与其他环境元素

温暖地区、低温地区、掉落物、固定事件和随机事件可以为史莱姆提供引导或触发条件。它们的边界如下：

- `MapManager` 负责地形、道路、区域归属、可行走空间、三路线和 Anchor 查询；区域进入/离开是空间事实，由 MapManager 发布；MechanismSystem 从引导源快照生成食物/熔炉事实，并用 MapManager 校验放置位置、注册运行时障碍；
- `MechanismSystem` 负责可交互环境元素及其引导源快照；
- `StatusManager` 负责寒冷、炙热、燃烧、冻结等状态和持续效果；
- `MovementSystem` 负责接触、阻挡、位置结果和移动请求；
- MapManager 中的 `DropSpawn`、`Event`、`Trap` 和 `Hindrance` 地图点只表示设计位置；FirstLevel 的地刺、宝箱和滚石由关卡组合根/场景适配器创建并接线，不属于通用机关配置实例；
- `CFG_Trap_*` / 补给配置尚未进入 Bootstrap 注册表，当前场景适配器没有读取这些配置。地刺数值由 `TrapDamageAuthoring` 提供；宝箱和滚石规则由对应场景组件提供。

### 5.5 FirstLevel 地图物件接入

`MAP_FirstLevel` 使用稳定地图点定位 `treasure_chest_01`、`rolling_rock_01`、`stone_art_02`、`swamp_art_01` 和 `elite_altar_art_01`。正式关卡由 `FirstLevelCompositionRoot` 根据这些点创建对应 SpriteRenderer；第二块石头仅表现，不带触发器。滚石触发区 `rolling_rock_trigger_01` 是 MapAreaKind.Hazard 多边形，装配器据其边界创建实际 Trigger Collider2D。只有 `rolling_rock_01` 会响应 Slime 进入，等待 1 秒后向右移动至触发区右边界。默认滚速为 6 Unity units/s，单次伤害为 20 HP，均可在 FirstLevel 场景的装配器组件上调整；伤害经 StatusManager 结算并按 ActorId 去重。

宝箱只接受活着的 Slime 接触，调用 `CampaignResourceLedger.AddFood(4)`，受关卡食物库存上限约束，成功增加后隐藏。祭坛 Sprite 与地图中的 EliteSpawn 点分别表达视觉和刷怪事实；当前无专属精英配置，仍使用该关已配置的 EnemyConfig。

Actor 进入或离开低温/温暖区域时，StatusManager 消费 MapManager 的区域事实并按配置申请或解除对应状态。暖区的吸引源快照由 MechanismSystem 从 MapManager 的区域边界和配置范围生成；区域归属本身不等同于吸引范围。`GetGuideSourcesInRange` 将像素半径按 Actor 精灵的 `pixelsPerUnit` 转换为世界距离，供感知层选择。

## 6. 依赖

- `ConfigSystem`；
- `MapManager`（地图事实、路线候选和 Anchor 查询）；
- `MechanismAuthoring` 提供的 2D 触发接触事实；移动阻挡由 `MapManager` 向 `MovementSystem` 暴露；
- `StatusManager` 处理效果；
- `CampaignResourceLedger` 管理放置库存、关卡入场快照和奖励；配置字段与 LevelId 必须匹配。
- `PlacementSystem` 提交玩家确认的环境元素放置结果；普通格子重复放置由 PlacementSystem 拒绝，Anchor 替换由双方公开契约协调。
- `SlimeManager`/`SlimeGroupActor` 查询引导源和元素影响，不直接修改机关状态。
- Core 结果规则组件优先观察第一关史莱姆终点统计/等待窗口事实；兼容武器链路可观察 `WeaponDestroyedEvent`、`WeaponReachedGoalEvent`，但不自动改变第一关成功口径，也不修改机关数据。

## 7. 关键不变量

1. 触发判定只在机关系统完成一次。
2. 冷却、使用次数、存在状态和可用性只能由机关系统修改。
3. 机关效果通过统一效果请求提交，不能绕过 `StatusManager` 或 `MovementSystem`；关卡结果通过事件交给 Core 结果规则组件。
4. 障碍是否阻挡某个 Actor 由地图/移动系统依据机关快照判定，机关系统不直接移动 Actor。
5. 普通网格重复放置必须拒绝；Anchor 替换只能在 Anchor 配置允许时由 `RequestPlaceAtAnchor` 安全事务提交，失败时保留旧对象。
6. Anchor、路线和陷阱是跨系统事实，不允许由机关预制体或 UI 直接改写 MapManager/Actor 私有数据。
7. 旧关卡的机关请求不能作用于重开的新 `LevelContext`。

## 8. 当前缺口与复盘项

- 修正 Castle 奖励的 LevelId 映射；
- 决定是否把陷阱/补给资产注册并接入场景适配器，避免配置资产与运行时硬编码并存；
- 完成护送武器被玩家障碍挡住时的绕行/等待/停止策略；当前固定路线可能因阻挡停滞；
- 继续核对食物、熔炉、障碍的数值配置是否与最新策划一致；运行时现有库存/上限规则不再属于待实现接口；
- 为餐车、Anchor 替换和拆除补充场景交互验收证据；静态存在代码不代表 UI 操作已做 Play Mode 验收。

## 补充：机制 Prefab 与场景组合边界（2026-10-14）

机制 Prefab、`MechanismAuthoring`、配置资产和场景预置点可以由负责人分别交付，再由集成测试场景通过 `[SerializeField] private` 引用组合。场景组合组件或 Placement 适配器不能直接 `Instantiate`、`Destroy` 或修改机制内部运行时数据；正式创建、替换、回收和生命周期仍由 `MechanismSystem` 负责。

`MechanismSystem` 在当前 `LevelContext` 中创建运行时机关并注册地图阻挡/事件。集成测试可以使用 Mock Backend 或占位 Prefab，但必须标记 `TEST_ONLY`，不能将测试对象或手动结果调用当成正式机关创建和第一关结果通过的证据。重开、失败、结算和卸载时，场景组合层只负责清理自己的表现/临时对象，机制运行时由当前 Context 统一 Dispose。

