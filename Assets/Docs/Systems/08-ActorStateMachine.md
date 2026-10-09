# 08 ActorStateMachine 角色状态机设计

系统编号：ACTOR-03
系统名称：ActorStateMachine
默认命名空间：SlimeBattle.Actors.AI.StateMachine
建议实现目录：Assets/Game/Scripts/Actors/AI/StateMachine/
生命周期：由单个 Actor 创建和销毁
状态：核心架构，具体状态依据策划方案继续细化

## 1. 职责

- 接收 ActorDecisionTree 产生的 ActionIntent；
- 执行当前动作并管理进入、更新、退出和转换；
- 处理暂停、受控、冻结、击退、死亡和其他外部中断；
- 控制攻击前摇、命中时机、后摇和一次性结算；
- 将 MovementSystem、StatusManager 和其他动作适配的结果转换为状态变化。

## 2. 攻击执行边界

当前不建立 CombatSystem。攻击行为由 ActorStateMachine 进入 AttackState，再由角色的 AttackAction 或 Ability 执行。

    AttackIntent
      → AttackState
      → 前摇和最终校验
      → AttackAction.ExecuteHit()
      → StatusManager.ApplyEffect(EffectRequest)

AttackAction 至少重新检查目标存活、攻击者可行动、攻击范围和冷却。这个检查不是新的战斗系统，而是避免感知快照在前摇期间过期。

## 3. 候选状态

    Idle
    Moving
    Attacking
    Controlled
    Frozen
    KnockedBack
    Dead

Hungry、Agitated、Afraid、Cold、Hot 和 Burning 等内容优先作为 StatusManager 管理的状态效果或决策条件，不默认全部做成互斥行动状态。

## 4. 核心类型

    ActorStateMachine
    IActorState
    StateId
    StateContext
    StateTransition
    StateMachineResult
    InterruptReason
    AttackAction

## 5. P0 公共接口

    StateMachineResult SubmitIntent(ActionIntent intent, StateContext context);
    bool NotifyMovementCompleted(string requestId);
    StateMachineResult RequestInterrupt(InterruptReason reason, IActorActionPort actions);
    void Tick(StateContext context);
    StateId GetCurrentStateId();
    void Resume();
    void Reset(IActorActionPort actions);

移动完成回执只接受当前请求 ID；已取消、暂停、死亡或旧关卡的迟到回执返回 false。若目标位置已经到达，状态机保持 Idle，不重复提交移动。

## 6. 不负责的内容

- 不决定长期目标和目标优先级；
- 不维护所有 Actor 的状态机；
- 不直接拥有生命和持续状态数据；
- 不把动画回调当作唯一的命中依据；
- 不直接执行跨角色的生命修改，效果必须交给 StatusManager。

## 7. 依赖

- ActorDecisionTree 提供 ActionIntent；
- MovementSystem 执行移动；
- StatusManager 查询控制状态并应用攻击或治疗效果；
- MapManager 提供空间事实；
- 所属 Actor 提供自身运行时数据。

## 8. 关键不变量

1. 一个 Actor 同一时刻只有一个主行动状态。
2. 一次攻击最多触发一次有效命中，除非 Ability 配置明确允许多段命中。
3. 死亡、暂停和重开时，当前动作和待执行命中必须失效。
4. 状态机退出后不能向已销毁的 Actor 或 LevelContext 提交请求。

## 9. P0 外部接入边界

MovementSystem 和 StatusManager 尚未提供可编译的正式实现。P0 使用 Actor 层的 `IActorActionPort` 输出 `ActorMovementRequest` 与 `ActorEffectRequest`，并由 `IActorPerceptionSource` 提供命中时的当前目标快照。正式适配器必须把两种请求分别转交文档中的 `MovementSystem.SubmitMovement` / `CancelMovement` 和 `StatusManager.ApplyEffect`，并把位置与带请求 ID 的移动完成结果回传 Actor；不得在正式适配器中自行扣血或写 Transform。`Assets/Game/Tests/` 的可视化场景仅为验收而移动圆形标记，不作为正式 MovementSystem。适配器尚未接入时，关卡不驱动 Actor AI。

