# 08 SlimeSystem 史莱姆系统设计

系统编号：`ACTOR-02`  
系统名称：`SlimeSystem`  
默认命名空间：`SlimeBattle.Slimes`  
建议实现目录：`Assets/Game/Scripts/Slime/`  
生命周期：当前关卡上下文生命周期  
状态：核心架构，个体规则依据当前策划方案继续细化

## 1. 职责

- 创建、初始化、注册和销毁史莱姆实例；
- 保存史莱姆的本局运行时数据；
- 驱动史莱姆的感知、决策和行动执行；
- 管理史莱姆的存活、死亡、变种和控制状态；
- 为其他系统提供史莱姆只读查询和明确的修改请求入口。

史莱姆自主行动采用两层结构：`DecisionTreeRunner` 负责主动感知与决策，`StateMachineRunner` 负责执行行动、处理控制和中断。两个运行器是史莱姆系统内部的组成部分。史莱姆群体的成员和策略由 `SlimeGroupSystem` 管理，个体生命和状态仍由本系统管理。

## 2. 不负责的内容

- 不决定机关本身何时触发；
- 不直接控制 Unity UI、特效和音效；
- 不在史莱姆内部计算统一的伤害、击退和易伤规则；
- 不让外部系统直接写入 `SlimeRuntimeData`；
- 不把 ScriptableObject 配置资产当作本局运行状态。

## 3. 核心数据

```text
SlimeConfig
SlimeRuntimeData
SlimeSpawnRequest
SlimeSnapshot
SlimeControlMode
SlimeActionIntent
SlimeNeedState
SlimeStatusSnapshot
```

`SlimeRuntimeData` 至少包含：`ActorId`、`ConfigId`、当前生命、存活状态、当前位置引用、控制模式、AI 运行状态引用、当前目标和进食/环境相关计时。具体字段由玩法和移动方案补充。

## 3.1 当前策划规则摘要

- 当前方案的史莱姆上限为 20 只；达到上限时，生成请求必须返回可诊断的拒绝结果。
- 初始数值方案为攻击力 30、生命值 500、移速 200、攻击间隔 1 秒；这些值应进入 `SlimeConfig`，后续允许由策划调整。
- 个体本能围绕进食、取暖和躲避危险展开；食物、熔炉等引导源由决策树查询，史莱姆不能被玩家直接选中并下达逐只指令。
- 史莱姆之间存在碰撞体；空间阻挡、推挤和窄路排队由 `MovementSystem`/`PassageSystem` 处理，不能在个体逻辑中偷偷绕过。
- 史莱姆遇到魔物后可以产生战斗意图；暴躁状态下还可能把附近人类纳入目标。战斗目标、生命阈值逃避和群体目标切换仍需结合 `DecisionTreeSystem` 确认。

## 3.2 个体状态效果的接入边界

当前策划提出的饥饿、暴躁、害怕、寒冷、炙热、冻结、燃烧和暴击，按以下边界接入：

- 计时、环境和进食事实由 `SlimeSystem`/`MechanismSystem` 产生；
- 状态添加、持续、移除和数值修正由 `StatusManager` 统一处理；
- 史莱姆读取状态快照并把它们转成决策条件或状态机中断；
- 寒冷与炙热互相抵消、冻结暂停行动、炙热死亡范围伤害等特殊规则，在最终数值确认后由 `StatusManager` 与 `CombatSystem` 协作实现。

## 4. 必须接口

```csharp
ActorId SpawnSlime(SlimeSpawnRequest request);
bool RequestDespawn(ActorId actorId, DespawnReason reason);
bool TryGetSnapshot(ActorId actorId, out SlimeSnapshot snapshot);
IReadOnlyList<SlimeSnapshot> GetAliveSnapshots();
bool SubmitControl(ActorId actorId, SlimeControlRequest request);
EffectApplyResult ApplyEffect(ActorId actorId, ResolvedEffect effect);
```

`ApplyEffect` 只接受 `StatusManager` 经过统一规则处理后的 `ResolvedEffect`；具体伤害是否导致死亡，仍由 `SlimeSystem` 修改自己的生命并发布结果。

## 5. 对外事件

```text
SlimeSpawnedEvent
SlimeControlChangedEvent
SlimeRuntimeStateChangedEvent
SlimeDiedEvent
SlimeDespawnedEvent
```

死亡事件表示史莱姆已经进入死亡结果，不表示刚收到一份伤害请求。

## 6. 依赖

- `ConfigSystem` 提供史莱姆配置；
- `ActorCore` 提供角色身份和公共契约；
- `DecisionTreeSystem` 与 `StateMachineSystem` 提供 AI 运行能力；
- `MapSystem`、`MovementSystem`、`PassageSystem`、`CombatSystem` 和 `StatusManager` 提供行动所需的查询或结果。
- `SlimeGroupSystem` 管理群体成员关系、共同目标和群体策略；本系统只提交个体目标/状态变化事实。

## 7. 内部更新顺序

1. 清理上一轮已经失效的对象和请求；
2. 更新来自 `StatusManager`、`MovementSystem` 和 `PassageSystem` 的结果；
3. 对仍可行动的史莱姆构造 `DecisionContext`；
4. 执行决策树；
5. 将 `ActionIntent` 提交给状态机；
6. 由状态机向移动、战斗或通行系统提交动作请求；
7. 记录本轮快照并等待下一轮结果。

个体在加入群体、离开群体或目标行为改变时，应通知 `SlimeGroupSystem`；这类通知是事实提交，不代表群体系统可以直接改写个体生命或状态。

## 8. 关键不变量

1. 当前关卡中每个史莱姆实例的 `ActorId` 唯一。
2. 死亡或销毁后的史莱姆不能继续思考和提交动作。
3. 史莱姆共享配置但不共享运行时生命、目标和行动状态。
4. 外部系统只能通过公共接口请求修改，不能直接拿到可写的运行时对象。

## 9. 验收场景

- 生成两只共享配置的史莱姆，修改其中一只生命不会影响另一只；
- 史莱姆受控或停顿时，决策树停止提交新的行动；
- 死亡后不再移动、攻击、触发机关或响应旧请求；
- 重新加载关卡后没有上一局的史莱姆、AI 订阅和队列。

## 10. 待确认项

- 史莱姆种类、变种和数量上限；
- 控制模式与冻结/停顿的具体优先级；
- 感知范围、决策频率和群体信息共享方式；
- 饥饿计时的分段规则，以及同伴死亡、血腥味等触发来源；
- 寒冷/炙热环境的判定范围、状态抵消和离开区域计时；
- 史莱姆是否允许由玩家直接选中或控制。

