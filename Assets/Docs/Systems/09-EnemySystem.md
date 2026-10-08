# 09 EnemySystem 敌人系统设计

系统编号：`ACTOR-03`  
系统名称：`EnemySystem`  
默认命名空间：`SlimeBattle.Enemies`  
建议实现目录：`Assets/Game/Scripts/Combat/`  
生命周期：当前关卡上下文生命周期  
状态：基础行为已有策划方案，特殊敌人与 Boss 待确认

## 1. 职责

- 创建、初始化、维护和销毁敌人实例；
- 保存敌人的本局运行时数据；
- 为敌人接入决策树、状态机、移动和战斗；
- 响应 `StatusManager` 产生的效果结果；
- 提供敌人状态查询和死亡通知。

当前普通敌人行为方案：

- 在关卡配置的固定活动范围内移动；
- 目标进入索敌范围后，优先攻击距离自身最近的单位；
- 普通敌人通常不逃跑，进入战斗后持续到死亡；
- 部分敌人的人类优先目标规则待后续敌人类型设计确定。

第一场景普通敌人的初始数值方案为攻击力 70、生命值 800、移速 300、攻击间隔 1 秒；后续场景强度提升 15%—20% 仅是策划文档中的方向，必须通过 `EnemyConfig` 或关卡配置表达，不能写死在系统代码中。

## 2. 不负责的内容

- 不实现通用决策树和状态机；
- 不直接修改史莱姆生命；
- 不决定所有机关的规则；
- 不让 UI 直接写入敌人数据；
- 不把敌人设计配置和敌人本局状态混在一起。

## 3. 核心数据

```text
EnemyConfig
EnemyRuntimeData
EnemySpawnRequest
EnemySnapshot
EnemyControlMode
EnemyTargetPolicy
EnemyActivityBounds
```

## 4. 必须接口

```csharp
ActorId SpawnEnemy(EnemySpawnRequest request);
bool RequestDespawn(ActorId actorId, DespawnReason reason);
bool TryGetSnapshot(ActorId actorId, out EnemySnapshot snapshot);
IReadOnlyList<EnemySnapshot> GetAliveSnapshots();
EffectApplyResult ApplyEffect(ActorId actorId, ResolvedEffect effect);
```

## 5. 对外事件

```text
EnemySpawnedEvent
EnemyStateChangedEvent
EnemyDiedEvent
EnemyDespawnedEvent
```

## 6. 依赖

- `ConfigSystem`；
- `ActorCore`；
- `DecisionTreeSystem` 与 `StateMachineSystem`；
- `MovementSystem`；
- `CombatSystem`；
- `StatusManager`；
- `MapSystem` 提供固定活动范围和索敌空间查询；
- `ObjectiveSystem` 提供护送武器目标状态（当敌人目标策略需要时）。

## 7. 关键不变量

1. 敌人只能由 `EnemySystem` 创建和销毁。
2. 敌人死亡后不能继续提交战斗和移动请求。
3. 阵营和目标关系由统一的角色/战斗查询决定，不能由每个敌人脚本各自解释。

## 8. 待确认项

- 是否存在敌人；
- 敌人的种类、目标和攻击方式；
- 是否与守卫共用部分行为；
- 特殊敌人和 Boss 的行为逻辑；
- 部分敌人是否优先攻击人类；
- 敌人是否需要独立的生成波次系统；
- “怪物受到场景元素影响”的具体范围和效果。

