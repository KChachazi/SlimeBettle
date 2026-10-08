# 07 ActorDecisionTree 角色决策树设计

系统编号：ACTOR-02
系统名称：ActorDecisionTree
默认命名空间：SlimeBattle.Actors.AI.DecisionTree
建议实现目录：Assets/Game/Scripts/Actors/AI/DecisionTree/
生命周期：由单个 Actor 创建和销毁
状态：核心架构，具体节点依据策划方案继续细化

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
    DecisionResult
    ActionIntent
    AttackIntent

AttackIntent 至少包含 AttackerId、TargetId 和 AbilityId。它表示行动意图，不表示攻击已经命中。

## 4. 公共接口

    void SetRoot(DecisionNode root);
    DecisionResult Evaluate(DecisionContext context);
    void Reset();
    void Stop();

Evaluate 只返回决策结果。它不能在一次评估中直接调用 MovementSystem、StatusManager 或 Manager 的写入接口。

## 5. 当前角色接入方向

- 史莱姆优先评估进食、取暖、危险和群体目标，再决定移动或攻击；
- 敌人优先评估活动范围内的目标，普通敌人通常选择距离最近的有效目标；
- 守卫根据护送目标、敌人战斗状态和自身控制模式决定待命、移动或攻击；
- SlimeGroupActor 提供共同目标、队列和群体策略事实，成员 Actor 仍然可以因更高优先级需求离开群体。

具体阈值、权重和随机概率仍由角色配置与策划确认决定。

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

