# 07 StateMachineSystem 状态机系统设计

系统编号：`AI-02`  
系统名称：`StateMachineSystem`  
默认命名空间：`SlimeBattle.AI.StateMachine`  
建议实现目录：`Assets/Game/Scripts/Slime/`  
生命周期：由具体角色系统创建和销毁  
状态：核心架构，具体状态依据当前策划方案继续细化

## 1. 职责

- 执行决策树产生的 `ActionIntent`；
- 表示角色当前正在执行的行为；
- 处理暂停、受控、击退、死亡和其他外部中断；
- 维护状态进入、更新、退出和转换；
- 将动作执行结果回报给所属角色系统。

## 2. 不负责的内容

- 不主动决定长期目标；
- 不直接计算最终伤害；
- 不持有所有角色的运行时状态；
- 不把动画播放成功当作状态完成条件。

## 3. 核心类型

```text
StateMachineRunner
IState
StateId
StateContext
StateTransition
StateMachineResult
```

第一版可以使用代码状态类；如果策划需要频繁调整状态图，再考虑 ScriptableObject 或编辑器可视化。

## 3.1 当前候选状态与状态效果

当前策划文档已经提出若干行为状态，但它们是否全部作为互斥状态、并行状态或由 `StatusManager` 作为叠加效果处理，仍需实现前确认：

| 候选项 | 当前含义 | 备注 |
| --- | --- | --- |
| `Idle` / `Moving` | 等待或执行移动 | 基础行动状态 |
| `Combat` | 已产生攻击意图并进入攻击范围 | 战斗期间对引导源的响应有例外规则 |
| `Hungry` | 超过一段时间未进食 | 正文与图示的阈值规则冲突 |
| `Agitated` | 攻击与移速提升，并可能攻击人类 | 可由饥饿、同伴死亡、血腥味等触发，具体来源待确认 |
| `Afraid` | 提速、随机行动、暂时不受引导源影响 | 图示还提出低生命逃避判定 |
| `Cold` / `Hot` | 环境或食物产生的温度状态 | 寒冷与炙热的抵消规则需要统一 |
| `Frozen` | 暂停当前所有行动和状态 | 由冻结效果触发，优先级高于普通行动 |
| `Burning` | 持续伤害效果 | 更适合作为持续状态效果，不一定是互斥行动状态 |

状态机只负责当前行动的进入、执行和中断；状态的添加、持续时间和数值由 `StatusManager` 按统一效果规则处理。

## 4. 必须接口

```csharp
void SetInitialState(StateId stateId);
void SubmitIntent(ActionIntent intent);
void RequestInterrupt(InterruptReason reason);
void Tick(StateContext context);
StateId GetCurrentStateId();
void Reset();
```

`SubmitIntent` 只提交动作意图，状态机根据当前状态和中断规则决定是否接受、排队、拒绝或打断。

## 5. 对外事件

角色系统可以将状态变化转换为对外的 `ActorStateChangedEvent`。状态机内部的每次转换不默认发布全局事件。

## 6. 依赖

- `DecisionTreeSystem` 输出的 `ActionIntent`；
- `MovementSystem`、`CombatSystem` 和 `PassageSystem` 的执行结果；
- `StatusManager` 提供的控制/击退/停顿状态；
- 所属角色系统的运行时数据。

## 7. 关键不变量

1. 同一时间只有一个当前状态。
2. 中断优先级和可打断性必须由状态定义，不能由表现层临时改写。
3. 状态退出后不能继续向已销毁的角色或系统提交请求。

## 8. 待确认项

- 状态名称和状态层级；
- 是否允许并行状态，例如移动与持续效果；
- 中断优先级；
- 行动失败后的重试和回退规则。

