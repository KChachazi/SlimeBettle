# 07 ActorDecisionTree 角色决策树设计

系统编号：ACTOR-02
系统名称：ActorDecisionTree
默认命名空间：SlimeBattle.Actors.AI.DecisionTree
建议实现目录：Assets/Game/Scripts/Actors/AI/DecisionTree/
生命周期：由单个 Actor 创建和销毁
状态：P0 意图输出与史莱姆/普通敌人选路已实现；概率和状态结算待外部系统接入

## 1. 职责

- 读取单个 Actor 的自身状态和局部感知；
- 根据事实选择下一步行动、目标或等待；
- 生成移动、攻击、等待、逃避、加入群体等 ActionIntent；
- 在目标失效、行动失败或外部状态改变时重新评估。

感知模块属于 ActorDecisionTree 的输入层。它提供目标距离、可见性、地图区域、危险、引导源和其他 Actor 快照等只读事实；感知模块不修改世界状态。

## 2. 决策与执行边界

- DecisionTree 决定“是否尝试攻击、攻击谁、何时等待”；
- StateMachine 决定动作如何开始、进行、打断和完成；
- AttackAction 在实际命中时机执行一次最终校验并生成 EffectRequest；
- StatusManager 负责最终伤害、治疗和状态变化。

因此，DecisionTree 不能直接调用 ApplyEffect，也不能直接移动或修改目标。

## 3. 核心类型

    ActorDecisionTree
    DecisionNode
    DecisionContext
    PerceptionSnapshot
    TargetObservation
    GuidanceKind
    GuidanceObservation
    ActionIntent
    AttackIntent

当前 P0 的 `ActionIntent.Reason` 用于展示选中分支；尚未单独封装 `DecisionResult`。AttackIntent 至少包含 AttackerId、TargetId 和 AbilityId。它表示行动意图，不表示攻击已经命中。

## 4. 公共接口

    void SetRoot(DecisionNode root);
    ActionIntent Evaluate(DecisionContext context);
    void Reset();
    void Stop();

Evaluate 只返回决策结果。它不能在一次评估中直接调用 MovementSystem、StatusManager 或 Manager 的写入接口。

## 5. 当前角色接入方向

- 史莱姆优先评估进食、引导源、危险和群体目标，再决定移动或攻击；首关熔炉只作为普通引导源，不产生温度状态；
- 敌人优先评估活动范围内的目标，普通敌人通常选择距离最近的有效目标；
- 守卫根据护送目标、敌人战斗状态和自身控制模式决定待命、移动或攻击；
- SlimeGroupActor 提供共同目标、队列和群体策略事实，成员 Actor 仍然可以因更高优先级需求离开群体。

P0 感知快照可提供食物/引导源、普通目的地、逃跑目的地、目标是否人类和 `FleeCheckSucceeded`。后者是一次概率判定的输入结果，不表示决策树或测试场景已经实现掷骰与状态添加。`GuidanceKind.Wander` 表示一个漫游目的地，Slime/Enemy 决策仅在其他优先行为没有目标时将它转换为 Move 意图。Level_test 的 `RandomWalkPerceptionSource` 只提供该类引导且不提供敌对目标；它是移动链路验收输入，不是正式目标感知。首关饥饿可优先食物；寒冷/炙热不产生状态效果。普通敌人无目标时可接受活动范围内的游走点。

首关史莱姆按本轮策划图示执行以下优先级（从高到低）：

1. **害怕**：只接受逃跑目的地或漫游点，不响应食物、熔炉等引导源；
2. **对应需求引导**：饥饿优先食物，寒冷优先温暖/熔炉；满足需求后重新进行战斗判定；
3. **战斗**：在有效敌对目标中选择最近者，进入攻击范围则攻击，否则接近；
4. **暴躁及正常状态**：没有更高优先级目标时，在感知范围内选择距离最近的食物或熔炉。

熔炉作为首关普通引导源保持可用状态；史莱姆到达熔炉后，只要熔炉仍在吸引范围和可用状态内，路线感知不会将它标记为一次性访问目标。

正式第一关组合根在目标感知之外复用有界 `RandomWalkPerceptionSource`：每个敌人出生时绑定以自身出生点为中心的局部 `EnemyActivityBounds`（默认半边长 4 个世界单位，并裁剪到地图边界），无目标时仍会产生可执行的局部游走意图；守卫不消费该游走点，而是读取护送武器的实时位置并围绕 `EscortDistance` 跟随、拦截和攻击，同时受 `LeashDistance` 限制。首关的自由史莱姆默认进入等待触发模式：出生后只在各自出生点半径 1.5 个世界单位内待机，感知范围内出现新食物、障碍状态解除或组合根调用 `ReleaseSlimeRoutes()` 后，才进入三路线选择；携带期间不再提交 Actor 移动意图。

同类食物在岔路选择阶段仍按首关规则随机选择；普通状态下的食物/熔炉吸引按距离选择最近来源。策划图示要求的随机选择、20%/50%逃跑概率及状态触发时机仍待正式状态/概率接口；测试场景只验证已给定条件下的选路。

`ActorTargetPerceptionSource` 从当前关卡的 SlimeManager/EnemyManager 公开快照构造敌我目标事实；按自身 `ActorConfig.DetectionRange` 过滤死亡、同阵营和半径外 Actor，暂不判断视线遮挡。Level_test 原有随机漫游感知保持不变。`Assets/Game/Tests/AIBranches/` 的独立场景在正式目标感知之外只补尚未接入的食物、热源、逃跑判定和测试用人类目标。

守卫感知还会读取 `EnemySnapshot.TargetActorId`、`IsAttacking` 和 `ActionState`，把敌人标记为“正在攻击护送武器”“正在攻击守卫”或“靠近武器且正在攻击”；这些标记只作为 `TargetObservation` 的只读事实输入，优先级由 `GuardDecision` 消费。

## 6. 不负责的内容

- 不写入生命、状态或 Transform；
- 不执行命中、扣血、治疗和击退；
- 不替 StateMachine 保存当前行动状态；
- 不通过全局事件广播每次思考结果；
- 不保存所有 Actor 的行为树实例。

## 7. 关键不变量

1. 相同感知事实和随机源应得到可解释的决策结果。
2. 死亡、暂停和重开时决策树必须停止。
3. 一个决策结果最多提交一个当前行动意图，重复评估不能重复结算效果。
4. 感知快照必须标明采样时间或更新轮次，避免使用已失效目标。

## 8. 待确认项

- 节点类型是否数据化为 ScriptableObject；
- 感知频率和每轮 Actor 数量上限；
- 是否需要随机权重、优先级和冷却节点；
- 攻击范围、视线和目标有效性的最终判定方式。

