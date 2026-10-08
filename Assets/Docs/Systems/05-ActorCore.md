# 05 ActorCore 角色基础层设计

系统编号：`ACTOR-01`  
系统名称：`ActorCore`  
默认命名空间：`SlimeBattle.Actors`  
建议实现目录：`Assets/Game/Scripts/Core/`  
生命周期：类型和契约随应用存在；具体运行时角色由所属角色系统管理  
状态：核心架构

## 1. 职责

`ActorCore` 是史莱姆、敌人和守卫共享的基础数据与契约层，不是一个集中管理所有角色的全局管理器。

- 定义角色实例的公共身份；
- 定义阵营、存活和可受效果等公共概念；
- 提供角色系统之间共享的只读快照和请求数据；
- 约束移动、AI、效果和表现适配的接入方式。

## 2. 不负责的内容

- 不集中拥有所有角色实例；
- 不决定角色类型的行为；
- 不代替史莱姆、敌人或守卫系统创建和销毁对象；
- 不直接扣血、移动或播放动画。

## 3. 核心类型与命名

```text
ActorId
FactionId
ActorType
ActorRuntimeData
ActorSnapshot
IEffectReceiver
IMovementAgent
IAiAgent
```

建议公共运行时数据至少表达：`ActorId`、`FactionId`、`ActorType`、`IsAlive` 和世界位置。具体角色数值放在角色自己的运行时数据中。

## 4. 必须接口

```csharp
ActorId GetActorId();
ActorType GetActorType();
FactionId GetFactionId();
bool IsAlive();
ActorSnapshot GetSnapshot();
```

受效果接口的概念形式：

```csharp
EffectApplyResult ApplyEffect(ResolvedEffect effect);
```

该接口表示目标所属系统接受效果，不代表调用方可以直接修改目标内部字段。

## 5. 对外事件

角色具体系统负责发布 `ActorSpawnedEvent`、`ActorDespawnedEvent`、`ActorDiedEvent` 等结果事件。`ActorCore` 不维护一套重复的全局角色事件分发器。

## 6. 依赖

- `ConfigSystem` 提供角色配置 ID 的解释；
- 史莱姆、敌人和守卫系统拥有具体运行时数据；
- `MovementSystem`、`CombatSystem` 和 `StatusManager` 使用本层公共身份和接收契约。

## 7. 关键不变量

1. `ActorId` 在当前关卡中唯一。
2. `ActorId` 失效后，旧请求不能作用于新实例。
3. 角色配置 ID 和角色实例 ID 不混用。
4. 角色的最终生命和存活状态由其所属角色系统拥有。

## 8. 待确认项

- `ActorId` 的具体类型和生成策略；
- 角色是否全部使用统一预制体接口；
- 是否需要可被玩家直接控制的通用角色接口。

