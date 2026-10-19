# 99 全局 API 查询表

版本：0.6 第一关地图机关子模块版
最后更新：2026-10-15
用途：协作时查询跨系统公共接口
状态：架构暂定

## 1. 使用规则

- 本表只记录跨系统公共接口，不记录系统内部方法、私有回调和具体节点实现；
- 完整职责、前置条件和失败语义以对应系统设计文档为准；
- 暂定接口可以在实现前调整，调用方不得把它当成不可变协议；
- 公共接口变化时，必须同步更新所属系统文档和本表；
- Request 表示请求执行，Query 或 Get/TryGet 表示读取，Event 表示结果已经发生；
- ActorDecisionTree、ActorStateMachine 和 AttackAction 的内部节点、状态和执行函数不进入全局表。
- `MechanismSystem` 的逻辑归属为 `Map` 领域下辖子模块，当前由小智维护；本次只更新责任边界，不新增或冻结公共 API。

## 2. 核心与目标 API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-CORE-001 | GameRoot | InitializeApplication() | 无 | 初始化结果 | 启动入口 | 已确认 |
| API-CORE-002 | GameRoot | RequestEnterLevel(LevelId) | 关卡 ID | 请求结果 | 主菜单、启动入口 | 暂定 |
| API-CORE-003 | LevelManager | RequestLoadLevel(LevelId) | 关卡 ID | 加载事件 | GameRoot、UI | 已确认 |
| API-CORE-004 | LevelManager | RequestRestartCurrentLevel() | 无 | 重开事件 | UI、调试工具 | 已确认 |
| API-CORE-005 | LevelManager | RequestPause() / RequestResume() | 无 | 暂停/恢复事件 | UI、系统流程 | 已确认 |
| API-CORE-006 | LevelManager | GetLevelState() | 无 | LevelState | UI、调试、系统 | 已确认 |
| API-CORE-007 | LevelContext | GetEventBus() | 无 | 当前关卡 EventBus | 当前关卡系统 | 已确认 |
| API-CORE-008 | Core 结果规则组件 | Initialize(LevelEventBus, LevelResultConfig) | 当前关卡 EventBus、结果配置 | 无 | LevelContext | 迁移待实现；最终类型名待确认 |
| API-CORE-009 | Core 结果规则组件 | GetSnapshot() | 无 | LevelResultSnapshot | LevelManager、UI、测试 | 迁移待实现 |
| API-CORE-010 | Core 结果规则组件 | Dispose() | 无 | 无 | LevelContext | 迁移待实现 |
| API-CORE-011 | Core 结果规则组件 | 结果事件聚合 | 第一关终点进入统计、等待窗口结束、`SlimeDepletedEvent` 等已确认事实 | `LevelSucceededEvent` / `LevelFailedEvent` | LevelManager、UI、测试 | 第一关口径已确认；事件 payload/代码待迁移 |
| API-CORE-014 | Core 结果规则组件 | 史莱姆终点统计与等待窗口（候选接口） | `SlimeReachedGoalEvent` / `LevelWaitWindowExpiredEvent` | 只读结果快照 | LevelManager、UI、测试 | 设计已确认；最终事件名和参数待评审 |
| API-CORE-015 | Core 结果规则组件 | 兼容结果事件适配 | `WeaponDestroyedEvent` / `WeaponReachedGoalEvent` | 兼容关卡结果事实 | 仅启用护送武器链路的关卡 | 保留兼容；不作为最新第一关成功条件 |
| API-CORE-012 | LevelContext | BindActorPorts(IActorPerceptionSource, IActorActionPort) | 当前关卡感知及行动请求适配器；效果端口可为 null | 无 | 关卡装配方 | P0 接入边界；MovementSystem 接管移动请求；效果端口为空时仅移动可用 |
| API-CORE-013 | LevelContext | MapManager / MovementSystem | 无 | 当前关卡地图和移动服务 | 当前关卡系统 | P0：静态阻挡识别及 Rigidbody2D 移动已接入 |

AI 选路验收场景位于 `Assets/Game/Tests/AIPathScene/`。它仅通过 Actor 的公开 Tick/效果接收/位置回报接口及上述感知、行动端口测试通路；`FleeCheckSucceeded` 是注入的概率判定结果，效果请求只记录，不由场景扣血。

新一轮独立分支场景位于 `Assets/Game/Tests/AIBranches/Scenes/`：由正式 GameRoot/LevelManager/LevelContext 装配，ActorTargetPerceptionSource 负责正式敌我感知，MovementSystem 执行移动，StatusManager 结算已注册 Actor 的攻击和测试前置状态；测试代码补食物、热源、逃跑判定和未注册的人类目标。食物等引导坐标目前写死在测试感知中，视觉圆点不是感知源，移动圆点不会更新 AI 目标；“首个分支通过”只检查选路关键词。旧 `AIPathScene` 不作为正式移动验收依据。

## 3. 配置 API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-DATA-001 | ConfigSystem | TryGet<TConfig>(ConfigId, out TConfig) | 配置 ID | 是否成功、配置 | 关卡和玩法系统 | 已确认 |
| API-DATA-002 | ConfigSystem | TryGetLevel(LevelId, out LevelConfig) | 关卡 ID | 是否成功、配置 | LevelManager、MapManager | 已确认 |
| API-DATA-003 | ConfigSystem | ValidateAll() | 无 | 校验报告 | 编辑器工具、启动检查 | 已确认 |
| API-DATA-004 | LevelResultConfig（候选名称） | 第一关终点目标数量、等待窗口和失败条件 | 无 | 结果规则配置 | Core 结果规则组件 | 第一关“至少 6 只史莱姆”已确认；最终字段待确认 |
| API-DATA-005 | ConfigSystem | TryGetActor(ConfigId, out ActorConfig) | 配置 ID | 是否成功、配置 | Actor Manager | 已实现 |
| API-DATA-006 | ConfigSystem | TryGetStatus(StatusId, out StatusConfig) | 状态 ID | 是否成功、配置 | StatusManager、MapManager 区域适配 | 已实现；重复 StatusId 拒绝 |
| API-DATA-007 | ConfigSystem | TryGetStatus(StatusKind, out StatusConfig) | 玩法状态种类 | 是否成功、配置 | StatusManager | 已实现；重复 StatusKind 拒绝 |

## 4. LevelContext Manager API

### 4.1 SlimeManager

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-LEVEL-001 | SlimeManager | SpawnSlime(SlimeSpawnRequest) | 生成请求 | ActorId 或拒绝原因 | LevelContext、测试 | 已确认 |
| API-LEVEL-002 | SlimeManager | TryGetSnapshot(ActorId, out SlimeSnapshot) | 史莱姆 ID | 是否成功、快照 | AI、UI、表现 | 已确认 |
| API-LEVEL-003 | SlimeManager | GetAliveSnapshots() | 无 | 史莱姆只读快照列表 | Core 结果规则组件、UI、AI | 已确认 |
| API-LEVEL-004 | SlimeManager | RequestDespawn(ActorId, DespawnReason) | ID、原因 | 是否成功 | LevelContext、测试 | 已确认 |
| API-LEVEL-005 | SlimeManager | TryGetGroupSnapshot(GroupId, out SlimeGroupSnapshot) | 群体 ID | 是否成功、快照 | UI、调试、Actor | 暂定 |
| API-LEVEL-014 | SlimeManager | TryCreateGroup(members, target, out GroupId) / TrySetGroupTarget(GroupId, target) / RequestDissolveGroup(GroupId) | 明确成员和目标 | 是否成功 | 关卡规则、测试 | P0 显式群体关系；自动规则待确认 |
| API-LEVEL-015 | SlimeManager | TrySetSpawnParent(Transform) | 场景 SlimeRoot | 是否成功；后续生成实例的父节点 | 关卡场景装配组件 | P0 |

### 4.2 EnemyManager

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-LEVEL-006 | EnemyManager | SpawnEnemy(EnemySpawnRequest) | 生成请求 | ActorId 或拒绝原因 | LevelContext、测试 | 暂定 |
| API-LEVEL-007 | EnemyManager | TryGetSnapshot(ActorId, out EnemySnapshot) | 敌人 ID | 是否成功、快照 | AI、UI、表现 | 暂定 |
| API-LEVEL-008 | EnemyManager | GetAliveSnapshots() | 无 | 敌人只读快照列表 | Actor、UI | 暂定 |
| API-LEVEL-009 | EnemyManager | RequestDespawn(ActorId, DespawnReason) | ID、原因 | 是否成功 | LevelContext、测试 | 暂定 |
| API-LEVEL-016 | EnemyManager | TrySetSpawnParent(Transform) | 场景 EnemyRoot | 是否成功；后续生成实例的父节点 | 关卡场景装配组件 | P0 |

### 4.3 GuardManager

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-LEVEL-010 | GuardManager | SpawnGuard(GuardSpawnRequest) | 生成请求 | ActorId 或拒绝原因 | LevelContext、测试 | 暂定 |
| API-LEVEL-011 | GuardManager | TryGetSnapshot(ActorId, out GuardSnapshot) | 守卫 ID | 是否成功、快照 | UI、AI、表现 | 暂定 |
| API-LEVEL-012 | GuardManager | GetAliveSnapshots() | 无 | 守卫只读快照列表 | UI、MechanismSystem、测试 | 暂定 |
| API-LEVEL-013 | GuardManager | SubmitCommand(ActorId, GuardCommand) | ID、语义指令 | 接受/拒绝 | InputSystem、调试工具 | 调试/待确认 |

Manager 不对外提供 ApplyEffect。生命和状态变化统一由 StatusManager 经过 LevelContext 内部 Actor 解析契约处理。

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-ACTOR-001 | SlimeActor / EnemyActor | NotifyMovementResult(Vector2) / NotifyMovementCompleted(string) | 已执行位置、当前移动请求 ID | 完成回执是否接受 | MovementSystem | P0 已接入 |

## 5. 地图与移动 API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-WORLD-001 | MapManager | TryGetMapBounds(out MapBounds) | 无 | 是否成功、地图边界 | CameraSystem、MovementSystem、AI | 已实现；支持 MapBoundsAuthoring 或 MapLayoutAuthoring 引用的离线地图定义 |
| API-WORLD-002 | MapManager | Query(Vector2) | 世界坐标 | MapSpatialSnapshot（道路、地形、区域、可放置区域和边界事实） | MovementSystem、AI、PlacementSystem、机关 | 已实现；区域支持 MapAreaAuthoring 或离线地图圆形区域数据 |
| API-WORLD-003 | MapManager | CanOccupy(Vector2, OccupancyQuery) | 位置、边界/半径/静态阻挡条件 | 是否可占用 | MovementSystem、PlacementSystem、AI、MechanismSystem | 已实现静态地图查询及已注册的放置障碍；不含动态 Actor |
| API-WORLD-004 | MapManager | GetMovementSpeedMultiplier(Vector2) | 世界坐标 | 地形移速修正（多个地形倍率相乘） | MovementSystem | 已实现；场景需配置地形区域 |
| API-WORLD-005 | MapManager | TryGetMapArea(MapAreaId, out MapAreaSnapshot) | 区域 ID | 是否成功、区域快照 | StatusManager、AI、关卡规则 | 已实现 |
| API-WORLD-006 | MapManager | TryGetMapPoint(MapPointId, out MapPointSnapshot) / GetMapPoints(MapPointKind) | 地图点 ID 或类型 | 是否成功、点位快照集合 | Actor Manager、MechanismSystem、Core 结果规则组件 | 已实现；第一关登记出生/终点洞穴/敌人点，兼容武器点可选 |
| API-WORLD-007 | MovementSystem | SubmitMovement(MovementRequest) | 移动请求 | MovementRequestId | ActorStateMachine、InputSystem | P0 已实现 |
| API-WORLD-008 | MovementSystem | CancelMovement(MovementRequestId, CancelReason) | 请求 ID、原因 | 是否取消 | ActorStateMachine、Manager | P0 已实现 |
| API-WORLD-009 | MovementSystem | TryGetMovementState(ActorId, out MovementState) | Actor ID | 是否成功、状态 | Actor、UI、调试 | P0 已实现 |
| API-WORLD-010 | MovementSystem | TryGetContactInfo(ActorId, out ContactInfo) | Actor ID | 是否成功、最近阻挡接触 | Actor、调试、UI | P0 已实现 |
| API-WORLD-011 | MapManager | TryGetRoute(MapRouteId, out MapRouteSnapshot) | 第一关路线段 ID | 是否成功、顺序路点、路线角色、合流点、终点及关联阻断/地图点事实 | 关卡路线规则、Actor/AI、兼容 MechanismSystem | 已实现；FirstLevelMapDefinition 提供 3 条分支段和 1 条主线段 |
| API-WORLD-015 | MapManager | TryGetPlacementAnchor(PlacementAnchorId, out PlacementAnchorSnapshot) / GetPlacementAnchors() | Anchor ID（可选） | 是否成功、位置、允许类型、替换策略和关联路线/地图点事实 | PlacementSystem、MechanismSystem、关卡规则 | 已实现只读查询；首关坐标数据已录入 |
| API-WORLD-012 | MapManager | GetMapAreaSnapshots() / TryGetMapAreaCenter(MapAreaId, out Vector2) | 区域 ID（可选） | 区域快照集合或区域中心 | MechanismSystem | 已实现 |
| API-WORLD-013 | MapManager | RegisterMovementBlocker(Collider2D) / UnregisterMovementBlocker(Collider2D) | 当前场景静态碰撞体 | 是否注册/注销 | MechanismSystem | 已实现；放置障碍加入 CanOccupy 查询 |
| API-WORLD-014 | `FixedRouteMovementAdapter` / `IFixedRouteSegmentMotor` | Start() / Tick() / RetryBlockedSegment() / Cancel()；TryGetPosition() / SubmitSegment() / TryGetResult() / CancelSegment() | 固定路线快照、当前位置和分段移动端口 | 按路点提交的分段状态及阻挡接触事实 | EscortWeapon 适配层 | 已实现契约和序列适配；物理端口由 MechanismSystem 提供，不执行寻路或阻挡策略 |

## 6. StatusManager API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-STATUS-001 | StatusManager | ApplyEffect(EffectRequest) | 带唯一请求 ID 的来源、目标、效果 | EffectApplyResult | ActorAction、环境、玩法适配器 | 已实现；当前目标为 Actor |
| API-STATUS-002 | StatusManager | ApplyStatus(StatusApplicationRequest) | 状态申请 | 是否成功 | Actor、机关、环境 | 已实现 |
| API-STATUS-003 | StatusManager | RemoveStatus(ActorId, StatusId, StatusRemoveReason) | Actor、状态、原因 | 是否成功 | Actor、关卡规则 | 已实现 |
| API-STATUS-004 | StatusManager | TryGetHealthSnapshot(ActorId, out HealthSnapshot) | Actor ID | 是否成功、生命快照 | AI、UI、Actor Manager | 已实现 |
| API-STATUS-005 | StatusManager | TryGetStatusSnapshot(ActorId, out StatusSnapshot) | Actor ID | 是否成功、状态快照 | AI、UI、状态机 | 已实现 |
| API-STATUS-009 | StatusManager | HasStatus(ActorId, StatusId/StatusKind) | Actor 与状态标识/种类 | 是否具有该状态 | AI、玩法规则 | 已实现 |
| API-STATUS-010 | StatusManager | TryGetStatus(ActorId, StatusId, out StatusEntrySnapshot) | Actor 与状态 ID | 是否成功、状态条目（含已持续时间） | AI、UI | 已实现 |
| API-STATUS-011 | StatusManager | NotifyFoodConsumed(ActorId, StatusId) | 史莱姆与可选冷/热食状态 | 是否处理 | 食物交互适配 | 已实现 |
| API-STATUS-012 | StatusManager | NotifyGuideReached(ActorId, StatusGuideKind) | 史莱姆与引导源种类 | 是否处理 | 感知/交互适配 | 已实现 |
| API-STATUS-013 | StatusManager | NotifyFurnaceCoreEntered/Exited(ActorId) | 火炉核心成员变化 | 是否处理 | MechanismSystem | 已实现 |
| API-STATUS-014 | StatusManager | NotifyInjuredHumanNearby(ActorId) | 受伤人类在嗅觉范围内的史莱姆 | 是否触发暴躁 | 感知适配 | 已实现 |
| API-STATUS-006 | StatusManager | GetMovementModifiers(ActorId) | Actor ID | MovementModifierSnapshot | MovementSystem | 已实现 |
| API-STATUS-007 | StatusManager | GetAttackSpeedMultiplier(ActorId) | Actor ID | 攻击速度倍率 | ActorStateMachine 行动端口 | 已实现 |
| API-STATUS-008 | StatusManager | Tick(StatusTickContext) | 当前帧间隔 | 更新状态计时与周期效果 | LevelContext | 已实现 |

ActorDecisionTree 只查询状态，不通过 StatusManager 直接造成效果。AttackAction 在命中时机使用 API-STATUS-001。

## 7. 机关、输入、镜头和 UI API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-MECH-001 | MechanismSystem | NotifyContact(MechanismTriggerContext) | 接触事实 | 是否受理 | MovementSystem | 首关待实现 |
| API-MECH-002 | MechanismSystem | RequestActivate(MechanismId, MechanismActivationRequest) | 机关 ID、请求 | 激活结果 | PlacementSystem、测试 | 首关待实现 |
| API-MECH-005 | MechanismSystem | TryGetWeaponSnapshot(WeaponId, out WeaponSnapshot) | 武器 ID | 是否成功、武器快照 | GuardManager、UI、Core 结果规则组件 | 首关候选接口；待实现 |
| API-MECH-006 | MechanismSystem | Weapon 伤害/运输结果入口（具体签名待确认） | 受击或运输事实 | `WeaponDamagedEvent` / `WeaponDestroyedEvent` / `WeaponReachedGoalEvent` | Actor/敌人、MovementSystem、测试 | 所有权已确认；签名待冻结 |
| API-MECH-003 | PassageSystem | RequestEnter(PassageAccessRequest) | 通行请求 | 通过/等待/拒绝 | ActorStateMachine、MovementSystem | 待决定 |
| API-MECH-004 | PassageSystem | NotifyExit(PassageId, ActorId) | 窄路、Actor ID | 是否释放 | MovementSystem、Manager | 待决定 |
| API-PLAYER-001 | InputSystem | ReadSnapshot() | 无 | PlayerInputSnapshot | CameraSystem、PlacementSystem、GuardManager | 已确认 |
| API-PLAYER-002 | InputSystem | SetInputMode(InputMode) | Disabled / Gameplay / UiOnly | void；非法值抛异常 | 场景接入、UI | P0 已实现 |
| API-PLAYER-003 | PlacementSystem | BeginPlacement(PlacementItemId) | 机关选择 ID（不是背包道具） | 开始结果 | UI、InputSystem | 首关范围已确认；最终名称待确认 |
| API-PLAYER-004 | PlacementSystem | PreviewPlacement(WorldPosition) | 世界位置 | 校验结果 | InputSystem、UI | 待确认 |
| API-PLAYER-005 | PlacementSystem | ConfirmPlacement() | 无 | PlacementResult | UI、InputSystem | 待确认 |
| API-PLAYER-010 | PlacementSystem | ResolvePlacementTarget(WorldPosition)（候选） | 世界位置 | 普通 GridCell 或特殊 PlacementAnchorId，以及合法性 | InputSystem、UI、测试 | Grid + Anchor 设计已确认；最终接口待评审 |
| API-PRESENT-001 | CameraSystem | ScreenToWorld(Vector2) | 屏幕像素位置 | Vector3，XY 玩法平面世界位置 | 玩家操作适配、UI | P0 已实现 |
| API-PRESENT-002 | CameraSystem | SetBounds(CameraBounds) | 镜头边界 | 无 | MapManager、LevelContext | 已确认 |
| API-UI-001 | UISystem | Refresh(UiState) | UI 状态 | 无 | UI 内部、状态同步 | 已确认 |
| API-UI-002 | UISystem | SubmitCommand(UiCommand) | Pause / Resume / Restart | void；UiCommandSubmittedEvent.Accepted | 按钮、面板 | P0 已实现 |

### 7.1 小宸 P0 补充接口（2026-10-09）

既有 InputSystem / UISystem 的 void 方法保持原系统设计签名；上表输出列中的“接受/拒绝”是事件结果。镜头概念坐标类型在 P0 使用 Vector2 / Vector3，调用方接入前请审核。

| API 编号 | 提供者 | 接口 | 输入 / 输出 | 调用方与生命周期 |
| --- | --- | --- | --- | --- |
| API-PLAYER-006 | InputSystem | GetInputMode() | InputMode | 输入使用方 |
| API-PLAYER-007 | InputSystem | Subscribe<T>(Action<T>) / Unsubscribe(InputSubscriptionToken) | Subscribe 返回专用 InputSubscriptionToken；token 限本实例 | 未来交互适配器、UI；禁用时解绑 |
| API-PLAYER-008 | InputSystem | Enable() / Disable() | void | 场景接入；禁用清空手势 |
| API-PLAYER-009 | PlayerPresentationRoot | Initialize(LevelManager, CameraSystem, IUiSnapshotSource = null, BattleHudView = null) | 显式当前关卡依赖 | 关卡场景接入，详见 24 |
| API-PRESENT-003 | CameraSystem | Initialize() | void | 场景接入，先于查询/命令 |
| API-PRESENT-004 | CameraSystem | WorldToScreen(Vector3) / GetState() | Vector2 / CameraState | UI、调试 |
| API-PRESENT-005 | CameraSystem | SubmitCommand(CameraCommand) | 世界 Pan / Focus、尺寸 Zoom | 输入适配、场景流程 |
| API-UI-003 | UISystem | Initialize(LevelManager, InputSystem, IUiSnapshotSource = null, BattleHudView = null) | 必须有 LevelManager；其他可选 | 场景接入 |
| API-UI-004 | IUiSnapshotSource | ReadSnapshot(LevelContext) | UiState 显示数据 | UISystem；初始化/阶段变化/5 Hz 查询 |
| API-UI-005 | UISystem | OpenPanel(UiPanelId) / ClosePanel(UiPanelId) / GetState() | Hud / Pause / Result；只读 UiState | 界面调用方，不改游戏状态 |

输入事件：PointerPressedEvent / PointerMovedEvent / PointerReleasedEvent / CancelInputEvent / PlayerCommandSubmittedEvent。取消的 IsUserRequested 区分用户取消与生命周期清理；消费者清除待处理交互。镜头与 UI 的状态/面板/指令事件使用组件上的 C# event，名称见所属文档。

2026-10-10 反馈修订与接口通知：PrimaryAction.ScreenPosition 只表示屏幕某位置发生主要操作；不表示移动、攻击或放置，也不要求 Actor 消费。Move / Attack 仅预留或调试，WASD 默认关闭。输入使用独立 InputEventChannel，Subscribe / Unsubscribe 改用 SlimeBattle.Player.Input.InputSubscriptionToken，与 Core.SubscriptionToken 不混用。显式旧 token 声明须迁移；默认、跨实例、重复解绑无效；null 处理器/销毁后订阅分别抛 ArgumentNullException / ObjectDisposedException。

模式切换、失焦和禁用始终发布生命周期取消；即使触点已松开，消费者仍须幂等清理待处理交互和测试标记。恢复要求控件先回中立状态。

## 8. 对外事件索引

| 事件 | 发布者 | 含义 | 主要观察者 |
| --- | --- | --- | --- |
| LevelStartedEvent | LevelManager | 关卡初始化完成并进入运行 | UI、表现、测试 |
| LevelPausedEvent | LevelManager | 关卡规则暂停 | UI、表现 |
| LevelSucceededEvent | Core 结果规则组件 | 关卡已确认成功 | LevelManager、UI、表现 |
| LevelFailedEvent | Core 结果规则组件 | 关卡已确认失败 | LevelManager、UI、表现 |
| SlimeSpawnedEvent | SlimeManager | 史莱姆已生成 | UI、表现、测试 |
| SlimeDiedEvent | SlimeManager | 史莱姆已确认死亡 | SlimeManager、UI、表现 |
| SlimeDepletedEvent | SlimeManager | 最后一只史莱姆已确认死亡 | Core 结果规则组件、UI、测试 |
| SlimeGroupCreatedEvent | SlimeManager | 群体已形成 | UI、表现、调试 |
| SlimeGroupDissolvedEvent | SlimeManager | 群体已解散 | UI、表现、调试 |
| EnemyDiedEvent | EnemyManager | 敌人已确认死亡 | UI、表现 |
| GuardDiedEvent | GuardManager | 守卫已确认死亡 | UI、表现 |
| MovementBlockedEvent | MovementSystem | 移动被空间规则阻止 | ActorStateMachine、AI、UI |
| MapReadyEvent | MapManager | 当前关卡地图资源和静态索引已完成绑定 | LevelContext、关卡服务（已发布） |
| MapAreaEnteredEvent | MapManager | Actor 进入一个道路/地形/区域标记体 | StatusManager、MovementSystem、AI |
| MapAreaExitedEvent | MapManager | Actor 离开一个道路/地形/区域标记体 | StatusManager、MovementSystem、AI |
| EffectAppliedEvent | StatusManager | 效果已应用 | Actor Manager、UI、表现 |
| EffectRejectedEvent | StatusManager | 效果请求被拒绝、免疫或重复提交 | Actor Manager、UI、调试 |
| HealthChangedEvent | StatusManager | Actor 生命已变化 | AI、Actor Manager、UI |
| StatusAddedEvent | StatusManager | 状态已添加 | AI、状态机、UI |
| StatusChangedEvent | StatusManager | 状态来源、时长或叠层已变化 | AI、状态机、UI |
| StatusRemovedEvent | StatusManager | 状态已移除 | AI、状态机、UI |
| StatusExpiredEvent | StatusManager | 状态因计时到期或区域宽限结束 | AI、状态机、UI |
| MovementModifierChangedEvent | StatusManager | Actor 移速或定身修正已变化 | MovementSystem、表现、UI |
| ActorControlChangedEvent | StatusManager | Actor 控制模式已变化 | Actor Manager、AI、表现 |
| PlacementConfirmedEvent | PlacementSystem | 放置已确认 | 机关、UI、表现 |
| PlacementReplacedEvent（候选） | PlacementSystem / MechanismSystem | 允许替换的 Anchor 已完成旧对象回收和新对象提交 | UI、表现、路线/陷阱规则 |
| MechanismTriggeredEvent | MechanismSystem | 机关触发规则已确认并产生结果 | Actor、UI、表现、测试 |
| WeaponDamagedEvent | MechanismSystem | 护送武器受到已结算效果 | GuardManager、UI、表现 |
| WeaponDestroyedEvent | MechanismSystem | 护送武器已被摧毁 | Core 结果规则组件、GuardManager、UI |
| WeaponReachedGoalEvent | MechanismSystem | 护送武器已抵达指定终点 | Core 结果规则组件、UI、表现 |
| SlimeReachedGoalEvent（候选） | SlimeManager / 终点规则适配 | 一只史莱姆已进入第一关终点洞穴 | Core 结果规则组件、UI、表现；第一关结果链路待实现 |
| LevelWaitWindowExpiredEvent（候选） | 关卡流程/结果规则适配 | 第一关等待窗口结束 | Core 结果规则组件、UI、测试；第一关结果链路待实现 |

## 9. 不进入全局表的内容

- DecisionTree 内部节点和感知实现；
- `GuidanceKind.Wander` 是 ActorDecisionTree 感知契约中的漫游引导类型，仅由 Level_test 用于移动链路验收；
- StateMachine 内部状态、转换和 AttackAction 私有步骤；
- Actor 的私有字段、Manager 的集合缓存和 Unity 组件引用；
- UI 控件刷新、动画、特效和音效播放方法；
- 只有一个系统内部调用方的辅助方法。


## 小宸首关接口通知（2026-10-11，待评审）

| 提供者 | 接口 | 调用方 / 边界 |
| --- | --- | --- |
| PlacementGridAuthoring | WorldToCell(Vector3) / GetCellCenter(Vector3Int) | PlacementSystem / 预览；场景矩形 XY Grid；普通格子坐标服务 |
| PlacementAnchorAuthoring（候选名称） | 只读提供 Anchor ID、位置、允许类型、替换策略及路线/陷阱关联事实 | PlacementSystem / MechanismSystem；不负责实例化、回收或触发规则；最终类型和查询接口待评审 |
| PlacementSystem | BeginPlacement(PlacementItemId) / PreviewPlacement(Vector3) / ConfirmPlacement() / CancelPlacement() / GetCurrentPreview() / GetBudgetSnapshot() | 放置适配与选择 UI；只报告结果，不生成机关 |
| IPlacementBackend | IsAvailable / RequestPlacement(PlacementRequest) / ReadPlacedObjects() / GetBudgetSnapshot() | PlacementSystem；由机关/关卡适配实现，负责最终请求与只读已放置对象 |
| IFirstLevelStatusSource | ReadSnapshot(LevelContext) | FirstLevelUiSnapshotSource；武器 / 结果只读显示，不决定结果 |
| UiState | 尾部可选参数 slimeLimit / weaponText | 保持旧构造调用兼容；未知值不显示成 0 |
| FirstLevelPresentationRoot | Initialize(LevelManager, InputSystem, CameraSystem, PlacementGridAuthoring, IPlacementBackend, PlacementConfirmationMode, IFirstLevelStatusSource = null) | 场景组合；本关依赖，禁用解绑，重开清理 |

机关创建、Anchor 替换、第一关史莱姆终点统计与新武器/Core 结果接口尚未完全提供，当前注入契约不替代对应所有者的正式 API。详细参数、失败与生命周期见小宸实现与接入通知；旧护送武器事件不改变最新第一关结果口径。

首关快照补充：PlayerInputSnapshot.PointerBlocked 为只读 UI 手势/恢复保护事实；末尾可选构造参数保持旧调用兼容。PlacementInputAdapter 仅在未屏蔽的 Gameplay 快照下更新预览。

返回值补充：`PreviewPlacement` → `PlacementValidationResult`（Preview / IsValid / Reason）；`ConfirmPlacement` → `PlacementResult`（Succeeded / MechanismId / Reason）。`PlacementSystem.Changed / PlacementCompleted` 为本地刷新／尝试结果事件，不是全局领域事实。backend 成功返回要求同步生效且 ID 有效，仅用于其注入时的当前关卡；新 Context 须重新注入。Anchor 替换必须能表达旧对象回收、新对象创建失败和最终占用结果；具体 payload 待评审。`IFirstLevelStatusSource` 返回 `FirstLevelStatusSnapshot`（结果文本和首关史莱姆终点统计；旧 WeaponText 仅兼容显示）。详细边界、降级与生命周期见实现与接入文档。

2026-10-12 交互修正：PlacementInputAdapter 在地图 PrimaryAction 后锁定目标世界格子，移动鼠标到 CONFIRM 或移动镜头不会改选，再次点击地图可替换。锁定不新增公开 API；直接调用 PlacementSystem.PreviewPlacement / ConfirmPlacement 的契约不变。

## 小宸首关当前接入补充（2026-10-16）

- 当前地图点击/轻触直接提交合法放置，取代上方历史的锁定/确认按钮交互；预览/确认方法仍分开，便于适配器和测试复用。
- `PlacementSystem` 构造参数尾部新增可选 `Func<IReadOnlyList<PlacementAnchorSnapshot>> readAnchors = null`、`float anchorPixelsPerWorldUnit = 0f`。旧无 Anchor 调用兼容；有 Anchor 必须显式提供同一地图定义的像素比例。
- 消费已合入的 `MapManager.GetPlacementAnchors()`；`PlacementPreview.Anchor` 携带原地图快照，`PlacementRequest.AnchorId` 为尾部可选参数。不重新定义地图 ID/角色/允许类型/替换事实。
- `PlacementValidationStatus` 追加 `AnchorFree / TypeMismatch / ReplacementDenied / ReplacementUnavailable`，保留旧枚举数值。确认重新读取快照；已占用且配置允许替换时，当前因缺少公开安全事务而拒绝，保留旧机关。
- `FirstLevelPresentationRoot.Initialize` 尾部可选 `FirstLevelMapDefinition anchorMapDefinition = null`，Prefab 显式引用 `MAP_FirstLevel`。重开默认使用新 Context 的正式 MechanismPlacementBackend；不复用旧注入后端。
- `IFirstLevelStatusSource` 已支持 GoalText / WaitingText / RouteHint；缺少上游路线提示时，HUD 仅显示当前 Anchor 的真实关联 ID，不推断路线状态。终点人数和窗口无真实来源时继续未接入。

以上是 Player/UI 本地兼容扩展及已有公开查询的接入通知；没有新增机关安全替换协议或候选终点事件实现，跨系统替换/结果契约仍待所属负责人提供。

## 小宸首关拆除接入补充（2026-10-19）

- `PlacementSystem.BeginRemoval()` / `ConfirmRemoval()` 进入持续拆除模式并提交回收；`PreviewPlacement()` 复用当前指针预览，`PlacementPreview.RemovalTarget` 表示活跃玩家机关 ID。确认时重查阶段、占用和目标身份。
- `IsRemoving` 表示拆除；`IsInteracting` 表示放置或拆除；`IsPlacing` 保持原语义。`CancelPlacement()` 同时取消两种模式，暂停、结算、重开和禁用沿用同一清理链路。
- 可选 `IPlacementRemovalBackend.SupportsRemoval / RequestRemoval(MechanismId)` 由两个正式 Placement 后端适配已有 `MechanismSystem.RequestRemove()`；未实现该接口的旧放置后端继续兼容。返回 `PlacementResult`；机关系统拥有对象回收、碰撞释放和配置冷却，UI 不直接销毁机关。
- `RemovalCompleted` 是本地尝试结果事件；资源返还与库存规则不在本次接口中新增。`ReferenceUiView.InitializePlacement()` / `ToggleRemoval()` 由当前关卡组合根接线，重开重新绑定新会话。

## 小宸首关开局放置可见性修正（2026-10-19）

- 实现事实：`FirstLevelPresentationRoot` 对每个新 `LevelContext` 初始化一次镜头焦点，优先读取护送武器、其次存活史莱姆的位置，无快照时使用地图中心；普通刷新与暂停恢复不重置玩家镜头。
- `PlacementSystem.IsCellPlaceable(Vector3Int)` 供 `PlacementGridView` 查询普通道路空格提示，读取当前放置模式、运行阶段、地图边界、网格中心、占用与既有 `canOccupy` 委托；未放置、非运行、越界、占用或查询异常返回 false。不改变预览或提交请求，最终确认仍由原有后端复核冷却和机关规则。
- `PlacementGridView.Initialize` 尾部新增可选 `UnityEngine.Camera sceneCamera = null`，正式首关注入镜头以仅绘制视野内的淡绿色道路空格，镜头拖动、缩放或宽高比变化时刷新；旧调用兼容。`VisiblePlaceableCellCount` 为当前可见提示数量，只读，取消/暂停/禁用时归零。
