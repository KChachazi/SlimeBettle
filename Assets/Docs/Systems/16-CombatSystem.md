# 16 CombatSystem 战斗系统设计

系统编号：`COMBAT-02`  
系统名称：`CombatSystem`  
默认命名空间：`SlimeBattle.Combat`  
建议实现目录：`Assets/Game/Scripts/Combat/`  
生命周期：当前关卡上下文生命周期  
状态：核心战斗边界已确定，目标优先级和数值待确认

## 1. 职责

- 接收角色或机关产生的攻击意图；
- 校验攻击者、目标关系、攻击时机和攻击间隔；
- 判断攻击范围、命中区域和命中结果；
- 把已经确认的命中转换为 `EffectRequest` 提交给 `StatusManager`；
- 发布攻击和命中结果。

## 2. 不负责的内容

- 不直接扣除目标生命；
- 不决定目标的最终死亡结果；
- 不负责动画、音效和特效是否播放；
- 不让攻击动画回调绕过战斗系统直接产生伤害；
- 不替 AI 选择长期目标。

## 3. 核心数据

```text
AttackId
AttackConfig
AttackRequest
AttackRuntimeData
HitQuery
HitResult
AttackSnapshot
TargetPolicy
CombatStateSnapshot
CriticalHitRule
```

## 4. 必须接口

```csharp
AttackId SubmitAttack(AttackRequest request);
bool CancelAttack(AttackId attackId, CancelReason reason);
bool TryGetAttackSnapshot(AttackId attackId, out AttackSnapshot snapshot);
HitResult ResolveHit(HitQuery query);
```

`ResolveHit` 只返回命中事实；伤害、击退和易伤等效果由 `StatusManager` 统一处理。

## 4.1 当前策划规则接入

- 史莱姆产生攻击意图并进入攻击范围后进入战斗状态；
- 暴躁史莱姆可以把索敌范围内的人类单位纳入攻击目标；
- 普通敌人通常攻击距离最近的单位且不会逃跑；
- 守卫只在护送武器附近出现战斗状态的魔物，或敌人正在攻击武器时发起攻击；
- 寒冷史莱姆的攻击可能附加 1 秒冻结，炙热史莱姆的攻击可能附加燃烧；
- 暴击将该次伤害翻倍，但暴击来源、概率和是否受易伤影响仍由配置确定。

这些是目标筛选和效果提交的规则输入，不允许由动画事件或角色表现脚本直接结算伤害。

## 5. 对外事件

```text
AttackStartedEvent
AttackHitEvent
AttackMissedEvent
AttackCompletedEvent
```

## 6. 依赖

- `ActorCore` 提供攻击者和目标身份；
- `MapSystem` 提供空间查询；
- `ConfigSystem` 提供攻击配置；
- `StatusManager` 接收命中效果；
- `MovementSystem` 提供位置和接触事实。
- `SlimeGroupSystem` 提供群体战斗策略修正和目标倾向；
- `ObjectiveSystem` 作为护送武器目标的效果接收者。

## 7. 关键不变量

1. 一个攻击只产生一次有效命中结算，除非配置明确允许多段命中。
2. 已销毁或死亡的攻击者和目标不能继续产生有效命中。
3. 战斗系统只提交效果请求，不直接修改目标的最终属性。

## 8. 待确认项

- 是否存在传统攻击系统；
- 史莱姆、敌人和守卫是否都能攻击；
- 命中检测采用触发器、距离查询还是自定义区域；
- 是否需要攻击前摇、后摇和可打断规则。
- 守卫“攻击/受伤后全体转为攻击”的持续时间和范围；
- 普通敌人固定活动范围、索敌范围和人类优先规则；
- 冻结、燃烧、暴击与群体策略的叠加顺序。

