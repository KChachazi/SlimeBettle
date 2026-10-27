# 09 SlimeActor 史莱姆个体设计

系统编号：ACTOR-04
系统名称：SlimeActor
默认命名空间：SlimeBattle.Actors.Slimes
建议实现目录：Assets/Game/Scripts/Actors/Slime/
生命周期：由当前 LevelContext 的 SlimeManager 创建和销毁
状态：个体生命周期、状态优先级、食物/热源引导和战斗决策已有运行时实现；路径由 MovementSystem 的 A* 执行

## 1. 职责

SlimeActor 表示一只史莱姆的完整个体运行时行为。它组合 ActorCore、ActorDecisionTree、ActorStateMachine 和史莱姆专属的需求、感知、行动与群体上下文。

实现形式是挂在正式史莱姆预制体上的 MonoBehaviour。预制体只保存静态组件和外观；生命、状态、目标与 AI 运行时数据由 Manager 实例化后在 `Initialize` 中逐个创建，不能写进预制体资产。

- 保存个体配置引用和本局运行时数据；
- 维护进食、引导源、危险和群体相关的决策输入；
- 产生自主行动意图并执行移动、攻击、等待或离开群体；
- 响应 StatusManager 提供的生命和状态结果；
- 向 SlimeManager 报告目标、存活和群体关系事实。

SlimeActor 不负责全局史莱姆数量、实例索引、生成和销毁。

## 2. 核心数据

    SlimeConfig
    SlimeRuntimeData
    SlimeSnapshot
    SlimeNeedState
    SlimeControlMode
    SlimeActionContext
    GroupId

SlimeRuntimeData 至少包含 ActorId、ConfigId、当前生命、位置、控制模式、当前目标、AI 运行状态和进食/环境相关计时。

## 3. 当前行为输入

- 进食、引导源和躲避危险是当前方案中的主要本能需求；
- 食物、首关只作引导源的熔炉和其他环境事实由感知层提供，不允许玩家逐只下达永久指令；
- 史莱姆遇到魔物后可以产生攻击意图，暴躁状态下可能把附近人类单位纳入目标；
- 生命阈值逃避、饥饿分段和其他已确认状态规则由 StatusManager 与配置提供；首关不启用寒冷/炙热效果；
- 史莱姆群体的共同目标、队列和策略由 SlimeGroupActor 提供，但成员仍保留自己的决策树。

当前方案的基础数值和史莱姆上限由 SlimeConfig 与关卡配置表达，不写死在 SlimeActor 行为中。

当前 `BasicSlimeDecision` 的优先级（高到低）：

1. **害怕：**清除战斗目标，忽略食物/熔炉等引导；有逃跑目标时离开危险，否则使用漫游目标或等待。
2. **饥饿：**有食物引导时优先前往食物。
3. **寒冷：**有温暖引导时前往热源。当前代码在饥饿与寒冷同时存在时先检查饥饿；是否启用寒冷由关卡状态配置决定。
4. **战斗：**选择可见且有效的最近敌对目标；进入攻击范围时攻击，否则接近。暴躁状态达到配置延迟后可把人类纳入目标。
5. **普通引导：**没有战斗目标时，选择最近的食物/温暖来源，并把该来源位置记录为后续游走中心。
6. **无引导：**使用感知提供的普通目标/群体目标，或在当前位置附近游走/等待。

史莱姆的移动目标由决策树提交给 MovementSystem；SlimeManager 注册的史莱姆使用 A* 网格路径避开地图和动态机关阻挡。`LastDecisionReason` 可供调试读取，`NeedState` 从状态 ID 派生，不另存一份状态。食物吃后是否增殖由战役配置开关控制；当前关闭。

移动适配层在收到位置结果后调用 `NotifyMovementResult(Vector2)`，到达时再用当前请求 ID 调用 `NotifyMovementCompleted(string)`；迟到或失效回执不会完成新行动。

## 4. 必须接口

    void Initialize(SlimeConfig config, ActorId actorId, Vector2 position, FactionId factionId);
    void Tick(ActorContext context);
    SlimeSnapshot GetSnapshot();
    void SetControlMode(SlimeControlMode mode);
    void NotifyGroupContext(SlimeGroupSnapshot snapshot);

生命和状态变化不由 SlimeActor 对外提供任意扣血接口，而由 StatusManager 通过 ActorCore 的效果接收契约完成。

## 5. 不负责的内容

- 不创建或销毁其他史莱姆；
- 不维护史莱姆总数和 ActorId 索引；
- 不统一计算伤害、治疗、击退和易伤；
- 不直接修改 MapManager、MovementSystem 或其他 Actor 的私有数据；
- 不播放 UI、动画和音效。

## 6. 依赖

- SlimeManager 提供生命周期和群体上下文；
- ConfigSystem 提供 SlimeConfig；
- ActorCore、ActorDecisionTree 和 ActorStateMachine 提供个体基础；
- MapManager 和 MovementSystem 提供空间与移动结果；
- StatusManager 提供生命、状态和移动修正查询。

## 7. 关键不变量

1. 史莱姆共享配置，不共享生命、目标、计时器和 AI 运行状态。
2. 死亡或销毁后，史莱姆不能继续思考、移动或执行攻击。
3. 群体关系变化必须通知 SlimeManager，不能由表现层直接改变成员列表。
4. 一次攻击由状态机的攻击动作只提交一次效果请求。

## 8. 验收场景

- 两只使用同一 SlimeConfig 的史莱姆拥有独立生命和目标；
- 史莱姆在感知到食物或危险后能产生可观测的移动意图；
- 史莱姆在攻击前摇期间目标死亡时不会造成伤害；
- 史莱姆死亡后不再产生新的决策和动作；
- 重开关卡后不保留上一局的 SlimeActor 和群体上下文。

