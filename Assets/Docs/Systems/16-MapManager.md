# 16 MapManager 地图与空间查询管理器设计

系统编号：LEVEL-04
系统名称：MapManager
默认命名空间：SlimeBattle.World
建议实现目录：Assets/Game/Scripts/World/；地图资源位于 Assets/Game/Scenes/
生命周期：由当前 LevelContext 创建和销毁
状态：核心架构

## 1. 定位

MapManager 是 LevelContext 内的地图服务，负责绑定当前场景的地图资源、空间标记和查询缓存。地图资源仍然属于 Unity 场景，MapManager 不把场景 Tilemap 复制成另一套地图资产。

## 2. 职责

- 读取当前场景的 Tilemap、地图边界、出生点、目标点、护送武器点和关卡标记；
- 提供可行走、可占用、阻挡和区域查询；
- 为 Actor、MovementSystem、PlacementSystem、CameraSystem 和 AI 提供统一空间事实；
- 管理地图查询所需的场景引用和缓存；
- 在场景加载完成后发布 MapReadyEvent。

当前默认方案是地图由 Unity 场景和 Tilemap 保存，运行时由 MapManager 读取，不立即引入随机地图生成器。

## 3. 不负责的内容

- 不创建和销毁 Actor、机关或放置物；
- 不决定角色为什么移动；
- 不执行实际位移和碰撞响应；
- 不把地图视觉 Tilemap 当作 Actor 运行时数据；
- 不在第一版承担随机地图生成。

## 4. 核心数据

    MapConfig
    MapBounds
    MapCellInfo
    SpawnPointId
    TargetPointId
    WeaponPointId
    WorldMarker
    MapSnapshot

## 5. 公共接口

    MapBounds GetMapBounds();
    bool TryGetSpawnPoint(SpawnPointId spawnPointId, out WorldPosition position);
    bool TryGetTargetPoint(TargetPointId targetPointId, out WorldPosition position);
    bool TryGetWeaponPoint(WeaponPointId weaponPointId, out WorldPosition position);
    bool CanOccupy(WorldPosition position, OccupancyQuery query);
    MapCellInfo QueryCell(WorldPosition position);
    bool TryGetWorldMarker(string markerId, out WorldMarker marker);

CanOccupy 和 QueryCell 只提供空间事实，不替调用方决定行动。

## 6. 关键不变量

1. 当前 LevelContext 中所有系统使用同一套世界坐标、边界和可占用判断。
2. 查询结果不能依赖摄像机是否看见对象。
3. 场景重开后，MapManager 必须重新绑定新场景引用和缓存。
4. 地图查询失败必须返回可诊断结果，不能静默使用上一关缓存。

## 7. 待确认项

- 地图碰撞层和可行走规则；
- 是否需要路径搜索；
- 窄路是地图属性还是独立 PassageSystem；
- 是否加入随机地图或运行时改造地形。

