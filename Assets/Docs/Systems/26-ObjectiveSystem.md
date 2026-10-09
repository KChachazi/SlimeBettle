# 26 ObjectiveSystem 关卡目标与护送武器系统设计

系统编号：`CORE-04`  
系统名称：`ObjectiveSystem`  
默认命名空间：`SlimeBattle.Objectives`  
建议实现目录：`Assets/Game/Scripts/Objectives/`  
生命周期：当前关卡上下文生命周期  
状态：核心边界已确定，具体胜利目标待策划确认

## 1. 核心功能

`ObjectiveSystem` 保存当前关卡的目标状态和护送武器状态，并把已经满足的成功/失败条件通知 `LevelManager`。它是目标结果的唯一确认者；`LevelManager` 只负责根据结果进入结算和重开流程。

当前策划方案明确了两个失败分支：

1. 史莱姆数量降为 0；
2. 被护送武器被摧毁。

P0 默认同时启用两项。`ObjectiveConfig.FailWhenNoSlimes` 可在关卡配置中关闭第一项；MVP `Level_test` 只在护送对象生命归零时失败，武器初始生命为 100，暂不移动或判定抵达终点。其他关卡继续采用默认失败规则。

流程图还提出失败后从当前场景重新开始。由于项目已经确定重开采用重新加载场景，目标系统只发布失败结果，不直接执行场景加载。

## 2. 职责

- 创建并保存当前关卡目标的运行时状态；
- 保存护送武器的生命、位置/阶段和摧毁状态；
- 接收史莱姆死亡、武器受击和目标进度事实；
- 按关卡规则检查失败和成功条件；
- 对同一局的成功或失败只确认一次；
- 为关卡、UI、守卫和调试工具提供只读目标快照。

## 3. 不负责的内容

- 不决定敌人是否攻击武器；
- 不直接计算攻击、伤害、燃烧或击退；
- 不创建史莱姆、敌人或守卫；
- 不执行场景重开、下一关加载或 UI 结算动画；
- 不在尚未确定的情况下假设武器的最终交付点和成功条件。

## 4. 核心数据

```text
ObjectiveId
ObjectiveConfig
ObjectiveRuntimeData
ObjectiveSnapshot
WeaponConfig
WeaponRuntimeData
WeaponSnapshot
ObjectiveResult
ObjectiveResultReason
```

`ObjectiveRuntimeData` 至少包含：目标 ID、当前结果阶段、是否已经结算、失败原因和关联武器 ID。`WeaponRuntimeData` 至少包含：武器 ID、最大/当前生命、护送阶段、位置引用和是否摧毁。

## 5. 必须接口

```csharp
bool TryGetObjectiveSnapshot(ObjectiveId objectiveId, out ObjectiveSnapshot snapshot);
bool TryGetWeaponSnapshot(WeaponId weaponId, out WeaponSnapshot snapshot);
EffectApplyResult ApplyWeaponEffect(WeaponId weaponId, ResolvedEffect effect);
void NotifySlimeCountChanged(int aliveCount);
ObjectiveResult EvaluateResult();
```

`ApplyWeaponEffect` 由 `StatusManager` 在统一效果结算后调用，武器的最终生命和摧毁状态仍由 `ObjectiveSystem` 修改。`NotifySlimeCountChanged` 只提交已发生的存活数量事实，不允许调用方直接写入目标状态；是否用零存活判负由当前 `ObjectiveConfig` 决定。

## 6. 对外事件

```text
ObjectiveStateChangedEvent
WeaponDamagedEvent
WeaponDestroyedEvent
ObjectiveSucceededEvent
ObjectiveFailedEvent
```

成功或失败事件在同一关卡生命周期内最多发布一次。`ObjectiveFailedEvent` 必须携带失败原因，但不携带可写的目标数据。

## 7. 依赖与信息流

- `LevelManager` 创建、暂停、重开和销毁本系统，并消费目标结果；
- `ConfigSystem` 提供 `ObjectiveConfig` 和 `WeaponConfig`；
- `StatusManager` 提供已经结算的武器效果；
- `SlimeManager` 发布史莱姆生成/死亡结果；
- `GuardManager` 查询武器状态并在武器受击时触发守卫行为；
- `UISystem` 读取目标和武器快照并显示失败/成功反馈。

推荐信息流为：攻击命中 → `StatusManager` 结算效果 → `ObjectiveSystem` 更新武器状态 → 发布武器/目标结果 → `LevelManager` 进入结算或重开流程。

## 8. 关键不变量

1. 武器生命只能由 `ObjectiveSystem` 修改。
2. 目标成功/失败只能由 `ObjectiveSystem` 确认一次。
3. `LevelManager` 不复制目标规则，也不从 UI 或动画回调推断结果。
4. 关卡重开后旧目标、武器生命、结算结果和订阅全部失效。
5. 未确定的胜利条件必须返回“未完成/等待条件”，不能被默认当作成功。

## 9. 待确认项

- 护送武器的正式名称、移动方式、交付点和成功判定；
- 武器是否可以移动、是否会被场景元素影响；
- 武器的初始生命、受击反馈和是否存在修复机制；
- 每个关卡是否只有一个武器和一个目标；
- 守卫初始数量，图示中的“10 个守卫”是否为最终规则；
- 史莱姆全部死亡与武器摧毁同时发生时的失败原因优先级；
- 多周目是否修改目标和武器规则。

## 10. 验收场景

- 史莱姆死亡数量变化能被目标系统读取，最后一只死亡时只发布一次失败事件；
- 武器生命降为 0 时只发布一次 `WeaponDestroyedEvent` 和一次失败结果；
- 目标系统不会因为 UI、动画或场景重开回调重复结算；
- 目标失败后 `LevelManager` 能进入结算/重开状态，目标系统本身不直接加载场景；
- 新场景加载后不存在上一局武器生命和目标结果。
