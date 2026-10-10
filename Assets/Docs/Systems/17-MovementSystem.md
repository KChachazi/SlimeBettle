# 17 MovementSystem 移动系统设计

系统编号：WORLD-01
系统名称：MovementSystem
默认命名空间：SlimeBattle.World.Movement
建议实现目录：Assets/Game/Scripts/World/Movement/
生命周期：当前 LevelContext 生命周期
状态：P0 直线移动、地图地形倍率和 StatusManager 移动修正已接入；寻路和其他移动方式尚未接入

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
      → 查询 MovementModifierSnapshot
      → Rigidbody2D 在 FixedUpdate 驱动
      → MapManager 查询场景静态阻挡与当前位置地形修正
      → 完成回调 / MovementBlockedEvent

固定物理步和普通位置同步都会把刚体实际位置交给 MapManager。MapManager 用它计算区域进入/离开事实；MovementSystem 不自行维护 Actor 的区域成员关系。速度计算组合 Actor 基础移速、地图地形修正和 StatusManager 提供的 Actor 状态修正。

ActorStateMachine 的移动请求由 LevelContext 内的行动端口适配器转给 MovementSystem；攻击效果请求由同一适配器转交 StatusManager，并向状态机提供攻速倍率。MovementSystem 在固定物理步中设置动态 Rigidbody2D 的速度，并把刚体位置回报给 Actor。Actor 不直接写入正式位置。

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

提交未注册 Actor、无效目标或零速 Actor 的移动请求会返回无效 ID；ActorStateMachine 将空 ID 视为拒绝，不进入 Moving 状态。

## 6. 对外事件

- `MovementBlockedEvent`：Actor 与当前 LevelContext 绑定的静态碰撞体接触，当前移动请求已停止。
- 成功到达通过当前 Actor 的 `NotifyMovementCompleted` 回调报告；取消由请求方发起，不作为已发生事件广播。

## 7. 依赖方向

- MapManager 提供场景静态阻挡识别、道路/地形/区域和地图点查询、区域转移事件及地形移速倍率；
- ActorCore 提供身份和当前位置语义；
- StatusManager 提供状态移速倍率和定身标记；当前关卡装配已注入该查询，未配置状态时使用中性修正（倍率 1、非定身）；
- PassageSystem 与 MechanismSystem 消费移动产生的事实。

## 8. P0 实现约定

- 使用动态 Rigidbody2D、零重力、冻结旋转和连续碰撞检测；MovementSystem 在 `FixedTick` 设置刚体速度，不直接改 Transform。
- 角色之间由二维物理碰撞产生推挤；MovementSystem 只结束与 MapManager 识别出的静态实体碰撞所对应的请求。
- `ActorConfig.MoveSpeed` 的单位为像素/秒；世界速度为 `MoveSpeed / SpriteRenderer.sprite.pixelsPerUnit`。
- 首版朝请求目标直线移动，不做寻路。与 MapManager 绑定的场景静态实体碰撞时停止当前请求、报告当前位置并发布 `MovementBlockedEvent`；系统不把碰撞解释为伤害或机关效果。
- 地形移速修正从 MapManager 的当前位置快照读取；寒冷、炙热、减速等 Actor 状态修正由 StatusManager 提供。两类修正都只影响速度计算，MovementSystem 仍独占实际位移。
- 地形倍率按乘法叠加到 Actor 基础移速和 StatusManager 提供的 Actor 状态倍率上。当前 `Level_test` 尚未配置语义地形区域，因此实际场景速度仍为原移速。
- 暂停、死亡、销毁和重开会停止刚体并清理当前请求。取消会使当前速度归零；同一 Actor 只保留一个活动请求。
- MapManager 的阻挡缓存只识别当前场景中非 Trigger 的静态 Collider2D；`MapAreaAuthoring` 使用 Trigger Collider2D 做地图区域查询，不会被当作物理阻挡。`Level_test` 的 `StaticMovementObstacle` 提供静态 BoxCollider2D 场景样例。
- `TryGetContactInfo` 返回最近一次受阻请求的接触信息；重新提交请求后接触信息清空。
- LevelContext 已装配 StatusManager 并注入移动修正提供器；状态计时随关卡暂停/恢复冻结。状态倍率及具体寒冷、炙热等效果由 StatusConfig 配置。

## 9. 关键不变量

1. 同一 Actor 的互斥移动请求不能同时写入位置。
2. 取消、死亡、暂停和重开时，正在执行的移动必须可停止。
3. 移动系统报告碰撞事实，但不自动把碰撞解释成伤害或机关效果。

## 10. 待确认项

- 后续是否加入绕开障碍的路径搜索；P0 首版已确认直线移动并在静态碰撞时结束请求；
- 动态占用查询接入后，玩家放置物/动态障碍对移动请求的影响；
- 沼泽减速和其他地形移动修正的组合顺序，以及地形区域边界事件与物理碰撞的先后语义。

## 11. 验收场景

`Assets/Game/Tests/README-Level_test-MVP.md` 记录当前 Level_test 的装配边界。`LevelActorSpawner` 为本场景绑定无目标的随机漫游感知源和移动端口；BasicSlimeDecision / BasicEnemyDecision 产生 Move 意图后，经 ActorStateMachine、MovementActorActionPort 到达 MovementSystem，再由动态 Rigidbody2D 执行。MapManager 区域事件、地形倍率和 StatusManager 移动修正已有运行时代码，但该场景还没有相应状态配置和区域标记，因此未在场景中验收这些效果；正式目标感知和确定性阻挡用例尚未接入。

