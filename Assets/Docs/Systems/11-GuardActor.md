# 11 GuardActor 守卫个体设计

系统编号：ACTOR-06
系统名称：GuardActor
默认命名空间：SlimeBattle.Actors.Guards
建议实现目录：Assets/Game/Scripts/Actors/Guard/
生命周期：由当前 LevelContext 的 GuardManager 创建和销毁
状态：基础行为已有策划方向，控制方式和数量待确认

## 1. 职责

GuardActor 表示一个守卫个体，组合通用 ActorCore、ActorDecisionTree 和 ActorStateMachine，围绕护送目标完成待命、防守、移动和攻击行为。

- 保存守卫配置和本局运行时数据；
- 根据护送目标、敌人战斗状态和控制模式产生行动意图；
- 执行移动、攻击、受击、控制和死亡状态；
- 接收来自 InputSystem 或调试工具的语义指令；
- 通过 AttackAction 和 StatusManager 完成攻击效果。

当前方案中，守卫默认围绕护送武器待命，不主动无条件索敌；敌人靠近目标或攻击目标时，守卫才进入防守行为。

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
- ObjectiveSystem 提供护送目标位置和状态；
- EnemyManager 提供敌人查询；
- MapManager 和 MovementSystem 提供空间与移动结果；
- StatusManager 处理守卫的效果和控制状态。

## 6. 关键不变量

1. 守卫的默认攻击行为由护送目标和敌人状态驱动。
2. 外部控制不能绕过 ActorStateMachine 直接写入 Transform。
3. 死亡、暂停和重开后，未执行指令必须失效。
4. 守卫的攻击和受击结果统一经过 StatusManager。

## 7. 待确认项

- 玩家实际控制对象是否为守卫；
- 守卫初始数量和编队规则；
- 守卫是否只承担护送目标的防守；
- 守卫与敌人的攻击、移动和状态规则复用程度。

