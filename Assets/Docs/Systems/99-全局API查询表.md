# 99 全局 API 查询表

版本：0.4 草案
最后更新：2026-10-08
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

## 3. 配置 API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-DATA-001 | ConfigSystem | TryGet<TConfig>(ConfigId, out TConfig) | 配置 ID | 是否成功、配置 | 关卡和玩法系统 | 已确认 |
| API-DATA-002 | ConfigSystem | TryGetLevel(LevelId, out LevelConfig) | 关卡 ID | 是否成功、配置 | LevelManager、MapManager | 已确认 |
| API-DATA-003 | ConfigSystem | ValidateAll() | 无 | 校验报告 | 编辑器工具、启动检查 | 已确认 |
| API-DATA-004 | ConfigSystem | TryGetActor(ConfigId, out ActorConfig) | 配置 ID | 是否成功、配置 | Actor Manager | 暂定 |
| API-DATA-005 | ConfigSystem | TryGetStatus(StatusId, out StatusConfig) | 状态 ID | 是否成功、配置 | StatusManager | 暂定 |

## 4. LevelContext Manager API

### 4.1 SlimeManager

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-LEVEL-001 | SlimeManager | SpawnSlime(SlimeSpawnRequest) | 生成请求 | ActorId 或拒绝原因 | LevelContext、测试 | 已确认 |
| API-LEVEL-002 | SlimeManager | TryGetSnapshot(ActorId, out SlimeSnapshot) | 史莱姆 ID | 是否成功、快照 | AI、UI、表现 | 已确认 |
| API-LEVEL-003 | SlimeManager | GetAliveSnapshots() | 无 | 史莱姆只读快照列表 | ObjectiveSystem、UI、AI | 已确认 |
| API-LEVEL-004 | SlimeManager | RequestDespawn(ActorId, DespawnReason) | ID、原因 | 是否成功 | LevelContext、测试 | 已确认 |
| API-LEVEL-005 | SlimeManager | TryGetGroupSnapshot(GroupId, out SlimeGroupSnapshot) | 群体 ID | 是否成功、快照 | UI、调试、Actor | 暂定 |

### 4.2 EnemyManager

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-LEVEL-006 | EnemyManager | SpawnEnemy(EnemySpawnRequest) | 生成请求 | ActorId 或拒绝原因 | LevelContext、测试 | 暂定 |
| API-LEVEL-007 | EnemyManager | TryGetSnapshot(ActorId, out EnemySnapshot) | 敌人 ID | 是否成功、快照 | AI、UI、表现 | 暂定 |
| API-LEVEL-008 | EnemyManager | GetAliveSnapshots() | 无 | 敌人只读快照列表 | Actor、UI、ObjectiveSystem | 暂定 |
| API-LEVEL-009 | EnemyManager | RequestDespawn(ActorId, DespawnReason) | ID、原因 | 是否成功 | LevelContext、测试 | 暂定 |

### 4.3 GuardManager

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-LEVEL-010 | GuardManager | SpawnGuard(GuardSpawnRequest) | 生成请求 | ActorId 或拒绝原因 | LevelContext、测试 | 暂定 |
| API-LEVEL-011 | GuardManager | TryGetSnapshot(ActorId, out GuardSnapshot) | 守卫 ID | 是否成功、快照 | UI、AI、表现 | 暂定 |
| API-LEVEL-012 | GuardManager | GetAliveSnapshots() | 无 | 守卫只读快照列表 | UI、ObjectiveSystem | 暂定 |
| API-LEVEL-013 | GuardManager | SubmitCommand(ActorId, GuardCommand) | ID、语义指令 | 接受/拒绝 | InputSystem、调试工具 | 调试/待确认 |

Manager 不对外提供 ApplyEffect。生命和状态变化统一由 StatusManager 经过 LevelContext 内部 Actor 解析契约处理。

## 5. 地图与移动 API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-WORLD-001 | MapManager | GetMapBounds() | 无 | MapBounds | CameraSystem、MovementSystem、AI | 已确认 |
| API-WORLD-002 | MapManager | CanOccupy(WorldPosition, OccupancyQuery) | 位置、条件 | 是否可占用 | MovementSystem、PlacementSystem、AI | 已确认 |
| API-WORLD-003 | MapManager | QueryCell(WorldPosition) | 世界位置 | MapCellInfo | MovementSystem、AI、机关 | 已确认 |
| API-WORLD-004 | MapManager | TryGetSpawnPoint(SpawnPointId, out WorldPosition) | 出生点 ID | 是否成功、位置 | 各 Manager | 已确认 |
| API-WORLD-005 | MapManager | TryGetTargetPoint(TargetPointId, out WorldPosition) | 目标点 ID | 是否成功、位置 | Actor、ObjectiveSystem | 暂定 |
| API-WORLD-006 | MapManager | TryGetWeaponPoint(WeaponPointId, out WorldPosition) | 武器点 ID | 是否成功、位置 | ObjectiveSystem、GuardActor | 暂定 |
| API-WORLD-007 | MovementSystem | SubmitMovement(MovementRequest) | 移动请求 | MovementRequestId | ActorStateMachine、InputSystem | 已确认 |
| API-WORLD-008 | MovementSystem | CancelMovement(MovementRequestId, CancelReason) | 请求 ID、原因 | 是否取消 | ActorStateMachine、Manager | 已确认 |
| API-WORLD-009 | MovementSystem | TryGetMovementState(ActorId, out MovementState) | Actor ID | 是否成功、状态 | Actor、UI、调试 | 暂定 |

## 6. StatusManager API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-STATUS-001 | StatusManager | ApplyEffect(EffectRequest) | 来源、目标、效果 | EffectApplyResult | ActorAction、机关、环境、ObjectiveSystem | 已确认 |
| API-STATUS-002 | StatusManager | ApplyStatus(StatusApplicationRequest) | 状态申请 | 是否成功 | Actor、机关、环境 | 已确认 |
| API-STATUS-003 | StatusManager | RemoveStatus(ActorId, StatusId, StatusRemoveReason) | Actor、状态、原因 | 是否成功 | Actor、关卡规则 | 暂定 |
| API-STATUS-004 | StatusManager | TryGetHealthSnapshot(ActorId, out HealthSnapshot) | Actor ID | 是否成功、生命快照 | AI、UI、ObjectiveSystem | 已确认 |
| API-STATUS-005 | StatusManager | TryGetStatusSnapshot(ActorId, out StatusSnapshot) | Actor ID | 是否成功、状态快照 | AI、UI、状态机 | 已确认 |
| API-STATUS-006 | StatusManager | GetMovementModifiers(ActorId) | Actor ID | MovementModifierSnapshot | MovementSystem | 已确认 |

ActorDecisionTree 只查询状态，不通过 StatusManager 直接造成效果。AttackAction 在命中时机使用 API-STATUS-001。

## 7. 机关、输入、镜头和 UI API

| API 编号 | 提供者 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-MECH-001 | MechanismSystem | NotifyContact(MechanismTriggerContext) | 接触事实 | 是否受理 | MovementSystem | 待决定 |
| API-MECH-002 | MechanismSystem | RequestActivate(MechanismId, MechanismActivationRequest) | 机关 ID、请求 | 激活结果 | PlacementSystem、测试 | 待决定 |
| API-MECH-003 | PassageSystem | RequestEnter(PassageAccessRequest) | 通行请求 | 通过/等待/拒绝 | ActorStateMachine、MovementSystem | 待决定 |
| API-MECH-004 | PassageSystem | NotifyExit(PassageId, ActorId) | 窄路、Actor ID | 是否释放 | MovementSystem、Manager | 待决定 |
| API-PLAYER-001 | InputSystem | ReadSnapshot() | 无 | PlayerInputSnapshot | CameraSystem、PlacementSystem、GuardManager | 已确认 |
| API-PLAYER-002 | InputSystem | SetInputMode(InputMode) | 输入模式 | 切换结果 | UI、LevelManager | 已确认 |
| API-PLAYER-003 | PlacementSystem | BeginPlacement(PlacementItemId) | 物品 ID | 开始结果 | UI、InputSystem | 待确认 |
| API-PLAYER-004 | PlacementSystem | PreviewPlacement(WorldPosition) | 世界位置 | 校验结果 | InputSystem、UI | 待确认 |
| API-PLAYER-005 | PlacementSystem | ConfirmPlacement() | 无 | PlacementResult | UI、InputSystem | 待确认 |
| API-PRESENT-001 | CameraSystem | ScreenToWorld(ScreenPosition) | 屏幕位置 | 世界位置 | PlacementSystem、InputSystem | 已确认 |
| API-PRESENT-002 | CameraSystem | SetBounds(CameraBounds) | 镜头边界 | 无 | MapManager、LevelContext | 已确认 |
| API-UI-001 | UISystem | Refresh(UiState) | UI 状态 | 无 | UI 内部、状态同步 | 已确认 |
| API-UI-002 | UISystem | SubmitCommand(UiCommand) | UI 指令 | 接受/拒绝 | 按钮、面板 | 已确认 |

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
| EffectAppliedEvent | StatusManager | 效果已应用 | Actor Manager、UI、表现 |
| HealthChangedEvent | StatusManager | Actor 生命已变化 | AI、ObjectiveSystem、UI |
| StatusAddedEvent | StatusManager | 状态已添加 | AI、状态机、UI |
| StatusRemovedEvent | StatusManager | 状态已移除 | AI、状态机、UI |
| PlacementConfirmedEvent | PlacementSystem | 放置已确认 | 机关、UI、表现 |
| WeaponDamagedEvent | ObjectiveSystem | 护送武器受到已结算效果 | GuardManager、UI、表现 |
| WeaponDestroyedEvent | ObjectiveSystem | 护送武器已被摧毁 | LevelManager、GuardManager、UI |
| ObjectiveFailedEvent | ObjectiveSystem | 当前目标已确认失败 | LevelManager、UI、表现 |

## 9. 不进入全局表的内容

- DecisionTree 内部节点和感知实现；
- StateMachine 内部状态、转换和 AttackAction 私有步骤；
- Actor 的私有字段、Manager 的集合缓存和 Unity 组件引用；
- UI 控件刷新、动画、特效和音效播放方法；
- 只有一个系统内部调用方的辅助方法。

