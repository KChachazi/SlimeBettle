# 06 ActorCore 角色基础层设计

系统编号：ACTOR-01
系统名称：ActorCore
默认命名空间：SlimeBattle.Actors
建议实现目录：Assets/Game/Scripts/Actors/Common/
生命周期：类型和契约随应用存在；具体运行时 Actor 由所属 Manager 管理
状态：核心架构

## 1. 职责

ActorCore 是所有史莱姆、敌人、守卫和群体 Actor 共用的基础数据与接收契约。

- 定义 ActorId、阵营、类型和配置引用；
- 保存个体的生命、存活、位置和状态查询所需的运行时基础数据；
- 提供 Actor 快照和效果接收边界；
- 为决策树、状态机、MovementSystem 和 StatusManager 提供统一身份。

ActorCore 是单个 Actor 的基础对象，不是全局 Actor 注册表。

## 2. 核心数据

    ActorId
    FactionId
    ActorType
    ActorRuntimeData
    ActorSnapshot
    ActorControlMode
    ResolvedEffect
    EffectApplyResult

ActorRuntimeData 至少表达 ActorId、ConfigId、FactionId、ActorType、当前生命、最大生命、位置、IsAlive 和当前控制模式。具体角色数据由 SlimeActor、EnemyActor 或 GuardActor 扩展。

P0 中 `ActorCore` 是唯一持有生命与状态 ID 的对象；所属 Actor 通过 `IEffectReceiver.ApplyResolvedEffect` 接收状态系统已结算结果，并通知自身 Manager 处理死亡。Manager 不提供对外任意扣血入口。

## 3. 公共接口

    ActorId GetActorId();
    ActorType GetActorType();
    FactionId GetFactionId();
    bool IsAlive();
    ActorSnapshot GetSnapshot();

效果接收接口只允许由 StatusManager 或 LevelContext 内部的效果适配调用：

    EffectApplyResult ApplyResolvedEffect(ResolvedEffect effect);

调用方不能通过该接口绕过 StatusManager 自己计算伤害；Actor 只负责把已统一结算的结果写入自身运行时数据并返回结果。

## 4. 不负责的内容

- 不管理其他 Actor 实例；
- 不决定个体的长期目标；
- 不执行移动、攻击命中和机关触发；
- 不直接播放动画、特效和音效；
- 不保存 ScriptableObject 配置资产的可变运行时状态。

## 5. 依赖

- ConfigSystem 提供配置引用和设计时数据；
- ActorDecisionTree 和 ActorStateMachine 提供自主行为；
- StatusManager 统一处理效果和状态修改；
- MovementSystem 执行位置变化。

## 6. 关键不变量

1. ActorId 在当前 LevelContext 中唯一，销毁后不能被旧请求复用为有效目标。
2. 当前生命和状态只存在一份可写运行时数据。
3. ActorSnapshot 不暴露可写引用。
4. 死亡 Actor 不能继续接受普通行动请求。

## 7. 待确认项

- ActorId 的具体类型和生成策略；
- 是否统一使用 ActorView 作为 Unity Transform 适配层；
- 是否需要通用的玩家可控 Actor 接口。

