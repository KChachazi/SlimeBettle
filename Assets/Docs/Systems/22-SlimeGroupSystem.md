# 22 SlimeGroupSystem 史莱姆群体系统设计

系统编号：`ACTOR-05`  
系统名称：`SlimeGroupSystem`  
默认命名空间：`SlimeBattle.Slimes.Groups`  
建议实现目录：`Assets/Game/Scripts/Slime/`  
生命周期：当前关卡上下文生命周期，由 `SlimeSystem` 创建和驱动  
状态：玩法待确认，边界已确定

## 1. 核心功能

`SlimeGroupSystem` 管理史莱姆个体之间形成的临时群体关系。它拥有群体成员、共同目标、队列/阵型和群体战斗策略，但不拥有成员的生命、状态和最终存活结果。

当前策划方案提出的群体规则：

- 同一目标行为的史莱姆达到 6 只及以上时，可以形成史莱姆群体；全局史莱姆上限当前为 20 只；
- 最多同时存在 3 个史莱姆群体；
- 目标消失且 10 秒内没有新目标时，群体解散；成员数少于 6 时立即解散；
- 有多个相同目标行为的群体可加入时，个体可以随机选择一个；
- 群体成员跟随群体行动，只要群体中有成员处于引导源范围内，个体不再单独受该放置物范围判断影响；
- 个体因更高优先级需求或更近的其他目标改变行为时，立即离开当前群体；
- 个体仍然保留碰撞体，群体关系不能替代空间碰撞和窄路通行规则。

## 2. 职责

- 根据史莱姆提交的目标行为事实创建、加入和解散群体；
- 保存群体成员、目标、创建时间、最后有效目标时间和当前策略；
- 为群体移动提供队列、阵型和群体目标信息；
- 处理群体战斗策略的选择和策略结果查询；
- 在成员死亡、目标变化、窄路阻塞和场景空间不足时更新群体状态；
- 为 UI、调试工具和表现层提供只读群体快照。

## 3. 不负责的内容

- 不修改史莱姆生命、状态和死亡结果；
- 不替代 `DecisionTreeSystem` 决定个体的本能需求和最终目标；
- 不替代 `MovementSystem` 处理碰撞、推挤和实际位移；
- 不替代 `CombatSystem` 结算攻击和伤害；
- 不直接创建或销毁史莱姆实例；
- 不让群体逻辑成为玩家逐只控制史莱姆的接口。

## 4. 核心数据

```text
GroupId
SlimeGroupRuntimeData
SlimeGroupJoinRequest
SlimeGroupSnapshot
GroupTarget
GroupFormation
GroupCombatStrategy
GroupDissolveReason
```

`SlimeGroupRuntimeData` 至少包含：`GroupId`、成员 `ActorId` 集合、共同目标行为、目标引用、成员数量、当前队列/阵型、群体战斗策略、最后有效目标时间和生命周期状态。

当前图示提出三种群体战斗策略：

| 策略 | 当前提出的效果 | 状态 |
| --- | --- | --- |
| `Defensive` | 成员生命值提升 40%，攻击力降低 20%，优先成为敌方攻击目标 | 暂定 |
| `Balanced` | 成员攻击力和生命值提升 10%，史莱姆出现“害怕”的概率增加 5% | 暂定 |
| `Desperate` | 成员攻击力提升 30%，生命值降低 15%，每次攻击有 5% 概率暴击 | 暂定 |

图示还提出“群体中每一个史莱姆增加成员 5% 攻击力和生命值”的规则；该规则与三种策略的叠加方式未确定，不能在实现前当作最终倍率。

## 5. 必须接口

```csharp
bool TryJoinGroup(ActorId actorId, SlimeGroupJoinRequest request, out GroupId groupId);
void LeaveGroup(ActorId actorId, GroupDissolveReason reason);
void NotifyTargetChanged(ActorId actorId, GroupTarget target);
void NotifySlimeRemoved(ActorId actorId);
void Tick(GroupContext context);
bool TryGetSnapshot(GroupId groupId, out SlimeGroupSnapshot snapshot);
IReadOnlyList<SlimeGroupSnapshot> GetActiveSnapshots();
```

`TryJoinGroup` 只能建立或加入满足当前成员、目标和数量条件的群体；如果同类群体、最大群体数或生命周期条件不满足，必须返回可诊断的失败原因。

## 6. 对外事件

```text
SlimeGroupCreatedEvent
SlimeGroupMemberAddedEvent
SlimeGroupMemberRemovedEvent
SlimeGroupFormationChangedEvent
SlimeGroupStrategyChangedEvent
SlimeGroupDissolvedEvent
```

事件只表示群体关系或策略已经变化，不携带可写的成员运行时对象。

## 7. 依赖与信息流

- `SlimeSystem` 提供个体生成、死亡、目标变化和状态快照；
- `DecisionTreeSystem` 提供个体当前行动意图和目标行为；
- `MovementSystem`/`PassageSystem` 提供空间宽度、队列和通行结果；
- `CombatSystem` 读取群体策略产生的攻击修正或目标倾向；
- `StatusManager` 负责最终的群体增益、减益、害怕和暴击效果；
- `UISystem` 和 `PresentationSystem` 只能读取群体快照或监听群体事件。

推荐信息流为：个体决策 → 提交目标行为 → 群体系统形成/更新关系 → 返回群体行动上下文 → 个体状态机执行移动或战斗。

## 8. 关键不变量

1. 每个存活史莱姆最多属于一个群体。
2. 群体系统不持有个体生命的第二份可写副本。
3. 群体解散后，成员恢复为普通个体决策，不遗留群体目标和策略修正。
4. 关卡暂停、重开和卸载时，所有群体、计时器和订阅一起清理。
5. 群体是否合并必须由一个明确规则决定；当前策划文字“群体不会融合”与“相同目标行为会自动融合”存在冲突，未确认前不得自行选择。

## 9. 待确认项

- “形成群体”是立即创建、周期检查还是由某个个体发起；
- 相同目标群体是否允许合并，以及群体作战时是否禁止合并；
- 群体移动时的队列宽度、阵型、路径和障碍处理；
- 三种战斗策略是随机选择、由目标/环境决定还是由策划指定；
- 群体成员 5% 增益是否存在、如何叠加；
- 群体中个体死亡、害怕、冻结和离开后的策略修正；
- 引导源范围对群体成员的具体判定方式。

## 10. 验收场景

- 6 只拥有同一目标行为的史莱姆能形成一个群体，5 只不能形成；
- 同时存在 3 个群体后，第 4 个候选群体不会静默创建；
- 目标消失后 10 秒内没有新目标时群体解散，成员数降到 5 时立即解散；
- 一个成员改变目标后立即离开群体，其他成员的成员表和策略保持一致；
- 群体通过窄路时仍由 `PassageSystem` 决定排队，成员碰撞体没有被群体逻辑关闭；
- 重开关卡后不存在上一局群体、策略修正和计时器。
