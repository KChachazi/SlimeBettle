# 14 PassageSystem 窄路与通行系统设计

系统编号：`MECH-02`  
系统名称：`PassageSystem`  
默认命名空间：`SlimeBattle.World.Passage`  
建议实现目录：`Assets/Game/Scripts/Mechanism/`  
生命周期：当前关卡上下文生命周期  
状态：规则待确认

## 1. 职责

- 管理需要排队、限流、单向或特殊占用规则的窄路；
- 维护窄路的入口、出口、占用者和等待顺序；
- 判断进入请求应当通过、等待还是拒绝；
- 在角色离开、死亡、取消或关卡重开时释放占用。

如果策划最终确认“窄路只是普通碰撞地形”，本系统应合并回 `MapSystem` 或 `MovementSystem`，不要保留空壳模块。

## 2. 不负责的内容

- 不直接移动角色；
- 不决定角色是否想进入窄路；
- 不计算伤害；
- 不播放排队动画；
- 不修改普通地图 Tilemap 的视觉数据。

## 3. 核心数据

```text
PassageId
PassageConfig
PassageRuntimeData
PassageAccessRequest
PassageAccessResult
PassageOccupancySnapshot
```

## 4. 必须接口

```csharp
PassageAccessResult RequestEnter(PassageAccessRequest request);
bool NotifyExit(PassageId passageId, ActorId actorId);
bool CancelEntry(PassageId passageId, ActorId actorId);
bool TryGetOccupancy(PassageId passageId, out PassageOccupancySnapshot snapshot);
```

## 5. 对外事件

```text
PassageEntryGrantedEvent
PassageEntryQueuedEvent
PassageEntryRejectedEvent
PassageOccupantExitedEvent
```

## 6. 依赖

- `MapSystem` 提供窄路标记和空间位置；
- `MovementSystem` 执行进入和离开；
- 角色系统报告死亡和销毁；
- `MechanismSystem` 在窄路被定义为特殊机关时提交触发请求。

## 7. 关键不变量

1. 一个角色不能同时占用两个互斥窄路位置。
2. 角色死亡、销毁和重开必须释放占用。
3. 队列顺序和占用结果必须由本系统统一维护。

## 8. 待确认项

- 窄路是否存在独立玩法规则；
- 是否允许反向通行和超车；
- 等待是否会影响 AI 决策；
- 是否需要把排队状态显示给 UI。

