# 13 SlimeManager 史莱姆管理器设计

系统编号：LEVEL-01
系统名称：SlimeManager
默认命名空间：SlimeBattle.Slimes
建议实现目录：Assets/Game/Scripts/Actors/Slime/
生命周期：由当前 LevelContext 创建和销毁
状态：核心架构

## 1. 定位

SlimeManager 是 LevelContext 内的关卡级管理器，负责所有史莱姆和 SlimeGroupActor 的集合、索引、生成、销毁与统一驱动。它不是史莱姆个体本身。

LevelContext 持有一个 SlimeManager。SlimeManager 创建的每只史莱姆都是一个独立的 SlimeActor。

## 2. 职责

- 根据生成请求创建、初始化和注册 SlimeActor；
- 维护当前关卡史莱姆 ActorId 索引和数量限制；
- 创建、驱动和销毁 SlimeGroupActor；
- 为 LevelContext、ObjectiveSystem、UI 和测试提供只读快照；
- 驱动史莱姆的感知、决策树和状态机 Tick；
- 处理史莱姆死亡、销毁、控制变化和群体关系变化的生命周期结果。

## 3. 不负责的内容

- 不替 SlimeActor 决定具体行为；
- 不直接结算伤害、治疗、击退、易伤和移动 Buff；
- 不直接修改 MapManager、MovementSystem 或其他 Actor；
- 不播放 UI、动画、特效和音效；
- 不作为跨关卡单例存在。

## 4. 核心数据

    Dictionary<ActorId, SlimeActor> actors
    Dictionary<GroupId, SlimeGroupActor> groups
    SlimeSpawnPolicy spawnPolicy
    SlimeManagerSnapshot snapshot

当前方案的史莱姆数量上限、出生点和初始配置应来自 LevelConfig / SlimeConfig，不写死在管理器的行为分支中。

## 5. 公共接口

    ActorId SpawnSlime(SlimeSpawnRequest request);
    bool RequestDespawn(ActorId actorId, DespawnReason reason);
    bool TryGetSnapshot(ActorId actorId, out SlimeSnapshot snapshot);
    IReadOnlyList<SlimeSnapshot> GetAliveSnapshots();
    bool TryGetGroupSnapshot(GroupId groupId, out SlimeGroupSnapshot snapshot);
    void Tick(LevelTickContext context);
    void Dispose();

StatusManager 通过 LevelContext 的内部 Actor 解析契约找到目标 Actor。SlimeManager 不再对外暴露一个供其他系统直接扣血的 ApplyEffect 接口。

## 6. 对外事件

    SlimeSpawnedEvent
    SlimeDiedEvent
    SlimeDespawnedEvent
    SlimeControlChangedEvent
    SlimeGroupCreatedEvent
    SlimeGroupDissolvedEvent

事件表示生命周期结果已经发生，不能被调用方当作可写数据源。

## 7. 依赖

- LevelContext 提供本局服务和 LevelEventBus；
- ConfigSystem 提供 SlimeConfig 和关卡配置；
- StatusManager 提供效果与状态结果；
- MovementSystem 与 MapManager 提供移动和空间事实；
- SlimeActor / SlimeGroupActor 提供个体与群体行为；
- ObjectiveSystem 读取史莱姆存活结果。

## 8. 关键不变量

1. 每个存活史莱姆只注册在一个 SlimeManager 中。
2. SlimeManager 是史莱姆生成和销毁的唯一关卡级入口。
3. 史莱姆死亡后必须从可行动集合移除，但死亡事件先于最终清理结果发布。
4. 管理器销毁时必须停止所有 Actor AI、群体计时器和事件订阅。
5. 管理器不保存生命和状态的第二份可写副本。

## 9. 验收场景

- 达到关卡配置的史莱姆上限时，生成请求返回可诊断的拒绝结果；
- 生成两只史莱姆后能分别查询快照和行为结果；
- 史莱姆死亡后不再被 Tick，且 ObjectiveSystem 能收到存活数量变化；
- 重开关卡后旧 SlimeManager、Actor、群体和事件订阅全部失效。

