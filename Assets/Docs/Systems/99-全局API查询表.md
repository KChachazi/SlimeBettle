# 99 全局 API 查询表

版本：0.2 草案  
最后更新：2026-10-08  
用途：协作时快速查询跨系统公共接口  
状态：架构暂定

## 1. 使用规则

- 本表只记录跨系统公共接口，不记录系统内部方法、私有回调和具体节点实现。
- 每个接口的完整职责、前置条件和失败语义，以对应的系统设计文档为准。
- 接口状态为“暂定”时，可以在实现前调整；调用方不得把暂定接口当作不可变协议。
- 公共接口发生变化时，必须同时更新对应系统文档和本表。
- `Request` 表示请求执行，`Query` 或 `Get/TryGet` 表示读取，`Event` 表示结果已经发生。

## 2. API 总表

| API 编号 | 提供系统 | 接口 | 输入 | 输出 | 主要调用方 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| API-CORE-001 | `GameRoot` | `InitializeApplication()` | 无 | 应用初始化结果/事件 | 启动入口 | 已确认 |
| API-CORE-002 | `GameRoot` | `RequestEnterLevel(LevelId)` | 关卡 ID | 请求结果/关卡事件 | 主菜单、启动入口 | 暂定 |
| API-CORE-003 | `LevelManager` | `RequestLoadLevel(LevelId)` | 关卡 ID | `LevelLoadStartedEvent`、`LevelStartedEvent` | `GameRoot`、UI | 已确认 |
| API-CORE-004 | `LevelManager` | `RequestRestartCurrentLevel()` | 无 | `LevelRestartedEvent` | UI、调试工具 | 已确认 |
| API-CORE-005 | `LevelManager` | `RequestPause()` / `RequestResume()` | 无 | 暂停/恢复事件 | UI、系统流程 | 已确认 |
| API-CORE-006 | `LevelManager` | `GetLevelState()` | 无 | `LevelState` | UI、调试、系统 | 已确认 |
| API-CORE-007 | `LevelContext` | `GetEventBus()` | 无 | 当前关卡 EventBus | 当前关卡系统 | 已确认 |
| API-CORE-008 | `ObjectiveSystem` | `TryGetObjectiveSnapshot(ObjectiveId, out ObjectiveSnapshot)` | 目标 ID | 是否成功、目标快照 | `LevelManager`、UI、调试 | 暂定 |
| API-CORE-009 | `ObjectiveSystem` | `TryGetWeaponSnapshot(WeaponId, out WeaponSnapshot)` | 武器 ID | 是否成功、武器快照 | `GuardSystem`、UI、敌人逻辑 | 暂定 |
| API-CORE-010 | `ObjectiveSystem` | `ApplyWeaponEffect(WeaponId, ResolvedEffect)` | 已结算效果 | `EffectApplyResult` | `StatusManager` | 暂定 |
| API-CORE-011 | `ObjectiveSystem` | `EvaluateResult()` | 无 | `ObjectiveResult` | `LevelManager`、测试 | 暂定 |
| API-DATA-001 | `ConfigSystem` | `TryGet<TConfig>(ConfigId, out TConfig)` | 配置 ID | 是否成功、配置 | 关卡和玩法系统 | 已确认 |
| API-DATA-002 | `ConfigSystem` | `TryGetLevel(LevelId, out LevelConfig)` | 关卡 ID | 是否成功、关卡配置 | `LevelManager`、`MapSystem` | 已确认 |
| API-DATA-003 | `ConfigSystem` | `ValidateAll()` | 无 | 校验报告 | 编辑器工具、启动检查 | 暂定 |
| API-DATA-004 | `ConfigSystem` | `TryGetObjective(ObjectiveId, out ObjectiveConfig)` | 目标 ID | 是否成功、目标配置 | `ObjectiveSystem`、`LevelManager` | 暂定 |
| API-DATA-005 | `ConfigSystem` | `TryGetWeapon(ConfigId, out WeaponConfig)` | 配置 ID | 是否成功、武器配置 | `ObjectiveSystem` | 暂定 |
| API-ACTOR-001 | `SlimeSystem` | `SpawnSlime(SlimeSpawnRequest)` | 生成请求 | `ActorId` | 关卡、生成点、测试 | 已确认 |
| API-ACTOR-002 | `SlimeSystem` | `TryGetSnapshot(ActorId, out SlimeSnapshot)` | 史莱姆 ID | 是否成功、快照 | AI、UI、表现 | 已确认 |
| API-ACTOR-003 | `SlimeSystem` | `ApplyEffect(ActorId, ResolvedEffect)` | 已结算效果 | `EffectApplyResult` | `StatusManager` | 已确认 |
| API-ACTOR-009 | `SlimeGroupSystem` | `TryJoinGroup(ActorId, SlimeGroupJoinRequest, out GroupId)` | 史莱姆 ID、加入请求 | 是否成功、群体 ID | `SlimeSystem`、AI | 暂定 |
| API-ACTOR-010 | `SlimeGroupSystem` | `LeaveGroup(ActorId, GroupDissolveReason)` | 史莱姆 ID、原因 | 无 | `SlimeSystem`、AI | 暂定 |
| API-ACTOR-011 | `SlimeGroupSystem` | `TryGetSnapshot(GroupId, out SlimeGroupSnapshot)` | 群体 ID | 是否成功、群体快照 | UI、表现、调试 | 暂定 |
| API-ACTOR-004 | `EnemySystem` | `SpawnEnemy(EnemySpawnRequest)` | 生成请求 | `ActorId` | 关卡、波次逻辑 | 暂定 |
| API-ACTOR-005 | `EnemySystem` | `TryGetSnapshot(ActorId, out EnemySnapshot)` | 敌人 ID | 是否成功、快照 | AI、战斗、UI | 暂定 |
| API-ACTOR-006 | `EnemySystem` | `ApplyEffect(ActorId, ResolvedEffect)` | 已结算效果 | `EffectApplyResult` | `StatusManager` | 暂定 |
| API-ACTOR-007 | `GuardSystem` | `SubmitCommand(ActorId, GuardCommand)` | 守卫指令 | 接受/拒绝 | 调试工具或未来输入 | 调试/待确认 |
| API-ACTOR-008 | `GuardSystem` | `ApplyEffect(ActorId, ResolvedEffect)` | 已结算效果 | `EffectApplyResult` | `StatusManager` | 待玩法确认 |
| API-WORLD-001 | `MapSystem` | `GetMapBounds()` | 无 | `MapBounds` | 镜头、放置、AI | 已确认 |
| API-WORLD-002 | `MapSystem` | `CanOccupy(WorldPosition, OccupancyQuery)` | 位置、查询条件 | 是否可占用 | 移动、放置、AI | 已确认 |
| API-WORLD-003 | `MapSystem` | `QueryCell(WorldPosition)` | 世界位置 | `MapCellInfo` | 移动、AI、机关 | 已确认 |
| API-WORLD-004 | `MapSystem` | `TryGetSpawnPoint(SpawnPointId, out WorldPosition)` | 出生点 ID | 是否成功、位置 | 角色系统 | 已确认 |
| API-WORLD-005 | `MovementSystem` | `SubmitMovement(MovementRequest)` | 移动请求 | 请求 ID | 状态机、守卫系统 | 已确认 |
| API-WORLD-006 | `MovementSystem` | `CancelMovement(MovementRequestId, CancelReason)` | 请求 ID、原因 | 是否取消 | 状态机、角色系统 | 已确认 |
| API-WORLD-007 | `MapSystem` | `TryGetWeaponPoint(WeaponPointId, out WorldPosition)` | 武器点 ID | 是否成功、位置 | `ObjectiveSystem`、守卫 | 暂定 |
| API-MECH-001 | `MechanismSystem` | `NotifyContact(MechanismTriggerContext)` | 接触事实 | 是否受理 | 移动系统 | 已确认 |
| API-MECH-002 | `MechanismSystem` | `RequestActivate(MechanismId, MechanismActivationRequest)` | 机关 ID、请求 | 激活结果 | 放置、测试、关卡 | 暂定 |
| API-MECH-003 | `PassageSystem` | `RequestEnter(PassageAccessRequest)` | 通行请求 | 通过/等待/拒绝 | 状态机、移动系统 | 待玩法确认 |
| API-MECH-004 | `PassageSystem` | `NotifyExit(PassageId, ActorId)` | 窄路、角色 ID | 是否释放 | 移动、角色系统 | 待玩法确认 |
| API-COMBAT-001 | `StatusManager` | `SubmitEffect(EffectRequest)` | 效果请求 | 效果请求 ID | 战斗、机关、关卡 | 已确认 |
| API-COMBAT-002 | `StatusManager` | `ApplyStatus(StatusApplicationRequest)` | 状态申请 | 是否成功 | 战斗、机关、角色 | 已确认 |
| API-COMBAT-003 | `StatusManager` | `TryGetStatusSnapshot(ActorId, out StatusSnapshot)` | 角色 ID | 是否成功、状态快照 | AI、UI、状态机 | 已确认 |
| API-COMBAT-004 | `CombatSystem` | `SubmitAttack(AttackRequest)` | 攻击请求 | `AttackId` | 角色 AI、守卫控制 | 待玩法确认 |
| API-PLAYER-001 | `InputSystem` | `ReadSnapshot()` | 无 | `PlayerInputSnapshot` | 镜头、放置、守卫 | 已确认 |
| API-PLAYER-002 | `InputSystem` | `SetInputMode(InputMode)` | 输入模式 | 无/切换结果 | UI、关卡流程 | 已确认 |
| API-PLAYER-003 | `PlacementSystem` | `BeginPlacement(PlacementItemId)` | 物品 ID | 开始事件 | UI、输入 | 待玩法确认 |
| API-PLAYER-004 | `PlacementSystem` | `PreviewPlacement(WorldPosition)` | 世界位置 | 校验结果 | 输入、UI | 待玩法确认 |
| API-PLAYER-005 | `PlacementSystem` | `ConfirmPlacement()` | 无 | `PlacementResult` | UI、输入 | 待玩法确认 |
| API-PLAYER-006 | `PlacementSystem` | `GetBudgetSnapshot()` | 无 | `PlacementBudgetSnapshot` | UI、放置预览、调试 | 暂定 |
| API-PRESENT-001 | `CameraSystem` | `ScreenToWorld(ScreenPosition)` | 屏幕位置 | 世界位置 | 放置、输入 | 已确认 |
| API-PRESENT-002 | `CameraSystem` | `SetBounds(CameraBounds)` | 镜头边界 | 无 | `MapSystem`、关卡 | 已确认 |
| API-UI-001 | `UISystem` | `Refresh(UiState)` | UI 状态 | 无 | UI 内部、状态同步 | 已确认 |
| API-UI-002 | `UISystem` | `SubmitCommand(UiCommand)` | UI 指令 | 接受/拒绝 | 按钮、面板 | 已确认 |

## 3. 对外事件索引

| 事件 | 发布系统 | 事件含义 | 主要观察者 |
| --- | --- | --- | --- |
| `LevelStartedEvent` | `LevelManager` | 关卡初始化完成并进入运行 | UI、表现、测试 |
| `LevelPausedEvent` | `LevelManager` | 关卡规则暂停 | UI、表现 |
| `LevelSucceededEvent` | `LevelManager` | 关卡已达成胜利结果 | UI、表现 |
| `LevelFailedEvent` | `LevelManager` | 关卡已达成失败结果 | UI、表现 |
| `SlimeSpawnedEvent` | `SlimeSystem` | 史莱姆已生成 | UI、表现、测试 |
| `SlimeDiedEvent` | `SlimeSystem` | 史莱姆已死亡 | 关卡、UI、表现 |
| `EnemyDiedEvent` | `EnemySystem` | 敌人已死亡 | 战斗、UI、表现 |
| `GuardDiedEvent` | `GuardSystem` | 守卫已死亡 | 关卡、UI、表现 |
| `SlimeGroupCreatedEvent` | `SlimeGroupSystem` | 史莱姆群体已形成 | UI、表现、调试 |
| `SlimeGroupDissolvedEvent` | `SlimeGroupSystem` | 史莱姆群体已解散 | UI、表现、调试 |
| `MovementBlockedEvent` | `MovementSystem` | 移动被空间规则阻止 | 状态机、AI、机关 |
| `MechanismTriggeredEvent` | `MechanismSystem` | 机关规则已触发 | `StatusManager`、UI、表现 |
| `PassageEntryQueuedEvent` | `PassageSystem` | 角色进入窄路等待队列 | AI、UI、表现 |
| `EffectAppliedEvent` | `StatusManager` | 效果已经应用 | 角色系统、UI、表现 |
| `StatusAddedEvent` | `StatusManager` | 持续状态已添加 | AI、状态机、UI |
| `AttackHitEvent` | `CombatSystem` | 攻击已命中 | `StatusManager`、表现 |
| `PlacementConfirmedEvent` | `PlacementSystem` | 放置已经确认 | 机关、UI、表现 |
| `WeaponDamagedEvent` | `ObjectiveSystem` | 护送武器已受到结算后的伤害 | `GuardSystem`、UI、表现 |
| `WeaponDestroyedEvent` | `ObjectiveSystem` | 护送武器已被摧毁 | `LevelManager`、Guard、UI |
| `ObjectiveFailedEvent` | `ObjectiveSystem` | 当前关卡目标已确认失败 | `LevelManager`、UI、表现 |

## 4. 当前不进入全局 API 表的内容

- 决策树内部节点、状态机内部状态和转换；
- UI 控件的私有刷新方法；
- 具体动画、特效和音效播放方法；
- 系统内部缓存和 Unity 组件引用；
- 只有一个系统内部调用方的辅助方法。

这些内容应记录在所属系统文档或代码中，避免全局表变成所有方法的目录。

