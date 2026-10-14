# 16 MapManager 地图与空间信息管理器设计

系统编号：LEVEL-04<br>
系统名称：MapManager<br>
默认命名空间：SlimeBattle.World<br>
建议实现目录：Assets/Game/Scripts/World/Map/（核心、Authoring、Events）；地图和区域数据由 Assets/Game/Scenes/ 中的关卡场景提供<br>
生命周期：当前 LevelContext 生命周期<br>
状态：场景区域/点位查询、边界、地形倍率、区域转移事件和路线查询已实现；第一关三路线与 Anchor 几何待场景装配

## 1. 定位

MapManager 是当前关卡的地图事实与空间查询服务。它绑定场景中的地图资源和语义标记，为移动、放置、Actor、StatusManager 以及未来的掉落和事件系统提供一致的地图查询结果。MapManager 不复制 Tilemap 或场景数据作为第二份可写地图。

道路、地形和区域可以叠加在同一空间位置，是不同的地图语义层。例如一个位置可以同时属于主干道和沼泽地形。MapManager 负责报告这些事实；各系统根据自己的职责决定如何使用。

## 2. 职责

- 绑定当前场景的地图边界、Tilemap、静态碰撞体、道路/地形/区域标记、三条策划路线、Anchor/地图点；
- 提供世界坐标处的道路、地形、区域、可通行性及边界查询；
- 为 Actor Manager、EnemyManager、PlacementSystem、MovementSystem、StatusManager、CameraSystem 和 AI 提供统一的地图事实；
- 维护场景地图引用和查询索引，并在关卡销毁时释放；
- 根据 MovementSystem 提交的 Actor 位置更新，比较区域成员关系并发布进入/离开事件；
- 按已登记的场景静态碰撞体识别 MovementSystem 遇到的地图阻挡；
- 在场景地图绑定与校验完成后发布 MapReadyEvent。

地图信息由关卡场景提供，视觉地面仍可使用 Tilemap；语义道路、地形和区域首版使用带 Trigger Collider2D 的 `MapAreaAuthoring` 体积，地图点使用带稳定 ID 的 `MapPointAuthoring`。这允许地形、道路和温度区域在同一位置叠加。首版不引入随机地图生成器。Trigger 只作为地图查询几何，不替代 MovementSystem 的物理碰撞体。

## 3. 不负责的内容

- 不创建、销毁或索引 Actor、掉落物、机关、陷阱、随机事件和固定事件实例；
- 不决定 Actor 的目标、行为或通用动态寻路策略；首关只提供策划配置的路线候选事实和点位关联；
- 不执行 Rigidbody2D 位移和物理碰撞响应；
- 不结算伤害、状态、掉落概率、事件结果或目标结果；
- 不把地图点误当成已经生成的动态对象；
- 不把摄像机可见范围当作地图范围或占用规则。

地图只保存静态空间事实及场景中预先放置的标记。动态对象的创建、存活状态和玩法效果归对应 Manager/System 所有。掉落物、敌人、陷阱、阻挠点和事件位置可以由地图标记提供；其生成时机和结果不能由 MapManager 推断。

## 4. 地图语义层

| 语义层 | 图示内容 | MapManager 提供的事实 | 规则消费者 |
| --- | --- | --- | --- |
| 道路 | 主干道、分支道路 | 道路类型及当前位置是否位于道路区域；P0 不建立动态寻路图 | MovementSystem、AI、关卡设计工具 |
| 地形 | 沼泽、峡谷、城堡等 | 地形类型、地形标签和可配置的移动修正 | MovementSystem、StatusManager 或对应关卡规则 |
| 区域 | 低温、温暖等 | 区域 ID、类型、关联的效果配置 ID，以及 Actor 的进入/离开事实 | StatusManager、AI |
| 地图点 | 出生点、目标点、武器点、敌人点 | 稳定点 ID、点类型和世界坐标 | 对应 Actor Manager、MechanismSystem、Core 结果规则组件 |
| 路线候选 | 第一关三条预绘制路线及其分支/主线标记 | 稳定路线 ID、顺序路点、终点和关联阻断点 | 关卡路线规则、Actor/AI、Core |
| 放置 Anchor | L1/L2/L3、SL1/SL2/SL3 等策划指定点位 | 稳定 Anchor ID、世界坐标、允许类型、替换策略和关联路线/陷阱事实 | PlacementSystem、MechanismSystem、路线/陷阱规则 |
| 可放置区域 | 玩家可放置物体的区域标记 | 稳定区域 ID 和当前位置是否处于放置区域 | Placement/Mechanism 规则消费者 |
| 物理空间 | 静态碰撞体、地图边界 | 静态阻挡识别、边界和占用查询 | MovementSystem、PlacementSystem、CameraSystem |

道路、地形和区域是可组合查询的标签，不相互覆盖。主干道应按策划要求留出足够宽度；三条路线是策划预绘制的候选目标，不是 MapManager 内部生成的动态寻路图。Actor 是否选择某条路线、何时重新选择以及路线被放置物影响后的规则由对应关卡规则/决策模块决定，MapManager 只提供事实。

沼泽道路的当前图示规则是移速降低 50%。地形修正作为地图移动修正提供给 MovementSystem，与 StatusManager 提供的角色状态修正分别计算；多个重叠地形倍率按乘法叠加。具体倍率保存在区域标记中，不能在 Actor 或决策树中硬编码。

峡谷可标记为地形区域。图示提出悬崖/落差会导致史莱姆掉落消失，但落差层级、其他 Actor 的结果及销毁通知尚未定义；MapManager 只提供地形事实，不直接删除 Actor。城堡地形目前没有已确认的特殊规则。

## 5. 核心数据

```text
MapBounds
MapAreaId
MapAreaKind                 // Road, Terrain, Region, Placement
MapRoadKind                 // Main, Branch
MapTerrainKind              // Swamp, Canyon, Castle, Other
MapRegionKind               // LowTemperature, Warm, Other
MapAreaSnapshot
MapPointId
MapPointKind                // Spawn, Target, Weapon, EnemySpawn
MapPointSnapshot
MapRouteId
MapRouteSnapshot            // 有序路点、终点、分支和关联阻断事实
PlacementAnchorId
PlacementAnchorSnapshot    // Anchor 位置、允许类型、替换策略和关联规则事实
MapSpatialSnapshot          // 边界、区域标签及地图修正的只读组合快照
OccupancyQuery
```

MapArea/MapPoint 的 ID 在同一关卡中必须唯一且稳定；名称只用于编辑器显示，不能作跨系统 ID。查询返回只读值或快照，不暴露可写的场景组件。

## 6. 公共接口

```csharp
bool TryGetMapBounds(out MapBounds bounds);
MapSpatialSnapshot Query(Vector2 worldPosition);
bool CanOccupy(Vector2 worldPosition, OccupancyQuery query);
float GetMovementSpeedMultiplier(Vector2 worldPosition);
bool TryGetMapPoint(MapPointId pointId, out MapPointSnapshot point);
IReadOnlyList<MapPointSnapshot> GetMapPoints(MapPointKind kind);
bool TryGetMapArea(MapAreaId areaId, out MapAreaSnapshot area);
bool TryGetRoute(MapRouteId routeId, out MapRouteSnapshot route);
IReadOnlyList<MapAreaSnapshot> GetMapAreaSnapshots();
bool TryGetMapAreaCenter(MapAreaId areaId, out Vector2 center);
```

Anchor 查询的最终公开签名待代码评审确认；MapManager 只提供只读空间事实。

`Query` 返回同一位置上的道路、地形和区域标签。`GetMovementSpeedMultiplier` 返回地图地形带来的移动修正，不包含 Actor 的状态修正；当前对所有与位置重叠的地形区域直接乘算，不根据道路标签筛选。要实现图示的沼泽路段降速 50%，场景作者需在对应 `MapAreaAuthoring` 上配置 `0.5`，并让标记体覆盖受影响路段。若规则最终要求移速修正只在特定道路生效，再增加路地关系筛选。`CanOccupy` 查询可选地图边界、场景静态阻挡和其他系统显式注册的移动阻挡体（当前用于放置障碍）；Actor 与非阻挡环境物不纳入占位查询。`RegisterMovementBlocker` 只接受当前场景内的非 Trigger 静态碰撞体；调用方需在对象移除时注销。

`MapAreaKind.Placement` 用带 Trigger Collider2D 的 `MapAreaAuthoring` 标记允许放置的区域。`Query` 会返回此类空间事实，但 `CanOccupy` 只验证地图边界与阻挡，不自动判定区域白名单；放置规则消费者需显式检查查询结果。它不替代网格映射、格子占用或放置合法性规则。

路线由场景中的 `MapRouteAuthoring` 配置。路线和路点 ID 在关卡内稳定唯一；路点按序列化列表顺序生成从 0 开始的 `Sequence`，至少包含起点和终点，且只有最后一点可标记为 Goal。第一关配置三条候选路线及其关联的 L/SL 点位；路线快照可以携带分支、主线、可被阻断/受陷阱影响等设计事实，但不携带运行时选择结果。路点通过同场景 Transform 提供 Unity 世界 XY 坐标；配置了地图边界时，越界路线会被拒绝并记录错误。`MapRouteSnapshot` 是只读快照，不引用 authoring 组件。

Anchor 由场景 authoring 组件提供稳定 ID、世界坐标、允许的放置类型、是否允许替换以及关联路线/陷阱事实。Anchor 是策划点位白名单，不是由普通 Grid 自动推导的格子集合。MapManager 只查询和校验这些静态事实，不执行替换、回收、路线阻断或陷阱触发。

MovementSystem 在 Actor 注册、物理位置同步和注销时，向 MapManager 提交位置变化/离场通知。它们是关卡内部的协作接口，不进入跨系统全局 API 表。MapManager 用位置查询维护区域成员关系，不保存 Actor 的位置副本；每次只在成员集合发生变化时发布边界事件。

## 7. 对外事件

- `MapReadyEvent`：当前场景地图资源绑定和静态索引校验已完成；
- `MapAreaEnteredEvent`：Actor 首次进入或移动进入一个登记区域，携带 ActorId、区域只读快照和发生位置；
- `MapAreaExitedEvent`：Actor 离开一个登记区域，携带 ActorId、区域只读快照和发生位置。

MapArea 事件只报告已经发生的空间事实，不包含效果结算结果。StatusManager 可根据区域类型和关联配置提交状态/效果；MovementSystem 读取地形移动修正；其他系统可以查询同一地图快照。MapManager 不直接调用 StatusManager。

当 Actor 在生成时已经位于区域内，应按进入该区域处理。Actor 销毁时清理其成员关系；目标已经失效时不为其继续发布区域事件。进入/离开事件绑定当前 LevelEventBus，并随 LevelContext 一起清理。

## 8. 与其他系统的关系

```text
关卡场景 / Tilemap / 地图标记
  → MapManager 建立静态空间索引

MovementSystem 提交 Actor 当前世界位置
  → MapManager 比较区域成员关系
  → MapAreaEnteredEvent / MapAreaExitedEvent
  → StatusManager 根据区域配置申请状态或效果

MovementSystem 查询当前地形移动修正
  → MapManager 返回地图事实
  → MovementSystem 与 StatusManager 的 Actor 修正一起计算实际速度

MapManager 提供敌人点 / 终点洞穴 / 三条路线 / Anchor 点位
  → 对应系统按自身规则生成、管理动态对象并消费路线/Anchor 事实
```

- `MovementSystem` 是 Actor 位置变化的执行方，向 MapManager 报告物理位置；MapManager 不从 Actor 私有字段读取位置；
- `StatusManager` 是状态与效果的唯一结算者，管理寒冷/炙热的持续时间、离开区域后的延迟解除和互斥；
- `MechanismSystem` 管理可交互和玩家放置环境元素；现有护送武器链路若继续保留也由它拥有；MapManager 只提供静态点位、路线候选、Anchor 与空间事实；
- `EnemyManager` 等对象管理器可以查询地图点，但仍由自身创建、索引和销毁对象；
- 通用掉落物、背包和道具不进入首关；第一关若配置敌人掉落食物，由对应局部供给规则消费地图点/事件事实，MapManager 不创建通用运行时对象。

## 9. 当前实现与关键不变量

当前代码由 LevelContext 绑定活动场景，缓存非 Trigger 静态 Collider2D，并扫描 `MapAreaAuthoring`、`MapPointAuthoring`、`MapRouteAuthoring` 和可选的 `MapBoundsAuthoring`。MovementSystem 已查询当前位置的地形移速倍率，并把刚体位置同步给 MapManager；区域进入/离开事件和 MapReadyEvent 已接入本局 EventBus，StatusManager 消费区域事件并按 `StatusEffectConfigId` 解析状态配置。MechanismSystem 可额外注册玩家放置的静态障碍碰撞体，供 `CanOccupy` 和 MovementSystem 查询。现有固定路线由 `TryGetRoute` 提供；第一关三路线、L/SL Anchor 的 authoring 和终点洞穴统计尚未完成正式场景装配与独立运行验证。`FixedRouteMovementAdapter` 仍是兼容的分段路线适配，不等同于第一关最终路线选择规则；完整的动态 Actor 占用与峡谷结果仍未实现。

1. 同一 LevelContext 内所有系统使用相同的世界坐标和地图索引。
2. MapManager 不拥有任何 Actor、掉落物、机关或事件实例的生命周期。
3. 位置查询不依赖摄像机是否看见对象。
4. 场景重开后必须重新扫描场景并清空旧关卡的区域成员索引。
5. 重复/无效 ID 和无效边界配置必须可诊断；地图边界是可选数据，缺失时在快照中明确表示。每个关卡创建新的 MapManager，不得沿用上一关缓存。
6. 区域事件只在成员关系变化时发布，不把每帧的空间查询变成事件流。

## 10. 待确认项

- 道路、地形和区域最终使用语义 Tilemap、Collider2D 体积标记还是两者组合；
- 第一关三路线、终点洞穴、L1/L2/L3、SL1/SL2/SL3 的正式坐标和区域范围；MapBounds 使用 Unity 世界 XY 单位，未配置时边界查询返回缺失；
- 除图示中沼泽道路移速降低 50% 外的地形移动修正数值，以及沼泽减速是否只作用于道路标签；
- 多个温度区域重叠时状态的进入、优先级、互斥和离开计时；
- 峡谷的层级、可通行判定、坠落 Actor 范围和销毁/掉落结果；
- 掉落物、固定/随机事件和阻挠点的正式运行时系统归属；
- 动态放置障碍如何注册到移动阻挡查询；普通格子/Anchor 的静态事实已确认，路线被障碍影响后的选择时机和同帧优先级待确认；
- L1/L2/L3 对应路线的阻断事实、SL1/SL2/SL3 对应陷阱事实如何由公开接口传递；MapManager 只报告事实，不自动绕行、替换或判失败；
- 窄路是否只使用普通道路标记，还是需要 PassageSystem 的排队/限流行为；
- 随机地图或运行时地图改造是否进入后续版本。

## 11. 验收场景

- 场景重开后 MapManager 只保留新场景引用和索引；
- 同一坐标查询可同时返回道路和地形标签，并按配置倍率乘算地形移速修正；
- Actor 出生在温暖/低温区域时产生一次进入事件；跨入、跨出区域时各发布一次对应事件，静止帧不重复发布；
- 沼泽移动修正通过地图查询提供给 MovementSystem，不改变 Actor 配置或状态数据；
- 各类地图点以稳定 ID 查询成功，缺失或重复 ID 可以诊断；
- 静态 Collider2D 阻挡识别继续兼容当前 `Level_test` 场景；
- 地图点和 Anchor 只提供稳定位置/配置事实，敌人、陷阱、补给和其他运行时对象仍由各自所有者管理。
- 第一关三条路线可以按稳定 ID 查询，并能查询其关联的阻断/陷阱点；不会被误解释为 MapManager 内部动态寻路结果。

## 12. 首关场景作者标注约定

- **视觉地面/地形：**使用 Tilemap、SpriteRenderer 等纯表现资源；视觉 Tilemap 不自动成为地形事实或阻挡体。
- **道路、地形、温度区和可放置区：**各用带 Trigger Collider2D 的 `MapAreaAuthoring`。`kind` 分别设为 Road、Terrain、Region、Placement；每个区域填稳定 `areaId`。沼泽按策划确定范围配置 `movementSpeedMultiplier = 0.5`。多个语义层可在同一位置叠加。
- **静态碰撞：**墙体、边界和不可通行物使用非 Trigger Collider2D，且不挂动态 Rigidbody2D（或使用 Static Rigidbody2D）。它们提供移动阻挡事实，不以视觉 Tilemap 代替。
- **地图边界/可放置范围：**用唯一的 `MapBoundsAuthoring` 配置本关 Unity 世界 XY 轴对齐边界；它是地图外框。若可放置区域只占地图一部分，再用 `MapAreaKind.Placement` 标出区域，放置规则消费者据此验白名单。格子尺寸和网格原点由放置系统配置，MapManager 不推导。
- **出生/敌人/终点洞穴：**用 `MapPointAuthoring`，为每个对象配置稳定 `pointId` 和对应类型，Transform 世界坐标即查询坐标。护送武器点仅作为现有兼容链路的可选点位，不自动成为第一关胜利目标。
- **三条路线：**在路线根对象添加 `MapRouteAuthoring`，每条路线按从起点到终点的顺序填写稳定路点 ID 和场景内 marker Transform；只把最后一个路点标记为 Goal，并在路线配置中记录分支/主线及关联 L/SL 点位。marker 必须位于地图边界内。
- **Anchor：**使用建议的 Anchor authoring 组件（最终类型名待代码评审确认），为 L1/L2/L3、SL1/SL2/SL3 配置稳定 ID、世界坐标、允许机关类型、替换策略以及关联路线/陷阱。Anchor 是放置规则事实，不负责实例化或回收对象。
- **机制：**路线表示策划预绘制几何，L/SL 关联点影响路线/陷阱规则；不在地图作者脚本里配置自动绕行、替换、陷阱结算或失败策略。

当前第一关草图的正式 Unity 场景尚未完成装配，因此尚未把草图坐标写入生产场景；以上规则用于场景标注、静态检查和后续运行验证。

## 补充：地图 Authoring 的独立交付与组合场景（2026-10-14）

小智可以在独立地图测试场景中交付 `MapBoundsAuthoring`、`MapRouteAuthoring`、`MapPointAuthoring`、地形/区域 Authoring 和 Anchor Authoring。集成测试场景（暂定 `FirstLevelComposition_TEST`）通过场景引用组合这些对象，`MapManager` 在当前关卡 Context 创建时统一扫描并建立快照。

地图 Authoring 只提供空间事实，不复制 `MapManager`，也不在场景脚本中实现路线阻断、Anchor 替换、陷阱结算或 Actor 创建。Anchor 最终查询接口未确认时，可以交付 Authoring 和隔离测试，但不得要求 Placement/UI 依赖临时字段名；共享组合场景由指定集成者维护，避免多人同时编辑同一个 `.unity` 文件。
