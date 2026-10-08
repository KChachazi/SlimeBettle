# 22 PlacementSystem 放置系统设计

系统编号：`PLAYER-02`  
系统名称：`PlacementSystem`  
默认命名空间：`SlimeBattle.Player.Placement`  
建议实现目录：`Assets/Game/Scripts/Player/`  
生命周期：当前关卡上下文生命周期  
状态：核心交互边界已确定，放置物规则待确认

## 1. 职责

- 接收玩家选择的放置物和目标位置；
- 将输入位置转换为地图坐标；
- 创建、更新和销毁放置预览；
- 检查地图范围、碰撞、占用和规则合法性；
- 确认合法放置并提交结果；
- 将成功放置通知机关或场景系统。

## 2. 不负责的内容

- 不直接解析底层触摸；
- 不绕过规则把预制体塞进场景；
- 不直接扣除资源，资源所有权待策划方案确认；
- 不决定机关触发后的伤害和状态效果；
- 不让 UI 直接修改地图对象。

## 3. 核心数据

```text
PlacementItemId
PlacementConfig
PlacementPreview
PlacementRequest
PlacementValidationResult
PlacementResult
PlacementBudgetSnapshot
PlacementCooldownSnapshot
```

## 4. 必须接口

```csharp
void BeginPlacement(PlacementItemId itemId);
PlacementValidationResult PreviewPlacement(WorldPosition position);
PlacementResult ConfirmPlacement();
void CancelPlacement();
PlacementPreview GetCurrentPreview();
PlacementBudgetSnapshot GetBudgetSnapshot();
```

## 4.1 当前策划放置规则

当前图示中的可放置环境元素包括食物、障碍和熔炉：

- 食物被史莱姆吃掉后消失，图示暂定再次放置间隔为 10 秒；
- 障碍数量有限，每个场景的分配数量可以不同，用于改变史莱姆路线；
- 熔炉当前设想为单个可重复使用，拆除后图示暂定 20 秒后才能重新安装；
- 总放置上限暂定为 5，不能在代码中写成无配置的全局常量；
- 组合放置可能影响路径、阵型和连锁反应，具体效果由 `MechanismSystem` 与关卡配置决定。

每次确认放置都必须同时校验物品库存/次数、冷却、场景总上限、地图占用和元素互斥规则；预览通过不代表最终确认一定成功。

## 5. 对外事件

```text
PlacementStartedEvent
PlacementPreviewChangedEvent
PlacementConfirmedEvent
PlacementRejectedEvent
PlacementCancelledEvent
```

## 6. 依赖

- `InputSystem`；
- `CameraSystem`；
- `MapManager`；
- `ConfigSystem`；
- `MechanismSystem`；
- 未来可能需要的关卡资源/库存系统。
- `LevelManager`/`ObjectiveSystem` 提供当前关卡阶段和目标状态；
- `SlimeGroupActor` 不直接依赖放置 UI，只读取放置结果和环境查询。

## 7. 关键不变量

1. 预览状态不等于已经放置。
2. 只有通过当前地图和玩法校验的请求才能确认。
3. 取消和关卡重开必须销毁预览对象并清理当前选择。
4. 资源扣除和对象创建必须属于同一个可验证的确认结果。

## 8. 待确认项

- 玩家可以放置哪些物品；
- 物品是否有数量、冷却或消耗；
- 放置位置是网格、自由坐标还是预设点；
- 放置对象是否属于 `MechanismSystem`；
- 总放置上限是否固定为 5，以及是否按场景/周目变化；
- 食物、障碍、熔炉的正式配置名、图标、范围和冷却；
- 玩家是否可以在暂停、战斗或结算阶段放置。

