# 12 SlimeGroupActor 史莱姆群体个体设计

系统编号：ACTOR-07
系统名称：SlimeGroupActor
默认命名空间：SlimeBattle.Actors.SlimeGroups
建议实现目录：Assets/Game/Scripts/Actors/Slime/Groups/
生命周期：由 SlimeManager 创建、驱动和销毁
状态：玩法待确认，群体边界已确定

## 1. 模块定位

SlimeGroupActor 是一个群体级自主决策 Actor，不是新的关卡级 Manager，也不是可以独立承受伤害的角色。它代表若干 SlimeActor 的共同目标、队列、阵型和群体策略。

SlimeManager 拥有 SlimeGroupActor 的集合，并负责把个体成员事实交给它；成员的生命、状态、碰撞和最终存活结果仍由各自 SlimeActor 负责。

## 2. 职责

- 根据成员的共同目标和数量形成或解散群体；
- 保存成员 ActorId、共同目标、队列/阵型和策略；
- 向成员决策树提供群体目标和行动上下文；
- 根据成员离开、死亡、目标消失和空间阻塞更新群体；
- 提供群体只读快照。

当前策划方案提出：同一目标行为达到 6 只时可以形成群体，最多同时存在 3 个群体，目标消失 10 秒后或成员少于 6 只时解散。数值和冲突规则仍需试玩确认。成员进入队列、多个同类引导源或多个状态同时存在时是否采用“先进先出”，暂记录为下次策划沟通问题，不在本轮编码。

P0 只实现显式创建、成员 ID、共同目标和只读快照；上述自动成群与解散规则未启用。`GroupId` 和 `SlimeGroupActor` 使用 `SlimeBattle.Actors.SlimeGroups`。

## 3. 核心数据

    GroupId
    SlimeGroupRuntimeData
    SlimeGroupJoinRequest
    SlimeGroupSnapshot
    GroupTarget
    GroupFormation
    GroupStrategy
    GroupDissolveReason

## 4. 必须接口

    bool TryJoinMember(ActorId actorId, SlimeGroupJoinRequest request);
    void RemoveMember(ActorId actorId, GroupDissolveReason reason);
    void NotifyTargetChanged(ActorId actorId, GroupTarget target);
    void NotifyMemberRemoved(ActorId actorId);
    void Tick(GroupContext context);
    SlimeGroupSnapshot GetSnapshot();

## 5. 不负责的内容

- 不保存成员的生命、状态和位置可写副本；
- 不替代 SlimeActor 的决策树；
- 不直接执行移动、碰撞、窄路通行和攻击；
- 不直接调用 StatusManager 修改成员；
- 不创建或销毁 SlimeActor。

## 6. 依赖与信息流

    SlimeActor 目标变化
      → SlimeManager
      → SlimeGroupActor 形成或更新
      → 返回群体上下文
      → SlimeActor DecisionTree / StateMachine

MovementSystem 和 PassageSystem 提供队列、通行和空间事实；StatusManager 负责群体策略产生的最终增益、减益或控制效果。

## 7. 关键不变量

1. 每个存活史莱姆最多属于一个群体。
2. 群体解散后，成员恢复普通个体决策，不遗留目标和策略修正。
3. 群体关系不能关闭成员碰撞体或绕过地图与窄路规则。
4. 重开或卸载时，群体、计时器和成员订阅一起清理。

## 8. 待确认项

- 群体形成是立即创建、周期检查还是由个体发起；
- 相同目标群体是否允许合并；
- 队列、阵型和窄路通行的具体规则；
- 群体策略的选择来源及与成员增益的叠加方式；
- 成员害怕、冻结、死亡或离开后的策略变化。

