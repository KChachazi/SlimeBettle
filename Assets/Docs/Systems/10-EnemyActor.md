# 10 EnemyActor 敌人个体设计

系统编号：ACTOR-05
系统名称：EnemyActor
默认命名空间：SlimeBattle.Actors.Enemies
建议实现目录：Assets/Game/Scripts/Actors/Enemy/
生命周期：由当前 LevelContext 的 EnemyManager 创建和销毁
状态：基础行为已有策划方向，特殊敌人与 Boss 待确认

## 1. 职责

EnemyActor 表示一个敌人个体，组合通用 ActorCore、ActorDecisionTree 和 ActorStateMachine，并实现敌人自己的索敌、活动范围、移动和攻击行为。

实现形式是挂在正式敌人预制体上的 MonoBehaviour。预制体不保存本局生命、状态、目标或 AI 运行态；EnemyManager 实例化后逐个初始化。

- 读取 EnemyConfig 并保存本局运行时数据；
- 在固定活动范围内感知目标；
- 选择有效目标并产生移动或攻击意图；
- 通过 AttackAction 在命中时机向 StatusManager 提交效果；
- 响应控制、受击、死亡和目标失效。

普通敌人当前方案通常选择索敌范围内距离最近的有效目标，不主动逃跑；具体目标优先级由配置和决策树确定。

P0 只支持 `NearestHostile`：在配置的活动范围内找最近可见敌对目标，进入攻击范围则产生攻击意图，否则产生移动意图；无目标时，如感知层给出范围内的游走点则移动，否则等待。`SlimeFirst` / `WeaponFirst` 暂不支持，生成时明确拒绝，不静默套用其他策略。`LastDecisionReason` 可供调试读取。巡游点如何生成由地图/感知适配器负责，Actor 不直接读地图私有数据。

移动适配层通过 `NotifyMovementResult(Vector2)` 回传位置，到达时通过 `NotifyMovementCompleted(string)` 回传当前请求 ID。

## 2. 核心数据

    EnemyConfig
    EnemyRuntimeData
    EnemySnapshot
    EnemyActivityBounds
    EnemyTargetPolicy
    EnemyControlMode

第一场景的攻击力、生命、移速和攻击间隔等数值必须进入 EnemyConfig 或关卡配置，不能写死在 EnemyActor 中。

## 3. 必须接口

    void Initialize(EnemyConfig config, ActorId actorId, Vector2 position,
                    FactionId factionId, EnemyTargetPolicy targetPolicy,
                    EnemyActivityBounds activityBounds);
    void Tick(ActorContext context);
    EnemySnapshot GetSnapshot();
    void SetActivityBounds(EnemyActivityBounds bounds);

## 4. 不负责的内容

- 不维护所有敌人的集合和生成波次；
- 不实现通用决策树和状态机基础；
- 不直接修改其他 Actor 的生命；
- 不直接执行地图碰撞和 Transform 移动；
- 不把目标筛选、命中和伤害写成一个不可测试的回调。

## 5. 依赖

- EnemyManager 提供生命周期和查询；
- ConfigSystem 提供敌人配置；
- ActorCore、ActorDecisionTree 和 ActorStateMachine 提供个体基础；
- MapManager 提供活动范围和空间查询；
- MovementSystem 执行移动；
- StatusManager 处理攻击效果和状态结果；
- MechanismSystem 在敌人目标可能包含护送武器时提供护送武器状态查询。

## 6. 关键不变量

1. 死亡或销毁后的 EnemyActor 不能继续索敌、移动或攻击。
2. 目标选择结果必须基于感知快照，不能读取其他系统的私有字段。
3. 一次攻击的生命变化只能由 StatusManager 产生。
4. 活动范围只限制敌人行为，不修改 MapManager 的地图数据。

## 7. 待确认项

- 敌人种类、特殊敌人和 Boss；
- 人类、守卫、史莱姆和护送武器的目标优先级；
- 是否需要独立波次或刷怪规则；
- 敌人是否受食物、熔炉和其他场景元素影响。

