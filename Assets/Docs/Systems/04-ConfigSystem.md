# 04 ConfigSystem 配置与 ScriptableObject 系统设计

系统编号：`DATA-01`  
系统名称：`ConfigSystem`  
主要协调类：`ConfigRegistry`  
默认命名空间：`SlimeBattle.Data`  
建议实现目录：`Assets/Game/Scripts/Data/Config/`；配置资产位于 `Assets/Game/Configs/`  
生命周期：当前关卡上下文生命周期，可由应用级资产提供只读数据  
状态：配置注册、查询与校验已有运行时代码；具体数值以当前 `.asset` 为准

> 2026-10-28 代码核对：当前结果规则由 `LevelConfig.ObjectiveKind` 选择；`ObjectiveConfig` 仍参与引用校验，但其 `FailWhenNoSlimes` 等字段没有传入 `LevelResultRules`。仓库没有 `LevelResultConfig` 类型。初始资源、奖励和守卫数量见当前配置资产，不能以早期候选数值表覆盖。

## 1. 职责

- 从 ScriptableObject 或确定的序列化资产读取设计时配置；
- 按稳定 `ConfigId` 提供只读查询；
- 校验缺失引用、重复 ID 和不合法数值；
- 提供配置对象，供 `LevelManager` 在关卡初始化时交给需要配置的系统；
- 区分配置数据和本局运行时数据。

## 2. 不负责的内容

- 不保存史莱姆当前生命、目标、冷却或队列；
- 不在运行中修改配置资产；
- 不决定某个角色何时行动；
- 不直接实例化所有场景对象；
- 不代替具体系统处理配置的业务规则。

## 3. 核心配置类型

```text
ConfigId
LevelConfig
SlimeConfig
SlimeGroupConfig
ActorConfig
EnemyConfig
GuardConfig
MechanismConfig
PassageConfig
StatusEffectConfig
PlacementMechanismConfig
ObjectiveConfig
EscortWeaponConfig
CampaignResourceConfig
FirstLevelHazardConfig
```

每个配置资产必须拥有稳定的 `ConfigId`。显示名称不能作为跨系统唯一键。

`ActorConfig.DetectionRange` 是目标感知半径（世界单位），必须大于 `AttackRange`。当前测试资产暂填攻击半径 1.2、感知半径 1.5；这是通路测试值，不是策划最终数值，也不含墙体遮挡判定。

`ObjectiveConfig` 保存目标与武器配置引用及旧结果开关；配置注册会校验其引用，但当前 Core 结果运行时只消费 `LevelConfig.ObjectiveKind`，不会读取 `ObjectiveConfig.FailWhenNoSlimes`。不要把旧 `ObjectiveSystem` 字段描述成当前正式关卡胜负条件。

## 3.1 当前策划提出的初始数值

下表来自更新后的策划文档，只作为配置资产的初始值，不能在代码中散落为常量；策划文档已经注明后续会继续调整：

| 配置 | 攻击力 | 最大生命 | 移速 | 攻击间隔 | 其他 |
| --- | ---: | ---: | ---: | ---: | --- |
| `SlimeConfig` | 30 | 500 | 200 | 1 秒 | 单体属性；同屏数量由 `LevelConfig` 决定 |
| `GuardConfig` | 100 | 1000 | 300 | 1.2 秒 | 当前 `CFG_Guard` 配置 5 名守卫及 5 个武器相对偏移 |
| 普通 `EnemyConfig` | 70 | 800 | 300 | 1 秒 | 后续场景强度约提升 15%—20%，待确认 |

史莱姆的饥饿、暴躁、害怕、寒冷、炙热、冻结、燃烧和暴击规则由 `StatusEffectConfig` 或相关规则配置承载。首关暂不启用寒冷和炙热；守卫不受食物、温暖或史莱姆状态影响。图示和正文存在饥饿计时规则差异，统一前不得假设只有一套最终数据。

2026-10-10 确认：`LevelConfig.MaxAliveSlimes` 默认 20，合法范围 1–20，表示同屏同时存活数量；不限制本局累计生成量。此数值不由 `SlimeConfig` 再维护第二份。P0 不做对象池，后续只在真机性能数据表明有必要时考虑。

## 4. 必须接口

```csharp
bool TryGet<TConfig>(ConfigId configId, out TConfig config);
bool TryGetLevel(LevelId levelId, out LevelConfig config);
bool TryGetActor(ConfigId configId, out ActorConfig config);
bool TryGetMechanism(ConfigId configId, out MechanismConfig config);
bool TryGetLevelResult(LevelId levelId, out LevelResultConfig config);
bool TryGetEscortWeapon(ConfigId configId, out EscortWeaponConfig config);
ConfigValidationReport ValidateAll();
```

具体配置类型可以继续细分，但所有查询必须说明找不到配置时的结果，不允许静默返回一份默认配置掩盖错误。

## 5. 对外事件

运行时配置系统默认不发布玩法事件。编辑器校验可以发布或输出 `ConfigValidationFailedEvent`，但不应成为关卡运行的依赖。

## 6. 数据所有权

配置资产属于设计时数据；运行时对象复制或读取配置内容后，必须由角色或机关系统拥有自己的 `RuntimeData`。共享一份 `SlimeConfig` 不等于共享史莱姆生命和行动状态。

当前关卡的配置资产由 `LevelManager` 收集并注册到一份 `ConfigRegistry`；`LevelManager` 调用 `ValidateAll()` 后，将同一份 Registry 交给需要配置的系统。`ConfigRegistry` 负责索引、查询和校验，不自行发现或装配关卡系统。

## 7. 关键不变量

1. 运行时不修改 ScriptableObject 配置资产。
2. 缺失配置、重复 ID 和关键引用为空时必须可诊断。
3. 配置对象不能反向持有当前关卡运行时对象引用。

## 8. 待确认项

- 是否全部使用 ScriptableObject；
- 配置资产的目录和命名后缀；
- 是否需要编辑器批量校验工具；
- 预制体和配置的引用关系。
- 史莱姆群体策略、环境放置预算和护送武器是否分别使用独立配置资产；
- 普通/特殊/Boss 敌人的数值成长方式；
- 图示与正文中冲突数值的最终来源。

