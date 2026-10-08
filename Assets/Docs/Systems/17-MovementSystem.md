# 17 MovementSystem 移动系统设计

系统编号：WORLD-01
系统名称：MovementSystem
默认命名空间：SlimeBattle.World.Movement
建议实现目录：Assets/Game/Scripts/World/Movement/
生命周期：当前 LevelContext 生命周期
状态：实现待选

## 1. 职责

- 接收 ActorStateMachine 产生的移动意图；
- 查询 StatusManager 提供的移速、定身、减速和其他移动修正；
- 执行实际位置变化、停止、转向以及后续确定的移动动作；
- 处理阻挡、碰撞、接触和移动失败；
- 向请求方报告移动结果。

移动修正由 StatusManager 管理，但位置变化、碰撞和空间占用由 MovementSystem 执行。

## 2. 信息流

    Actor / Input / AI
      → MovementSystem.SubmitMovement
      → StatusManager.GetMovementModifiers(actorId)
      → MapManager 查询边界和可占用
      → 执行位置变化
      → 发布移动结果

StatusManager 不直接修改 Actor 的 Transform，Actor 也不绕过 MovementSystem 写入正式位置。

## 3. 不负责的内容

- 不决定角色为什么移动；
- 不计算最终伤害和状态效果；
- 不决定攻击目标；
- 不直接改变机关冷却和窄路队列；
- 不由镜头是否跟随决定移动是否执行。

## 4. 核心数据

    MovementRequest
    MovementState
    MovementResult
    ContactInfo
    MovementMode
    MovementModifierSnapshot

## 5. 公共接口

    MovementRequestId SubmitMovement(MovementRequest request);
    bool CancelMovement(MovementRequestId requestId, CancelReason reason);
    bool TryGetMovementState(ActorId actorId, out MovementState state);
    bool TryGetContactInfo(ActorId actorId, out ContactInfo contactInfo);

## 6. 依赖

- MapManager 提供边界、可占用和区域查询；
- ActorCore 提供身份和当前位置语义；
- StatusManager 提供移动修正和控制状态；
- PassageSystem 与 MechanismSystem 消费移动产生的事实。

## 7. 实现选项

可以选择 Rigidbody2D、运动学移动或自定义空间模拟。选择依据是推挤、碰撞、窄路和移动端性能测试，不在接口层提前绑定具体实现。

## 8. 关键不变量

1. 同一 Actor 的互斥移动请求不能同时写入位置。
2. 取消、死亡、暂停和重开时，正在执行的移动必须可停止。
3. 移动系统报告碰撞事实，但不自动把碰撞解释成伤害或机关效果。

