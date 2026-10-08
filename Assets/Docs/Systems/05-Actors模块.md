# 05 Actors 模块设计

模块编号：ACTORS
模块名称：Actors
默认根命名空间：SlimeBattle.Actors
建议实现目录：Assets/Game/Scripts/Actors/
生命周期：类型契约随应用存在，运行时实例属于当前 LevelContext
状态：核心架构

## 1. 模块定位

Actors 模块描述一个个体或群体级自主决策生物本身。它不负责保存所有角色的全局集合，也不负责关卡内角色的生成和销毁。

LevelContext 持有的 SlimeManager、EnemyManager 和 GuardManager 负责生命周期与索引；这些 Manager 创建并驱动本模块中的 Actor 实例。

## 2. 模块内容

| 内容 | 作用 |
| --- | --- |
| ActorCore | 角色身份、阵营、配置引用、生命与公共运行时数据的基础承载 |
| ActorDecisionTree | 感知、判断和行动意图生成的通用基础 |
| ActorStateMachine | 行动执行、攻击时机、暂停、受控和中断的通用基础 |
| SlimeActor | 史莱姆个体的需求、感知和自主行动规则 |
| EnemyActor | 敌人个体的索敌、移动和攻击行为规则 |
| GuardActor | 守卫个体的护送、防守和受击响应规则 |
| SlimeGroupActor | 史莱姆群体的共同目标、成员关系和群体决策 |

## 3. 运行时组成

一个自主决策 Actor 至少由以下部分组成：

    ActorCore
      ├─ ActorRuntimeData
      ├─ PerceptionSnapshot
      ├─ ActorDecisionTree
      ├─ ActorStateMachine
      └─ Actor-specific Action / Ability

决策树只生成 ActionIntent；状态机负责消费意图并执行动作。攻击在 AttackAction 的命中时机生成 EffectRequest，最终由 StatusManager 修改目标状态。

## 4. 数据所有权

- Actor 持有自身的身份、生命、当前状态、位置快照和 AI 运行时数据；
- StatusManager 拥有状态和效果的统一结算规则，是生命与状态的唯一合法修改入口；
- SlimeManager、EnemyManager 和 GuardManager 持有同类型 Actor 的集合、索引和生命周期；
- SlimeGroupActor 持有群体关系，不复制成员的生命和状态；
- MapManager 与 MovementSystem 持有地图和移动执行规则，不把位置复制到多个 Actor 管理器中。

## 5. 信息流

    Manager 创建 Actor
      → Actor 读取配置并初始化
      → Perception 提供当前事实
      → DecisionTree 产生 ActionIntent
      → StateMachine 执行动作
      → MovementSystem 或 StatusManager 接收明确请求
      → Actor 更新自身运行时数据并发布结果

感知结果是只读事实，不能通过感知接口修改其他 Actor。ActionIntent 不是已经发生的结果，不能被 UI 或表现层当作成功事件使用。

## 6. 不负责的内容

- 不提供跨关卡全局 Actor 单例；
- 不创建 LevelContext 持有的 Manager；
- 不把攻击目标选择、攻击执行和生命修改混成一个步骤；
- 不让决策树直接扣血、移动 Transform 或触发机关；
- 不让动画回调直接产生伤害；
- 不让 SlimeGroupActor 替成员保存第二份生命和状态。

## 7. 关键不变量

1. 每个存活 Actor 在当前 LevelContext 中拥有唯一 ActorId。
2. Actor 被销毁、死亡或关卡重开后，决策树和状态机都不能继续提交动作。
3. 决策树可以重复评估，但一次攻击只能由攻击动作在命中时机结算一次。
4. Actor 的公共查询只能返回只读快照或结果，不暴露可写的内部运行时对象。
5. 同一份生命和状态数据不能同时由 Actor、Manager 和 StatusManager 各自维护。

## 8. 验收场景

- 创建史莱姆、敌人和守卫时，每个实例都有独立 ActorId、生命、状态和 AI 运行器；
- 决策树发现目标后产生攻击意图，但在状态机实际进入命中时机前不会扣血；
- 目标在前摇期间死亡或离开范围时，AttackAction 被拒绝且不产生效果；
- StatusManager 施加减速后，MovementSystem 使用新的移动修正；
- 重开关卡后，所有 Actor、群体关系、AI 引用和待执行动作均不存在。

## 9. 待确认项

- Actor 是否使用纯 C# 运行时对象、MonoBehaviour 适配器或两者组合；
- 感知是否由每个 Actor 独立查询，还是由关卡级空间索引提供；
- 攻击命中采用距离查询、触发器、命中区域还是投射物；
- SlimeGroupActor 是否需要独立的表现对象；
- 是否需要玩家可以直接控制某类 Actor。

