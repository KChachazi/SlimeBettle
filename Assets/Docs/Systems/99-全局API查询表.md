# 99 全局 API 查询表

版本：0.4 草案
最后更新：2026-10-09
用途：协作时查询跨系统公共接口
状态：架构暂定

## 1. 使用规则

- 本表只记录跨系统公共接口，不记录系统内部方法、私有回调和具体节点实现；
- 完整职责、前置条件和失败语义以对应系统设计文档为准；
- 暂定接口可以在实现前调整，调用方不得把它当成不可变协议；
- 公共接口变化时，必须同步更新所属系统文档和本表；
- Request 表示请求执行，Query 或 Get/TryGet 表示读取，Event 表示结果已经发生；
- ActorDecisionTree、ActorStateMachine 和 AttackAction 的内部节点、状态和执行函数不进入全局表。

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
| API-CORE-008 | ObjectiveSystem | TryGetObjectiveSnapshot(ObjectiveId, out ObjectiveSnapshot) | 目标 ID | 是否成功、快照 | LevelManager、UI | 暂定 |
| API-CORE-009 | ObjectiveSystem | TryGetWeaponSnapshot(WeaponId, out WeaponSnapshot) | 武器 ID | 是否成功、快照 | GuardManager、UI、EnemyActor | 暂定 |
| API-CORE-010 | ObjectiveSystem | ApplyWeaponEffect(WeaponId, ResolvedEffect) | 已结算效果 | EffectApplyResult | StatusManager | 暂定 |
| API-CORE-011 | ObjectiveSystem | EvaluateResult() | 无 | ObjectiveResult | LevelManager、测试 | 成功条件待确认 |
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
| API-DATA-004 | ObjectiveConfig | FailWhenNoSlimes | 无 | 是否在史莱姆全灭时失败 | ObjectiveSystem | P0，默认开启；Level_test 关闭 |
| API-DATA-005 | ConfigSystem | TryGetActor(ConfigId, out ActorConfig) | 配置 ID | 是否成功、配置 | Actor Manager | 已实现 |
| API-DATA-006 | ConfigSystem | TryGetStatus(StatusId, out StatusConfig) | 状态 ID | 是否成功、配置 | StatusManager、MapManager 区域适配 | 已实现；重复 StatusId 拒绝 |

## 4. LevelContext Manager API

### 4.1 SlimeManager

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-LEVEL-001 | SlimeManager | SpawnSlime(SlimeSpawnRequest) | 生成请求 | ActorId 或拒绝原因 | LevelContext、测试 | 已确认 |
| API-LEVEL-002 | SlimeManager | TryGetSnapshot(ActorId, out SlimeSnapshot) | 史莱姆 ID | 是否成功、快照 | AI、UI、表现 | 已确认 |
| API-LEVEL-003 | SlimeManager | GetAliveSnapshots() | 无 | 史莱姆只读快照列表 | ObjectiveSystem、UI、AI | 已确认 |
| API-LEVEL-004 | SlimeManager | RequestDespawn(ActorId, DespawnReason) | ID、原因 | 是否成功 | LevelContext、测试 | 已确认 |
| API-LEVEL-005 | SlimeManager | TryGetGroupSnapshot(GroupId, out SlimeGroupSnapshot) | 群体 ID | 是否成功、快照 | UI、调试、Actor | 暂定 |
| API-LEVEL-014 | SlimeManager | TryCreateGroup(members, target, out GroupId) / TrySetGroupTarget(GroupId, target) / RequestDissolveGroup(GroupId) | 明确成员和目标 | 是否成功 | 关卡规则、测试 | P0 显式群体关系；自动规则待确认 |
| API-LEVEL-015 | SlimeManager | TrySetSpawnParent(Transform) | 场景 SlimeRoot | 是否成功；后续生成实例的父节点 | 关卡场景装配组件 | P0 |

### 4.2 EnemyManager

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-LEVEL-006 | EnemyManager | SpawnEnemy(EnemySpawnRequest) | 生成请求 | ActorId 或拒绝原因 | LevelContext、测试 | 暂定 |
| API-LEVEL-007 | EnemyManager | TryGetSnapshot(ActorId, out EnemySnapshot) | 敌人 ID | 是否成功、快照 | AI、UI、表现 | 暂定 |
| API-LEVEL-008 | EnemyManager | GetAliveSnapshots() | 无 | 敌人只读快照列表 | Actor、UI、ObjectiveSystem | 暂定 |
| API-LEVEL-009 | EnemyManager | RequestDespawn(ActorId, DespawnReason) | ID、原因 | 是否成功 | LevelContext、测试 | 暂定 |
| API-LEVEL-016 | EnemyManager | TrySetSpawnParent(Transform) | 场景 EnemyRoot | 是否成功；后续生成实例的父节点 | 关卡场景装配组件 | P0 |

### 4.3 GuardManager

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-LEVEL-010 | GuardManager | SpawnGuard(GuardSpawnRequest) | 生成请求 | ActorId 或拒绝原因 | LevelContext、测试 | 暂定 |
| API-LEVEL-011 | GuardManager | TryGetSnapshot(ActorId, out GuardSnapshot) | 守卫 ID | 是否成功、快照 | UI、AI、表现 | 暂定 |
| API-LEVEL-012 | GuardManager | GetAliveSnapshots() | 无 | 守卫只读快照列表 | UI、ObjectiveSystem | 暂定 |
| API-LEVEL-013 | GuardManager | SubmitCommand(ActorId, GuardCommand) | ID、语义指令 | 接受/拒绝 | InputSystem、调试工具 | 调试/待确认 |

Manager 不对外提供 ApplyEffect。生命和状态变化统一由 StatusManager 经过 LevelContext 内部 Actor 解析契约处理。

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-ACTOR-001 | SlimeActor / EnemyActor | NotifyMovementResult(Vector2) / NotifyMovementCompleted(string) | 已执行位置、当前移动请求 ID | 完成回执是否接受 | MovementSystem | P0 已接入 |

## 5. 地图与移动 API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-WORLD-001 | MapManager | TryGetMapBounds(out MapBounds) | 无 | 是否成功、地图边界 | CameraSystem、MovementSystem、AI | 已实现；场景需配置 MapBoundsAuthoring |
| API-WORLD-002 | MapManager | Query(Vector2) | 世界坐标 | MapSpatialSnapshot（道路、地形、区域和边界事实） | MovementSystem、AI、PlacementSystem、机关 | 已实现；区域需配置 MapAreaAuthoring |
| API-WORLD-003 | MapManager | CanOccupy(Vector2, OccupancyQuery) | 位置、边界/半径/静态阻挡条件 | 是否可占用 | MovementSystem、PlacementSystem、AI | 已实现静态地图查询；不含动态 Actor/放置物 |
| API-WORLD-004 | MapManager | GetMovementSpeedMultiplier(Vector2) | 世界坐标 | 地形移速修正（多个地形倍率相乘） | MovementSystem | 已实现；场景需配置地形区域 |
| API-WORLD-005 | MapManager | TryGetMapArea(MapAreaId, out MapAreaSnapshot) | 区域 ID | 是否成功、区域快照 | StatusManager、AI、关卡规则 | 已实现 |
| API-WORLD-006 | MapManager | TryGetMapPoint(MapPointId, out MapPointSnapshot) / GetMapPoints(MapPointKind) | 地图点 ID 或类型 | 是否成功、点位快照集合 | Actor Manager、ObjectiveSystem、未来的掉落/事件系统 | 已实现；场景需配置 MapPointAuthoring |
| API-WORLD-007 | MovementSystem | SubmitMovement(MovementRequest) | 移动请求 | MovementRequestId | ActorStateMachine、InputSystem | P0 已实现 |
| API-WORLD-008 | MovementSystem | CancelMovement(MovementRequestId, CancelReason) | 请求 ID、原因 | 是否取消 | ActorStateMachine、Manager | P0 已实现 |
| API-WORLD-009 | MovementSystem | TryGetMovementState(ActorId, out MovementState) | Actor ID | 是否成功、状态 | Actor、UI、调试 | P0 已实现 |
| API-WORLD-010 | MovementSystem | TryGetContactInfo(ActorId, out ContactInfo) | Actor ID | 是否成功、最近阻挡接触 | Actor、调试、UI | P0 已实现 |

## 6. StatusManager API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-STATUS-001 | StatusManager | ApplyEffect(EffectRequest) | 带唯一请求 ID 的来源、目标、效果 | EffectApplyResult | ActorAction、环境、玩法适配器 | 已实现；当前目标为 Actor |
| API-STATUS-002 | StatusManager | ApplyStatus(StatusApplicationRequest) | 状态申请 | 是否成功 | Actor、机关、环境 | 已实现 |
| API-STATUS-003 | StatusManager | RemoveStatus(ActorId, StatusId, StatusRemoveReason) | Actor、状态、原因 | 是否成功 | Actor、关卡规则 | 已实现 |
| API-STATUS-004 | StatusManager | TryGetHealthSnapshot(ActorId, out HealthSnapshot) | Actor ID | 是否成功、生命快照 | AI、UI、ObjectiveSystem | 已实现 |
| API-STATUS-005 | StatusManager | TryGetStatusSnapshot(ActorId, out StatusSnapshot) | Actor ID | 是否成功、状态快照 | AI、UI、状态机 | 已实现 |
| API-STATUS-006 | StatusManager | GetMovementModifiers(ActorId) | Actor ID | MovementModifierSnapshot | MovementSystem | 已实现 |
| API-STATUS-007 | StatusManager | GetAttackSpeedMultiplier(ActorId) | Actor ID | 攻击速度倍率 | ActorStateMachine 行动端口 | 已实现 |
| API-STATUS-008 | StatusManager | Tick(StatusTickContext) | 当前帧间隔 | 更新状态计时与周期效果 | LevelContext | 已实现 |

ActorDecisionTree 只查询状态，不通过 StatusManager 直接造成效果。AttackAction 在命中时机使用 API-STATUS-001。

## 7. 机关、输入、镜头和 UI API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-MECH-001 | MechanismSystem | NotifyContact(MechanismTriggerContext) | 接触事实 | 是否受理 | MovementSystem | 待决定 |
| API-MECH-002 | MechanismSystem | RequestActivate(MechanismId, MechanismActivationRequest) | 机关 ID、请求 | 激活结果 | PlacementSystem、测试 | 待决定 |
| API-MECH-003 | PassageSystem | RequestEnter(PassageAccessRequest) | 通行请求 | 通过/等待/拒绝 | ActorStateMachine、MovementSystem | 待决定 |
| API-MECH-004 | PassageSystem | NotifyExit(PassageId, ActorId) | 窄路、Actor ID | 是否释放 | MovementSystem、Manager | 待决定 |
| API-PLAYER-001 | InputSystem | ReadSnapshot() | 无 | PlayerInputSnapshot | CameraSystem、PlacementSystem、GuardManager | 已确认 |
| API-PLAYER-002 | InputSystem | SetInputMode(InputMode) | Disabled / Gameplay / UiOnly | void；非法值抛异常 | 场景接入、UI | P0 已实现 |
| API-PLAYER-003 | PlacementSystem | BeginPlacement(PlacementItemId) | 物品 ID | 开始结果 | UI、InputSystem | 待确认 |
| API-PLAYER-004 | PlacementSystem | PreviewPlacement(WorldPosition) | 世界位置 | 校验结果 | InputSystem、UI | 待确认 |
| API-PLAYER-005 | PlacementSystem | ConfirmPlacement() | 无 | PlacementResult | UI、InputSystem | 待确认 |
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
| LevelSucceededEvent | LevelManager | 关卡已确认成功 | UI、表现 |
| LevelFailedEvent | LevelManager | 关卡已确认失败 | UI、表现 |
| SlimeSpawnedEvent | SlimeManager | 史莱姆已生成 | UI、表现、测试 |
| SlimeDiedEvent | SlimeManager | 史莱姆已确认死亡 | ObjectiveSystem、UI、表现 |
| SlimeGroupCreatedEvent | SlimeManager | 群体已形成 | UI、表现、调试 |
| SlimeGroupDissolvedEvent | SlimeManager | 群体已解散 | UI、表现、调试 |
| EnemyDiedEvent | EnemyManager | 敌人已确认死亡 | ObjectiveSystem、UI、表现 |
| GuardDiedEvent | GuardManager | 守卫已确认死亡 | ObjectiveSystem、UI、表现 |
| MovementBlockedEvent | MovementSystem | 移动被空间规则阻止 | ActorStateMachine、AI、UI |
| MapReadyEvent | MapManager | 当前关卡地图资源和静态索引已完成绑定 | LevelContext、关卡服务（已发布） |
| MapAreaEnteredEvent | MapManager | Actor 进入一个道路/地形/区域标记体 | StatusManager、MovementSystem、AI |
| MapAreaExitedEvent | MapManager | Actor 离开一个道路/地形/区域标记体 | StatusManager、MovementSystem、AI |
| EffectAppliedEvent | StatusManager | 效果已应用 | Actor Manager、UI、表现 |
| EffectRejectedEvent | StatusManager | 效果请求被拒绝、免疫或重复提交 | Actor Manager、UI、调试 |
| HealthChangedEvent | StatusManager | Actor 生命已变化 | AI、ObjectiveSystem、UI |
| StatusAddedEvent | StatusManager | 状态已添加 | AI、状态机、UI |
| StatusChangedEvent | StatusManager | 状态来源、时长或叠层已变化 | AI、状态机、UI |
| StatusRemovedEvent | StatusManager | 状态已移除 | AI、状态机、UI |
| StatusExpiredEvent | StatusManager | 状态因计时到期或区域宽限结束 | AI、状态机、UI |
| MovementModifierChangedEvent | StatusManager | Actor 移速或定身修正已变化 | MovementSystem、表现、UI |
| ActorControlChangedEvent | StatusManager | Actor 控制模式已变化 | Actor Manager、AI、表现 |
| PlacementConfirmedEvent | PlacementSystem | 放置已确认 | 机关、UI、表现 |
| WeaponDamagedEvent | ObjectiveSystem | 护送武器受到已结算效果 | GuardManager、UI、表现 |
| WeaponDestroyedEvent | ObjectiveSystem | 护送武器已被摧毁 | LevelManager、GuardManager、UI |
| ObjectiveFailedEvent | ObjectiveSystem | 当前目标已确认失败 | LevelManager、UI、表现 |

## 9. 不进入全局表的内容

- DecisionTree 内部节点和感知实现；
- `GuidanceKind.Wander` 是 ActorDecisionTree 感知契约中的漫游引导类型，仅由 Level_test 用于移动链路验收；
- StateMachine 内部状态、转换和 AttackAction 私有步骤；
- Actor 的私有字段、Manager 的集合缓存和 Unity 组件引用；
- UI 控件刷新、动画、特效和音效播放方法；
- 只有一个系统内部调用方的辅助方法。

