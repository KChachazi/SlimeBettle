# 19 MechanismSystem 机关系统设计

系统编号：`MECH-01`  
系统名称：`MechanismSystem`  
默认命名空间：`SlimeBattle.Mechanisms`  
建议实现目录：`Assets/Game/Scripts/Mechanism/`  
生命周期：当前关卡上下文生命周期  
状态：核心架构，当前环境元素方案已补充，触发细则待确认

## 1. 职责

- 注册和销毁场景中的机关实例；
- 读取机关配置并保存机关运行时状态；
- 接收接触、经过、操作和区域进入等触发事实；
- 判断机关是否满足触发条件、是否冷却或耗尽；
- 向 `StatusManager`、`PassageSystem` 或角色系统提交明确结果。

## 2. 不负责的内容

- 不直接修改目标生命、位置和状态字段；
- 不从动画播放结果推断机关是否成功；
- 不承担全部地图可行走查询；
- 不让机关预制体直接调用任意角色内部方法。

## 3. 核心数据

```text
MechanismId
MechanismConfig
MechanismRuntimeData
MechanismTriggerContext
MechanismSnapshot
MechanismActivationResult
GuideSourceType
FoodRuntimeData
ObstacleRuntimeData
FurnaceRuntimeData
PlacementCooldown
```

## 4. 必须接口

```csharp
bool NotifyContact(MechanismTriggerContext context);
bool RequestActivate(MechanismId mechanismId, MechanismActivationRequest request);
bool TryGetSnapshot(MechanismId mechanismId, out MechanismSnapshot snapshot);
IReadOnlyList<MechanismSnapshot> GetActiveSnapshots();
```

## 5. 对外事件

```text
MechanismTriggeredEvent
MechanismStateChangedEvent
MechanismExhaustedEvent
```

`MechanismTriggeredEvent` 表示机关规则已经通过并产生结果，不表示仅仅发生了碰撞回调。

## 5.1 当前环境元素方案

策划图示中的环境元素先统一归入本系统，具体预制体和表现不改变其运行时所有权：

| 元素 | 当前规则方向 | 暂定限制 |
| --- | --- | --- |
| 食物 | 引导范围内史莱姆；被吃掉后消失 | 再次放置间隔 10 秒 |
| 障碍 | 让史莱姆重新寻找路线；每个场景数量可不同 | 数量有限 |
| 熔炉 | 单个可重复使用，吸引较大范围史莱姆 | 拆除后 20 秒才能重新安装 |

组合放置可能形成路径、阵型和连锁反应；环境元素还可能与地形和敌人互相作用。总放置上限暂定为 5，必须由配置或关卡规则提供，不能在系统代码中硬编码。

## 6. 依赖

- `ConfigSystem`；
- `MapManager`；
- `MovementSystem` 提供的接触事实；
- `StatusManager` 处理效果；
- `PassageSystem` 处理窄路相关规则。
- `PlacementSystem` 提交玩家确认的环境元素放置结果。
- `SlimeManager`/`SlimeGroupActor` 查询引导源和元素影响，不直接修改机关状态。

## 7. 关键不变量

1. 触发判定只在机关系统完成一次。
2. 冷却和使用次数只能由机关系统修改。
3. 机关效果通过统一效果请求提交，不能绕过 `StatusManager`。

## 8. 待确认项

- 机关种类和触发方式；
- 机关是否可被玩家放置；
- 机关是永久场景对象还是可生成对象；
- 机关与窄路的职责边界。
- 食物、障碍和熔炉的具体触发范围、重复使用和销毁条件；
- 放置总上限是否按场景、周目或物品类型分别计算；
- 元素组合、地形互动和对敌人的影响；
- 机关是否包含陷阱、魔法和不可放置的场景机关。

