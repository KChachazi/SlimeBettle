# 18 StatusManager 生命与状态管理器设计

系统编号：STATUS-01
系统名称：StatusManager
默认命名空间：SlimeBattle.Status
建议实现目录：Assets/Game/Scripts/Status/
生命周期：当前 LevelContext 生命周期
状态：核心架构，状态优先级和数值待确认

## 1. 定位

StatusManager 是当前关卡统一的效果与状态入口，负责生命值变化、治疗、状态效果和移动相关 Buff/Debuff 的规则结算。

Actor 持有自己的生命和状态运行时数据；StatusManager 持有统一的修改规则、持续时间、叠加和优先级处理，不保存一份独立的生命可写副本。

## 2. 职责

- 接收来自 Actor Action、机关、环境和目标规则的 EffectRequest；
- 结算伤害、治疗、生命变化、击退、易伤、控制和持续状态；
- 管理移速、攻速、定身、冻结等影响行动的状态修正；
- 向 MovementSystem 提供某个 Actor 的移动修正快照；
- 校验来源、目标、存活、阵营关系和效果时机；
- 消费 MapManager 发布的区域进入/离开事实，根据区域类型和配置申请寒冷、炙热等状态；
- 维护状态持续时间、叠加、互斥、抵抗和过期；
- 发布效果和状态结果事件。

## 3. 不负责的内容

- 不选择攻击目标；
- 不判断角色是否应该发动攻击；
- 不执行命中检测、攻击范围查询和攻击前摇；
- 不直接操作 Actor Transform；
- 不播放动画、特效、音效和 UI；
- 不把状态结果再转发给另一个战斗系统。

当前不建立 CombatSystem。攻击由 ActorDecisionTree 产生意图，ActorStateMachine/AttackAction 在命中时机调用 StatusManager。

## 4. 核心数据

    EffectRequest
    ResolvedEffect
    EffectType
    StatusId
    StatusApplicationRequest
    StatusRuntimeData
    StatusSnapshot
    HealthSnapshot
    MovementModifierSnapshot
    EffectApplyResult

环境区域只提供触发事实和区域配置引用。区域成员关系由 MapManager 根据 Actor 的实际位置维护；StatusManager 自己拥有环境状态、互斥、持续时间和离开区域后的延迟解除计时。

EffectRequest 至少包含 SourceActorId、TargetActorId、EffectType、基础数值、持续时间或方向、来源上下文和请求轮次。

## 5. 必须接口

    EffectApplyResult ApplyEffect(EffectRequest request);
    bool ApplyStatus(StatusApplicationRequest request);
    bool RemoveStatus(ActorId actorId, StatusId statusId, StatusRemoveReason reason);
    bool TryGetHealthSnapshot(ActorId actorId, out HealthSnapshot snapshot);
    bool TryGetStatusSnapshot(ActorId actorId, out StatusSnapshot snapshot);
    MovementModifierSnapshot GetMovementModifiers(ActorId actorId);
    void Tick(StatusTickContext context);

ApplyEffect 是同步规则入口，返回值必须能区分已应用、被拒绝、目标失效和被免疫。持续状态在后续 Tick 中继续产生结果，但一次请求不能被重复结算。

## 6. 推荐数据流

### 6.1 攻击、治疗和自残

    Actor A / Ability / 机关
      → EffectRequest(Target = A 或 B)
      → StatusManager.ApplyEffect
      → 目标 Actor 的生命或状态变化
      → EffectApplied / HealthChanged / ActorDied

这里传递的是“效果请求”，不是把 A 的生命值直接传给 B。治疗、自残和普通伤害都只是不同的 SourceId、TargetId 和 EffectType 组合。

### 6.2 移动状态修正

    StatusManager 保存减速、加速、冻结等状态
      → MovementSystem 查询 GetMovementModifiers
      → MovementSystem 执行实际移动

StatusManager 可以发布 MovementModifierChangedEvent 让 MovementSystem 失效缓存，但不能直接修改位置。

### 6.3 地图环境状态

    MovementSystem 同步 Actor 的刚体实际位置
      → MapManager 发布 MapAreaEnteredEvent / MapAreaExitedEvent
      → StatusManager 根据 MapAreaSnapshot 和状态配置申请状态/效果
      → StatusManager 维护持续时间、互斥与解除条件

低温和温暖区域属于地图区域事实；MapManager 不直接调用 StatusManager，也不把状态效果写进 Actor。区域进入/离开事件只在成员关系改变时发布。图示和正文提出离开低温/炎热区域 20 秒后解除、靠近反向温度源可解除等规则，计时和来源优先级由 StatusManager 统一处理；具体互斥和重叠区域规则仍待确认。

## 7. 当前状态登记

饥饿、暴躁、害怕、寒冷、炙热、冻结、燃烧和暴击等状态继续保留为策划输入。具体阈值、倍率、互斥关系、持续伤害与移动修正的优先级，必须通过配置和统一结算规则表达，不能散落在 SlimeActor、EnemyActor 或机关脚本中。

## 8. 对外事件

    EffectAppliedEvent
    EffectRejectedEvent
    HealthChangedEvent
    StatusAddedEvent
    StatusRemovedEvent
    StatusExpiredEvent
    MovementModifierChangedEvent
    ActorControlChangedEvent
    MapAreaEnteredEvent / MapAreaExitedEvent（MapManager 发布，StatusManager 消费）

ActorDiedEvent 由目标 Actor 所属 Manager 在确认死亡后发布；StatusManager 可以发布已确认的致死效果结果，但不能代替 Manager 管理 Actor 生命周期。

## 9. 依赖

- LevelContext 的 Actor 解析契约，用于定位目标 Actor；
- ConfigSystem 提供状态、效果和抗性配置；
- ActorCore 接收已经结算的效果并更新自身数据；
- MovementSystem 消费移动修正和控制结果；
- MapManager 发布 Actor 进入/离开地图区域的事实，区域类型关联的效果由本系统配置解析；
- SlimeManager、EnemyManager、GuardManager 接收死亡和状态变化事实。

## 10. 关键不变量

1. 所有生命和状态修改必须经过 StatusManager。
2. StatusManager 不保存 Actor 生命的第二份可写副本。
3. 目标死亡、销毁、暂停或重开后，旧效果不能继续生效。
4. 同一 EffectRequest 不能被多个系统重复结算。
5. 移动 Buff 只能改变 MovementSystem 的计算输入，不能绕过移动系统改变位置。

## 11. 验收场景

- A 对 B 施加伤害，B 生命下降且只产生一次 EffectAppliedEvent；
- A 对自身施加治疗或伤害，TargetActorId 为 A 时流程仍然成立；
- 机关对 B 施加效果时不需要经过攻击系统；
- 减速状态生效后 MovementSystem 查询到新的移动修正；
- Actor 进入低温/温暖区域时，本系统消费一次 MapAreaEnteredEvent 并按配置申请状态；离开区域后的解除计时由本系统持有；
- 目标死亡后再次提交同一旧效果会被拒绝；
- 重开关卡后所有持续状态、计时器和待处理效果清理。

