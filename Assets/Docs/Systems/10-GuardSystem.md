# 10 GuardSystem 守卫系统设计

系统编号：`ACTOR-04`  
系统名称：`GuardSystem`  
默认命名空间：`SlimeBattle.Guards`  
建议实现目录：`Assets/Game/Scripts/Combat/`  
生命周期：当前关卡上下文生命周期  
状态：基础行为已有策划方案，控制方式和数量待确认

## 1. 职责

- 创建、维护和销毁守卫实例；
- 保存守卫的本局运行时数据；
- 让守卫接入 AI 或玩家控制模式；
- 处理守卫的移动、战斗、受击和死亡结果；
- 对外提供守卫快照和控制入口。

当前策划方案中，守卫围绕被护送武器待命，不主动攻击；只有靠近武器且处于战斗状态的魔物，或正在攻击武器的敌人，才会触发守卫攻击。单个守卫攻击或受伤后，图示提出所有守卫都会转为攻击，并优先攻击距离自己最近的敌人。守卫也会受到场景元素影响。

## 2. 不负责的内容

- 不解析底层触摸或鼠标输入；
- 不直接修改史莱姆和敌人的生命；
- 不在角色脚本中重复实现状态机；
- 不由 UI 直接改写守卫位置或攻击结果。

## 3. 核心数据

```text
GuardConfig
GuardRuntimeData
GuardSpawnRequest
GuardSnapshot
GuardControlMode
GuardCommand
GuardTargetPolicy
```

## 4. 必须接口

```csharp
ActorId SpawnGuard(GuardSpawnRequest request);
bool RequestDespawn(ActorId actorId, DespawnReason reason);
bool TryGetSnapshot(ActorId actorId, out GuardSnapshot snapshot);
bool SubmitCommand(ActorId actorId, GuardCommand command);
EffectApplyResult ApplyEffect(ActorId actorId, ResolvedEffect effect);
```

## 5. 对外事件

```text
GuardSpawnedEvent
GuardControlModeChangedEvent
GuardStateChangedEvent
GuardDiedEvent
GuardDespawnedEvent
```

## 6. 依赖

- `InputSystem` 或其他指令来源；
- `DecisionTreeSystem` 与 `StateMachineSystem`；
- `MovementSystem`、`CombatSystem` 和 `StatusManager`；
- `ConfigSystem` 与 `MapSystem`；
- `ObjectiveSystem` 提供护送武器位置和受击状态；
- `EnemySystem` 提供敌人战斗状态和目标查询；
- `MechanismSystem`/`StatusManager` 提供场景元素与状态效果结果。

## 7. 关键不变量

1. 守卫的默认行为由护送目标和敌人战斗状态驱动，不会无条件主动索敌。
2. 守卫只能通过 `CombatSystem`/`StatusManager` 产生攻击和受击结果。
3. 守卫死亡、关卡暂停和重开时，未执行指令必须失效。
4. 输入系统不能绕过系统边界直接操作守卫 Transform；`SubmitCommand` 当前只保留为调试或未来玩法入口。

## 8. 待确认项

- 玩家操作对象是否为守卫；
- 守卫初始数量，图示提出的 10 个是否为最终值；
- 守卫是否只作为护送武器的防守单位存在；
- 守卫和敌人的攻击、移动、状态规则是否共享。

