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

P0 使用注入的正式 `SlimeActor` 预制体执行 `Instantiate`，随后初始化 ActorId、配置和本局运行时数据。没有预制体时拒绝生成并返回 `LastFailureReason`；销毁或关卡 Dispose 时清理实例及 AI 请求。默认预制体位于 `Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab`，由 LevelManager 在 Inspector 引用。

## 2. 职责

- 根据生成请求创建、初始化和注册 SlimeActor；
- 维护当前关卡史莱姆 ActorId 索引和数量限制；
- 创建、驱动和销毁 SlimeGroupActor；
- 为 LevelContext、Core 结果规则组件、UI 和测试提供只读快照；
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

同屏上限只从 `LevelConfig.MaxAliveSlimes` 注入，首关确认上限为 20；`SlimeConfig` 只保存单体静态数值。P0 不做 Actor 或 GameObject 对象池。

## 5. 公共接口

    bool TrySetSpawnParent(Transform parent);
    ActorId SpawnSlime(SlimeSpawnRequest request);
    bool RequestDespawn(ActorId actorId, DespawnReason reason);
    bool TryGetSnapshot(ActorId actorId, out SlimeSnapshot snapshot);
    IReadOnlyList<SlimeSnapshot> GetAliveSnapshots();
    bool TryGetGroupSnapshot(GroupId groupId, out SlimeGroupSnapshot snapshot);
    void Tick(LevelTickContext context);
    void Dispose();

`Level_test` 的场景装配组件在首次生成前传入 `SlimeRoot`。该父节点只影响后续由 Manager 实例化的角色，不改变 Actor 的生命或行为数据。

P0 补充：`TryCreateGroup(IReadOnlyList<ActorId> members, Vector2 target, out GroupId groupId)` 只接受显式成员集合。自动成群阈值与策略待策划确认，不在本轮推断。

StatusManager 通过 LevelContext 的内部 Actor 解析契约找到目标 Actor。SlimeManager 不再对外暴露一个供其他系统直接扣血的 ApplyEffect 接口。

## 6. 对外事件

    SlimeSpawnedEvent
    SlimeDiedEvent
    SlimeDespawnedEvent
    SlimeControlChangedEvent
    SlimeGroupCreatedEvent
    SlimeGroupDissolvedEvent
    SlimeDepletedEvent

事件表示生命周期结果已经发生，不能被调用方当作可写数据源。

## 7. 依赖

- LevelContext 提供本局服务和 LevelEventBus；
- ConfigSystem 提供 SlimeConfig 和关卡配置；
- StatusManager 提供效果与状态结果；
- MovementSystem 与 MapManager 提供移动和空间事实；
- SlimeActor / SlimeGroupActor 提供个体与群体行为；
- Core 结果规则组件监听 `SlimeDepletedEvent`，不直接读取或修改 SlimeManager 的私有集合。

## 8. 关键不变量

1. 每个存活史莱姆只注册在一个 SlimeManager 中。
2. SlimeManager 是史莱姆生成和销毁的唯一关卡级入口。
3. 史莱姆死亡后必须从可行动集合移除；最后一只史莱姆确认死亡后发布一次 `SlimeDepletedEvent`，且该事件先于最终清理结果失效。
4. 管理器销毁时必须停止所有 Actor AI、群体计时器和事件订阅。
5. 管理器不保存生命和状态的第二份可写副本。

## 9. 验收场景

- 达到首关确认的 20 只或关卡配置上限时，生成请求返回可诊断的拒绝结果；
- 生成两只史莱姆后能分别查询快照和行为结果；
- 史莱姆死亡后不再被 Tick，且 Core 结果规则组件能收到一次 `SlimeDepletedEvent`；
- 重开关卡后旧 SlimeManager、Actor、群体和事件订阅全部失效。

