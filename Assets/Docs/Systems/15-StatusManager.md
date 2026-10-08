# 15 StatusManager 效果与状态系统设计

系统编号：`COMBAT-01`  
系统名称：`StatusManager`  
默认命名空间：`SlimeBattle.Combat.Status`  
建议实现目录：`Assets/Game/Scripts/Combat/`  
生命周期：当前关卡上下文生命周期  
状态：核心架构，当前策划状态规则已登记，结算优先级待确认

## 1. 职责

- 接收并统一处理伤害、治疗、击退、易伤、控制和其他状态效果；
- 校验来源、目标、阵营、时机和目标有效性；
- 按确定顺序计算效果叠加、倍率、持续时间和抵抗；
- 保存需要持续存在的状态运行时数据；
- 将最终效果结果提交给目标所属角色系统；
- 发布已应用、被拒绝、过期和移除结果。

`StatusManager` 拥有状态效果的处理和持续状态记录，但不拥有史莱姆、敌人或守卫的最终生命、种类和存活数据。

## 2. 不负责的内容

- 不直接写入角色生命字段；
- 不决定攻击是否命中；
- 不替代 `CombatSystem` 选择攻击目标；
- 不控制 UI、特效和动画；
- 不接受表现层伪造的伤害结果。

## 3. 核心数据

```text
EffectRequest
ResolvedEffect
EffectType
DamageRequest
KnockbackRequest
StatusApplicationRequest
StatusRuntimeData
StatusSnapshot
EffectApplyResult
SlimeStatusId
StatusModifier
DamageOverTimeRule
```

统一效果请求至少应携带：`SourceActorId`、`TargetActorId`、效果类型、基础数值、持续时间或方向、来源上下文和请求有效性信息。

## 3.1 当前策划状态规则登记

以下规则来自更新后的策划文档，数值是当前方案，不代表最终实现常量：

| 状态/效果 | 当前规则 | 结算边界 |
| --- | --- | --- |
| 饥饿 | 超过 90 秒没有进食；攻击力降低 10%；每 10 秒有 20% 概率变为暴躁；进食后解除 | 正文规则与图示的 90/120/150 秒分段方案冲突 |
| 暴躁 | 攻击力、移速提升 10%；可能攻击附近人类；食物或熔炉引导可消除 | 触发来源和消除优先级待确认 |
| 害怕 | 移速提升 20%；随机行动；暂时不受引导源影响；10 秒后解除 | 生命阈值逃避和同伴死亡触发来自图示 |
| 寒冷 | 移速、攻速降低 20%；攻击使敌人冻结 1 秒 | 温暖源或离开低温地区 20 秒后解除 |
| 炙热 | 生命值减少 30%；攻击使敌人燃烧；死亡时对半径 150 px 内敌人造成攻击力 15% 的伤害 | 离开炎热地区或进入低温地区 20 秒后解除 |
| 冻结 | 暂停当前所有行动和状态 | 优先级高于普通行动，具体是否暂停持续伤害待确认 |
| 燃烧 | 每秒受到最大生命值 2% 伤害，持续 5 秒 | 被冻结或处于低温地区时解除 |
| 暴击 | 本次造成的伤害翻倍 | 由攻击/群体策略提供触发事实 |

寒冷与炙热相互抵消：同时进食寒冷和炙热物品，或在一种温度状态下进食相反温度物品时，当前方案是两种状态都不保留。该规则的表现和状态事件顺序待确认。

## 4. 必须接口

```csharp
EffectRequestId SubmitEffect(EffectRequest request);
EffectApplyResult ResolveEffect(EffectRequestId requestId);
bool ApplyStatus(StatusApplicationRequest request);
bool RemoveStatus(ActorId actorId, StatusId statusId, StatusRemoveReason reason);
bool TryGetStatusSnapshot(ActorId actorId, out StatusSnapshot snapshot);
```

`ResolveEffect` 的具体同步/异步形式待更新循环方案确定。对外必须能区分已应用、被拒绝、目标失效、等待和取消。

## 5. 对外事件

```text
EffectAppliedEvent
EffectRejectedEvent
StatusAddedEvent
StatusRemovedEvent
StatusExpiredEvent
ActorControlChangedEvent
```

事件表示效果或状态结果已经发生，不表示攻击请求刚刚提交。

## 6. 依赖

- `ConfigSystem` 提供状态效果配置；
- `ActorCore` 提供目标身份和阵营；
- 史莱姆、敌人和守卫系统提供目标效果接收接口；
- `MovementSystem` 接收击退或控制后的移动请求；
- `CombatSystem` 提供命中后的效果请求。
- `SlimeSystem` 提供进食、环境区域、同伴死亡和状态读取等领域事实；
- `SlimeGroupSystem` 提供群体策略造成的修正或触发条件；
- `ObjectiveSystem` 接收护送武器的最终效果结果。

## 7. 关键不变量

1. 同一效果不能被多个系统重复结算。
2. 目标已经死亡、销毁或关卡已重开时，旧效果不能生效。
3. 目标的生命最终由目标所属系统修改。
4. 伤害、击退和易伤规则不能分别散落在机关、敌人和史莱姆脚本中。

## 8. 待确认项

- 伤害和状态的结算顺序；
- 易伤是否可叠加；
- 击退与移动/碰撞的关系；
- 控制、停顿和受击硬直是否属于状态效果；
- 是否需要效果优先级和抗性。
- 饥饿正文与图示的分段阈值采用哪一套；
- 状态是互斥、可叠加还是分为行动状态与效果状态两层；
- 冻结是否暂停燃烧等持续效果；
- 炙热死亡范围伤害以死亡前攻击力还是死亡时修正后的攻击力计算；
- “生命值减少 30%”是当前生命扣除、最大生命修正还是伤害倍率。

