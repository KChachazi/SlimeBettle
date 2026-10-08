# 12 MovementSystem 移动系统设计

系统编号：`WORLD-02`  
系统名称：`MovementSystem`  
默认命名空间：`SlimeBattle.World.Movement`  
建议实现目录：`Assets/Game/Scripts/Slime/`  
生命周期：当前关卡上下文生命周期  
状态：实现待选

## 1. 职责

- 接收角色状态机产生的移动意图；
- 执行实际位置变化、停止、转向、跳跃或其他移动动作；
- 处理阻挡、碰撞、落地、接触和移动失败；
- 向请求方报告动作执行结果。

## 2. 不负责的内容

- 不决定角色为什么移动；
- 不计算最终伤害；
- 不直接改变机关冷却和窄路队列；
- 不由镜头是否跟随决定移动是否执行。

## 3. 核心数据

```text
MovementRequest
MovementState
MovementResult
ContactInfo
MovementMode
```

## 4. 必须接口

```csharp
MovementRequestId SubmitMovement(MovementRequest request);
bool CancelMovement(MovementRequestId requestId, CancelReason reason);
bool TryGetMovementState(ActorId actorId, out MovementState state);
bool TryGetContactInfo(ActorId actorId, out ContactInfo contactInfo);
```

## 5. 对外事件

```text
MovementStartedEvent
MovementCompletedEvent
MovementBlockedEvent
ActorLandedEvent
ActorContactEvent
```

这些事件描述移动事实。机关触发和战斗命中由各自系统根据事实继续处理。

## 6. 依赖

- `MapSystem` 提供边界、可占用和区域查询；
- `ActorCore` 提供角色身份和位置语义；
- 角色状态机提交移动请求；
- `MechanismSystem` 和 `PassageSystem` 消费移动产生的接触、进入和离开事实。

## 7. 实现选项

可以选择 Rigidbody2D、运动学移动或自定义空间模拟。选择依据是推挤、碰撞、跳跃/坑、窄路和移动端性能测试，不在接口层提前绑定具体实现。

## 8. 关键不变量

1. 同一角色的互斥移动请求不能同时写入位置。
2. 取消、死亡、暂停和重开时，正在执行的移动必须可停止。
3. 移动系统报告碰撞事实，但不把碰撞自动解释成机关效果或伤害。

## 9. 待确认项

- 移动单位和速度模型；
- 是否允许角色互相推动；
- 是否使用 Unity 物理；
- 是否需要路径搜索和局部避障。

