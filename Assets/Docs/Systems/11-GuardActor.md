# 11 GuardActor 守卫个体设计

系统编号：ACTOR-06
系统名称：GuardActor
默认命名空间：SlimeBattle.Actors.Guards
建议实现目录：Assets/Game/Scripts/Actors/Guard/
生命周期：由当前 LevelContext 的 GuardManager 创建和销毁
状态：护送武器守卫点、攻击目标优先级、攻击和生命周期已有运行时实现；当前共用 `CFG_Guard` 配置 5 名守卫

## 1. 职责

GuardActor 表示一个守卫个体，组合通用 ActorCore、ActorDecisionTree 和 ActorStateMachine，围绕护送目标完成待命、防守、移动和攻击行为。

- 保存守卫配置和本局运行时数据；
- 根据护送目标、敌人战斗状态和控制模式产生行动意图；
- 执行移动、攻击、受击、控制和死亡状态；
- 接收来自 InputSystem 或调试工具的语义指令；
- 通过 AttackAction 和 StatusManager 完成攻击效果。

当前 `FirstLevel` 与 `Castle` 均启用护送武器。守卫的站岗位置是武器位置加 `GuardSpawnRequest.EscortOffset`；该偏移来自配置的出生偏移，生成点受阻时会以最近可通行点重新计算偏移。守卫离岗距离超过 `LeashDistance` 时先返回站岗点；追击目标也必须处于站岗点的 leash 范围内。守卫不受食物、熔炉引导或史莱姆状态影响。

守卫决策按以下优先级从高到低选择目标：

1. 正在攻击护送武器的敌人；
2. 正在攻击任一守卫（包括当前守卫）的敌人，或正在攻击该守卫的史莱姆；
3. 靠近护送武器且当前攻击决策已成立的敌人；
4. 没有上述目标时，回到护送武器周围的 `EscortDistance` 位置。

敌人的公开快照提供当前目标、攻击决策事实和行动状态；攻击间隔冷却不会清除“当前决策为攻击”的事实。守卫目标筛选只使用可见、存活、敌对且位于 leash 范围内的目标；史莱姆只会作为报复目标进入第二优先级。实际伤害由 `ActorStateMachine` 经 `StatusManager` 结算。

## 2. 核心数据

    GuardConfig
    GuardRuntimeData
    GuardSnapshot
    GuardControlMode
    GuardCommand
    GuardTargetPolicy

## 3. 必须接口

    void Initialize(GuardConfig config, ActorId actorId);
    void Tick(ActorContext context);
    GuardSnapshot GetSnapshot();
    bool SubmitCommand(GuardCommand command);

SubmitCommand 只提交语义指令，不允许调用方直接修改位置、生命和状态。

## 4. 不负责的内容

- 不解析触摸、鼠标和键盘原始输入；
- 不管理全部守卫的集合和生成销毁；
- 不直接修改史莱姆或敌人的生命；
- 不实现通用状态机和状态效果规则；
- 不由 UI 直接写入守卫 Transform。

## 5. 依赖

- GuardManager 提供生命周期和查询；
- InputSystem 提供设备无关的语义指令；
- MechanismSystem 在兼容武器链路启用时提供护送武器位置和状态；
- EnemyManager 提供敌人查询；
- MapManager 和 MovementSystem 提供空间与移动结果；
- StatusManager 不向守卫应用食物、温暖或史莱姆状态修正；守卫的基础生命和战斗受击入口仍需与 Actor 效果契约联调。

## 6. 关键不变量

1. 守卫的默认攻击行为由关卡配置的防守目标和敌人状态驱动；护送武器只是兼容方案之一。
2. 外部控制不能绕过 ActorStateMachine 直接写入 Transform。
3. 死亡、暂停和重开后，未执行指令必须失效。
4. 守卫的攻击和常规受击结果遵守已确认的 Actor 效果契约，但不消费食物、温暖或状态修正。

## 7. 待确认项

- 若后续关卡不再使用护送武器，需要另行定义守卫的站岗目标；
- 目标优先级当前按 `GuardDecision` 与感知快照字段执行，若策划更新需同步修改代码和本文。

