# 26 Core 关卡结果规则设计（原 ObjectiveSystem 文档）

系统编号：`CORE-04`  
文档称谓：`CoreLevelOutcomeRules`（仅用于说明职责，不冻结最终类名）
默认命名空间：`SlimeBattle.Core`
建议实现目录：`Assets/Game/Scripts/Core/`
生命周期：随当前 `LevelContext` 创建、订阅、结算和销毁
状态：结果规则归属已确认；代码迁移和最终类型名待实现

> 为避免本次文档更新破坏既有链接，文件名暂保留 `26-ObjectiveSystem.md`。新代码不得继续把 `ObjectiveSystem` 作为独立 LevelContext 系统，也不得在 `Assets/Game/Scripts/Objectives/` 新增护送武器运行时逻辑。

## 1. 定位

Core 关卡结果规则组件只聚合当前关卡已经发生的结果事件，并确认本局成功或失败。它不是一个拥有玩法对象的独立系统，不保存武器、史莱姆、守卫或机关的运行时副本。

首关规则已经确认：

1. `WeaponReachedGoalEvent` 触发成功；
2. `WeaponDestroyedEvent` 触发失败；
3. `SlimeDepletedEvent`（最后一只史莱姆确认死亡）触发失败；
4. 同一局成功或失败只确认一次；
5. `LevelManager` 只负责接收最终结果并推进结算、重开或离开流程，不复制上述规则。

武器的生命、位置、运输载体、固定路线进度和抵达状态完全属于 `MechanismSystem`。

## 2. 职责

- 订阅当前 `LevelContext` 的 `LevelEventBus`；
- 记录本局结果阶段、结果原因和只读结果快照；
- 接收 `WeaponDestroyedEvent`、`WeaponReachedGoalEvent` 和 `SlimeDepletedEvent`；
- 根据已确认关卡规则确认一次 `LevelSucceededEvent` 或 `LevelFailedEvent`；
- 在暂停、重开和卸载时清理订阅与本局结果状态；
- 为 `LevelManager`、UI 和测试提供只读结果查询。

## 3. 不负责的内容

- 不创建、保存或修改护送武器运行时数据；
- 不创建或统计史莱姆、敌人和守卫的运行时集合；
- 不直接计算伤害、移动、运输、碰撞、状态或机关触发；
- 不判断敌人是否攻击武器；
- 不执行场景加载、重开、下一关跳转或 UI 动画；
- 不从 UI、动画或场景对象的显示状态推断成功/失败。

## 4. 核心数据

```text
LevelResultPhase
LevelResultReason
LevelResultSnapshot
LevelResultConfig（如后续需要按关卡配置规则）
```

结果组件最多保存：当前阶段、是否已确认、原因、确认时间/帧和关联的只读 ID。不得保存 `WeaponRuntimeData`、`SlimeRuntimeData` 或其他对象的第二份可写状态。

## 5. 生命周期接口候选

以下是代码迁移时需要登记到全局 API 表的最小生命周期和查询边界；具体 C# 类型名在实现前仍需确认：

```csharp
void Initialize(LevelEventBus eventBus, LevelResultConfig config);
LevelResultSnapshot GetSnapshot();
void Dispose();
```

结果确认由组件内部消费 EventBus 完成，不建议向 `LevelManager` 暴露“手动提交成功/失败”的公共命令。若后续需要调试注入，必须单独标记为测试接口。

## 6. 对外事件

```text
LevelSucceededEvent
LevelFailedEvent
```

输入事实事件由各自所有者发布：

| 事件 | 发布者 | 含义 |
| --- | --- | --- |
| `WeaponDamagedEvent` | `MechanismSystem` | 护送武器已经完成一次受击/伤害结算 |
| `WeaponDestroyedEvent` | `MechanismSystem` | 护送武器生命归零并确认摧毁 |
| `WeaponReachedGoalEvent` | `MechanismSystem` | 护送武器到达指定终点 |
| `SlimeDepletedEvent` | `SlimeManager` | 当前关卡最后一只史莱姆已经确认死亡 |
| `LevelSucceededEvent` | Core 结果规则组件 | 本局成功已经确认 |
| `LevelFailedEvent` | Core 结果规则组件 | 本局失败已经确认 |

结果事件只携带只读快照或结果原因，不携带可写的运行时对象。

## 7. 依赖与信息流

```text
MechanismSystem
  ├─ WeaponDamagedEvent / WeaponDestroyedEvent / WeaponReachedGoalEvent
SlimeManager
  └─ SlimeDepletedEvent
        ↓ 当前 LevelEventBus
Core 结果规则组件
  ├─ LevelSucceededEvent / LevelFailedEvent
  └─ LevelResultSnapshot
        ↓
LevelManager / UISystem / 测试
```

`GameRoot` 不持有本局 EventBus，也不参与关卡结果判定。`GuardManager` 查询 `MechanismSystem` 的武器快照来执行防守，不通过结果组件获取或修改武器。

## 8. 关键不变量

1. 武器运行时数据只能由 `MechanismSystem` 修改。
2. `WeaponDestroyedEvent`、`WeaponReachedGoalEvent` 和 `SlimeDepletedEvent` 只能由对应所有者发布。
3. Core 结果规则组件每局最多发布一次成功或失败结果。
4. `LevelManager` 不复制结果条件，不从 UI 或表现回调推断结果。
5. `Dispose` 后旧事件、计时器和结果不能作用于新关卡。
6. 如果成功和失败事实在同一更新周期同时到达，优先级仍待确认，不能在各自 PR 中私自决定。

## 9. 待确认项

- 结果组件最终类型名和是否由 `LevelContext` 直接持有字段；
- 成功/失败事实同一帧同时到达时的优先级；
- 多武器、多终点或后续关卡是否需要可配置结果规则；
- 武器初始生命、受击反馈、修复机制和武器伤害入口的最终 API；
- 旧 `ObjectiveConfig`、`ObjectiveRuntimeData` 和 `Objectives/` 目录的删除时机；
- `LevelManager` 订阅 EventBus 还是由结果组件转发到其 C# 事件。

## 10. 验收场景

- 武器生命归零时由 `MechanismSystem` 只发布一次 `WeaponDestroyedEvent`，结果组件只确认一次失败；
- 武器到达指定终点时由 `MechanismSystem` 发布 `WeaponReachedGoalEvent`，结果组件只确认一次成功；
- 最后一只史莱姆死亡时由 `SlimeManager` 发布 `SlimeDepletedEvent`，结果组件只确认一次失败；
- UI、动画或场景重开回调不能重复确认结果；
- `LevelManager` 能消费最终结果并进入结算/重开流程，结果组件本身不加载场景；
- 重开后不存在上一局结果快照、事件订阅或武器运行时引用。
