# 14 EnemyManager 敌人管理器设计

系统编号：LEVEL-02
系统名称：EnemyManager
默认命名空间：SlimeBattle.Enemies
建议实现目录：Assets/Game/Scripts/Actors/Enemy/
生命周期：由当前 LevelContext 创建和销毁
状态：核心架构，敌人种类和波次待确认

## 1. 定位

EnemyManager 是 LevelContext 内的敌人生命周期管理器，负责 EnemyActor 的集合、索引、生成、销毁和统一驱动。

## 2. 职责

- 根据生成请求创建并注册 EnemyActor；
- 维护敌人 ActorId 索引和存活快照；
- 驱动敌人的感知、决策树和状态机；
- 处理敌人死亡、销毁和活动范围配置；
- 为其他系统提供只读敌人查询。

## 3. 不负责的内容

- 不决定每个敌人的目标优先级和具体攻击行为；
- 不直接修改敌人或其他 Actor 的生命；
- 不实现伤害、状态和移动规则；
- 不播放表现；
- 不在管理器中写死敌人种类和波次规则。

## 4. 核心数据

    Dictionary<ActorId, EnemyActor> actors
    EnemySpawnPolicy spawnPolicy
    EnemyManagerSnapshot snapshot

## 5. 公共接口

    ActorId SpawnEnemy(EnemySpawnRequest request);
    bool RequestDespawn(ActorId actorId, DespawnReason reason);
    bool TryGetSnapshot(ActorId actorId, out EnemySnapshot snapshot);
    IReadOnlyList<EnemySnapshot> GetAliveSnapshots();
    void Tick(LevelTickContext context);
    void Dispose();

## 6. 对外事件

    EnemySpawnedEvent
    EnemyDiedEvent
    EnemyDespawnedEvent
    EnemyStateChangedEvent

## 7. 依赖

- LevelContext 和 LevelEventBus；
- ConfigSystem 提供 EnemyConfig；
- EnemyActor 提供个体行为；
- MapManager 与 MovementSystem 提供范围和移动事实；
- StatusManager 处理效果和状态；
- ObjectiveSystem 提供护送目标查询。

## 8. 关键不变量

1. 敌人只能由当前 EnemyManager 创建和销毁。
2. 死亡敌人不能继续被 AI Tick 或提交攻击动作。
3. 管理器只保存索引和生命周期信息，不复制 Actor 的生命与状态。
4. 管理器销毁时必须清理所有 EnemyActor 和待执行请求。

