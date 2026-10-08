# 11 MapSystem 地图与空间查询系统设计

系统编号：`WORLD-01`  
系统名称：`MapSystem`  
默认命名空间：`SlimeBattle.World`  
建议实现目录：`Assets/Game/Scripts/Core/`；地图资源位于 `Assets/Game/Scenes/`  
生命周期：当前关卡上下文生命周期  
状态：核心架构

## 1. 职责

- 从当前关卡场景读取地图边界、Tilemap、出生点、目标点、护送武器点和关卡标记；
- 为角色、机关、放置和镜头提供统一的空间查询；
- 提供可行走、可占用、阻挡和区域信息；
- 管理地图查询所需的场景引用和缓存。

当前默认方案是地图由 Unity 场景和 Tilemap 保存，运行时 `MapSystem` 读取并提供查询，不立即引入随机地图生成器。

## 2. 不负责的内容

- 不创建和销毁史莱姆、敌人或机关；
- 不决定角色下一步行动；
- 不把地图视觉 Tilemap 当作角色运行时数据；
- 不在第一版承担随机地图生成，除非策划明确需要。

## 3. 核心数据

```text
MapConfig
MapBounds
MapCellInfo
SpawnPointId
TargetPointId
WeaponPointId
WorldMarker
MapSnapshot
```

## 4. 必须接口

```csharp
MapBounds GetMapBounds();
bool TryGetSpawnPoint(SpawnPointId spawnPointId, out WorldPosition position);
bool TryGetTargetPoint(TargetPointId targetPointId, out WorldPosition position);
bool TryGetWeaponPoint(WeaponPointId weaponPointId, out WorldPosition position);
bool CanOccupy(WorldPosition position, OccupancyQuery query);
MapCellInfo QueryCell(WorldPosition position);
bool TryGetWorldMarker(string markerId, out WorldMarker marker);
```

`CanOccupy` 和 `QueryCell` 只提供空间事实，不替调用方决定行动。

## 5. 对外事件

```text
MapReadyEvent
MapQueryStateChangedEvent
```

常规空间查询不发布事件。只有地图初始化完成、可用区域发生运行时变化时，才需要通知其他系统。

## 6. 场景约定

建议场景包含独立的地面、碰撞和装饰 Tilemap，并使用场景对象标记出生点、目标点、护送武器点、机关范围和窄路入口。地图数据属于场景资产，地图运行时状态不写回场景资产。

## 7. 关键不变量

1. 所有系统使用同一套世界坐标、边界和可占用判断。
2. 查询结果不能依赖当前摄像机是否看见对象。
3. 场景重开后，地图查询引用必须重新绑定到新场景。

## 8. 待确认项

- 地图碰撞层和可行走规则；
- 是否需要路径搜索；
- 窄路是地图属性还是独立机制；
- 是否会加入随机地图或运行时改造地形。

