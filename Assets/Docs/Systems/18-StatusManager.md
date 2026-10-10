# 18 StatusManager 生命与状态管理器

系统编号：STATUS-01<br />
系统名称：`StatusManager`<br />
默认命名空间：`SlimeBattle.Status`<br />
实现目录：`Assets/Game/Scripts/Status/`<br />
生命周期：当前 `LevelContext` 生命周期<br />
状态：P0 效果结算、状态运行时和地图区域接入已实现；具体玩法配置由状态资产提供

源码按职责放在 `Configuration/`、`Effects/`、`Runtime/`、`Snapshots/` 和 `Events/` 子目录。各类型继续使用 `SlimeBattle.Status` 命名空间，文件夹调整不改变调用方 API。

## 1. 定位与所有权

`StatusManager` 是当前关卡的统一效果入口，校验来源与目标，按配置结算生命、状态、控制和数值修正，并发布结果事件。

- `ActorCore` 持有权威生命值、存活状态、控制模式和活动 `StatusId`；`StatusManager` 不保留第二份可写生命值。
- `StatusManager` 持有关卡内状态计时、叠层、区域来源、周期效果与修正规则。
- `MapManager` 持有地图区域成员关系并发布进入/离开事实；`StatusManager` 消费这些事实，将区域配置关联到 `StatusConfig`。
- `MovementSystem` 执行位移；通过提供器查询 `StatusManager` 的移动倍率和定身标记。
- `ActorStateMachine` 通过行动端口读取攻击速度修正，命中时提交伤害请求。

## 2. 职责与范围

- 结算 `Damage`、`Heal`、`Status` 和 `Control` 请求；
- 统一验证目标存活、来源可用性和可选阵营关系；
- 按 `StatusConfig` 管理状态免疫、互斥、持续时间、叠层、周期效果与控制；
- 组合移速、攻速、伤害造成和伤害承受倍率；
- 响应 `MapAreaEnteredEvent` / `MapAreaExitedEvent`，处理环境状态及离区宽限；
- 发布效果、生命、状态、控制和移动修正事件；
- 在 Actor 死亡、销毁、关卡暂停和卸载时清理或冻结运行时状态。

当前不建立独立 `CombatSystem`。命中与攻击时机仍属于 `ActorStateMachine` / `AttackAction`。通用请求只支持 Actor 目标；护送武器等非 Actor 目标须由目标系统提供自己的已结算入口。击退方向目前只在 `EffectRequest` 中保留上下文，MovementSystem 尚无冲量接口，因此不会产生击退位移。

## 3. 关键数据

```text
StatusConfig
EffectRequest / EffectRequestId
StatusApplicationRequest
ResolvedEffect / EffectApplyResult
StatusId / StatusEntrySnapshot / StatusSnapshot
HealthSnapshot
MovementModifierSnapshot
StatusTickContext
```

`StatusConfig` 由 `ConfigRegistry` 按 `StatusId` 唯一解析。重复的 `StatusId` 或缺失配置会被拒绝，不会静默选取某一项。配置包含持续时间、区域离开宽限、移动/攻击/伤害倍率、定身和控制、叠层策略、抵消状态、Actor 类型免疫及周期伤害/治疗。

## 4. 效果请求与结算

主要入口：

```csharp
EffectApplyResult ApplyEffect(EffectRequest request);
bool ApplyStatus(StatusApplicationRequest request);
```

每次请求必须携带有效 `EffectRequestId`。同一关卡内已处理的请求 ID 会被记录至关卡释放，重复请求返回 `Duplicate`，不会再次修改 Actor。请求在目标、来源、配置或暂停检查中失败时发布 `EffectRejectedEvent`。

- 目标 Actor 必须存在且存活；来源可选，关系策略为 `Any` 时环境/持续效果可无 Actor 来源。
- `HostileOnly`、`FriendlyOnly`、`SelfOnly` 需要解析来源并验证阵营或身份；要求存活的来源死亡后不能继续提交直接效果。
- `Damage` 计算：`基础伤害 × 来源伤害造成倍率 × 目标伤害承受倍率`。治疗当前不受这两个伤害倍率影响。
- 结果由目标 `IEffectReceiver` / `ActorCore` 应用；StatusManager 比较前后快照并发布 `HealthChangedEvent`。
- 结果区分已应用/无变化、拒绝、目标不可用、免疫和重复。一次请求只在首次处理时产生一次结算结果。

`RequestRound`、`ContextConfigId` 和方向用于关联来源上下文；当前不依据它们做命中、护甲、回合规则或击退计算。

## 5. 状态配置与运行规则

### 5.1 生命周期

- `StatusApplicationRequest.DurationOverrideSeconds == -1` 使用配置持续时间；正数使用覆盖时长。
- 配置持续时间或覆盖时长为 `0` 表示不按时间到期；区域状态则由区域来源决定存续。
- 状态由直接来源和一个或多个 `MapAreaId` 来源共同维持。移出一个重叠区域不会清除其他区域或直接来源仍在维持的状态。
- 最后一个区域来源退出且没有直接来源时，使用 `AreaExitGraceSeconds`；宽限为 `0` 时立即移除，进入同状态区域会取消宽限。
- Actor 死亡或销毁时清除其运行时状态；关卡暂停时停止状态与周期计时，恢复后继续。

### 5.2 叠层和互斥

同一 `StatusId` 仅有一份运行时条目。区域来源按区域 ID 去重，同一个状态因多个区域维持时仍只贡献一份倍率。直接施加的叠层共用一个刷新后的持续时间，到期时一并结束；若地图区域仍维持该状态，则回到单层区域状态。

| 策略 | 直接重复施加 | 叠层数 |
| --- | --- | --- |
| `IgnoreIfActive` | 保持当前持续时间 | 不增加 |
| `RefreshDuration` | 刷新持续时间 | 不增加 |
| `Replace` | 替换状态配置实例并刷新持续时间 | 回到 1 |
| `AddStack` | 增加至 `MaximumStacks` 并刷新持续时间 | 逐层增加 |

叠层状态的倍率按该倍率的叠层数次方计算；周期效果的每次数值乘以叠层数。区域重叠只记来源数量，不额外叠层。配置的 `CounteractsStatusId` 会在施加新状态时移除对应状态。

### 5.3 数值与控制

- 同时活动的不同状态，其移速、攻速、伤害造成和伤害承受倍率分别相乘。
- `IsRooted` 在任一活动状态为真时生效；`ControlMode.Controlled` 在任一状态提供时控制 Actor。最后一个控制状态移除后恢复到应用控制前的模式。
- 零移速倍率等效不能移动；伤害倍率为零使伤害无生命变化。
- 周期效果只允许配置 `Damage` 或 `Heal`，间隔必须大于零。单次 Tick 最多补结 16 次周期，避免长帧后无限追赶。
- 免疫按 `ActorType` 判断。免疫的状态请求不修改 Actor，并返回 `Immune`。

### 5.4 地图区域接入

`MapAreaSnapshot.StatusEffectConfigId` 指向一个 `StatusConfig.ConfigId`。Actor 进入区域后，StatusManager 将其状态 ID 与 `MapAreaId` 建立来源关系；离开时移除该来源并按配置处理宽限。地形移速倍率仍由 MapManager/MovementSystem 负责，与 Actor 状态倍率相乘。

因此，寒冷/温暖区域的具体结果通过状态资产表达：区域资产关联相应状态配置，状态配置再定义减速、周期伤害或其他效果。缺少或存在歧义的状态配置会发出拒绝事件，不会写死在 MapManager 中。

## 6. 查询与移除接口

```csharp
bool RemoveStatus(ActorId actorId, StatusId statusId,
    StatusRemoveReason reason = StatusRemoveReason.Dispelled);
bool TryGetHealthSnapshot(ActorId actorId, out HealthSnapshot snapshot);
bool TryGetStatusSnapshot(ActorId actorId, out StatusSnapshot snapshot);
MovementModifierSnapshot GetMovementModifiers(ActorId actorId);
float GetAttackSpeedMultiplier(ActorId actorId);
float GetDamageDealtMultiplier(ActorId actorId);
float GetDamageTakenMultiplier(ActorId actorId);
void Tick(StatusTickContext context);
```

快照只读并反映 Actor 当前权威数据及本系统跟踪的配置状态。移除、过期、抵消、离区宽限结束、死亡和销毁会带上对应 `StatusRemoveReason`。

## 7. 事件

| 事件 | 含义 |
| --- | --- |
| `EffectAppliedEvent` | 请求已被接受，含结算效果与结果 |
| `EffectRejectedEvent` | 请求未应用，含拒绝/不可用/免疫/重复结果 |
| `HealthChangedEvent` | Actor 生命值发生变化 |
| `StatusAddedEvent` / `StatusChangedEvent` | 状态新增或运行时来源、时长、叠层变化 |
| `StatusRemovedEvent` / `StatusExpiredEvent` | 状态移除或按时到期 |
| `MovementModifierChangedEvent` | 移速倍率或定身状态变化 |
| `ActorControlChangedEvent` | Actor 控制模式变化 |

`MapAreaEnteredEvent` / `MapAreaExitedEvent` 由 MapManager 发布并由本系统消费。Actor 所属 Manager 仍负责发布 Actor 死亡/销毁事件。

## 8. LevelContext 集成

`LevelContext.Initialize()` 在 MapManager 后创建 StatusManager，并将其移动修正查询注入 MovementSystem。Actor 行动端口把 `AttackAction` 的伤害请求转交 StatusManager，同时向状态机提供攻速倍率。`LevelContext.Tick()` 驱动状态计时；暂停、恢复和 Dispose 与其他关卡系统同步。Actor 在注册到 MovementSystem 前进入所属 Manager 的索引，使初始区域事件能够解析到 Actor。

## 9. 关键不变量

1. 生命值只能经 StatusManager 结算并由 ActorCore 保存。
2. 状态计时、免疫、堆叠、来源和修正规则不散落在 Actor、Manager 或地图脚本中。
3. 同一 `EffectRequestId` 在当前关卡生命周期内不重复结算。
4. 地图区域事件表达成员关系变化；地图系统不直接写入 Actor 状态。
5. 状态修正只提供规则输入；MovementSystem 仍独占实际位移。
6. 关卡暂停冻结计时，死亡、销毁和卸载会清理本关运行态与订阅。

## 10. 尚未接入的玩法

- 击退需要 MovementSystem 提供确定的冲量/受击接口后再消费 `EffectRequest.Direction`。
- 受击闪烁、动画、音效和 UI 由表现/UI 系统观察事件实现。
- 地图落物、阻挠点、峡谷坠落、区域随机事件由地图/机关等系统负责；本系统只结算其提交的 Actor 效果。
- 状态资产、具体数值、状态优先级和免疫表仍由玩法配置确认；当前没有预设寒冷、炙热等状态数值。
