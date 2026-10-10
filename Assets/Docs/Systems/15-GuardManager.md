# 15 GuardManager 守卫管理器设计

系统编号：LEVEL-03
系统名称：GuardManager
默认命名空间：SlimeBattle.Guards
建议实现目录：Assets/Game/Scripts/Actors/Guard/
生命周期：由当前 LevelContext 创建和销毁
状态：核心架构，首关需要生成守卫并保护护送武器；数量与控制方式待确认

## 1. 定位

GuardManager 是 LevelContext 内的守卫生命周期管理器，负责 GuardActor 的集合、索引、生成、销毁和统一驱动。

## 2. 职责

- 创建、初始化和注册 GuardActor；
- 维护守卫 ActorId 索引与只读快照；
- 向守卫提交来自 InputSystem 或调试工具的语义指令；
- 驱动守卫决策树和状态机；
- 处理守卫死亡、销毁和控制模式变化。

## 3. 不负责的内容

- 不解析原始触摸、鼠标和键盘输入；
- 不直接写入守卫 Transform、生命和状态；
- 不替代 GuardActor 的决策树与状态机；
- 不实现统一效果规则；
- 不播放 UI、动画和音效。

## 4. 公共接口

    ActorId SpawnGuard(GuardSpawnRequest request);
    bool RequestDespawn(ActorId actorId, DespawnReason reason);
    bool TryGetSnapshot(ActorId actorId, out GuardSnapshot snapshot);
    IReadOnlyList<GuardSnapshot> GetAliveSnapshots();
    bool SubmitCommand(ActorId actorId, GuardCommand command);
    void Tick(LevelTickContext context);
    void Dispose();

## 5. 对外事件

    GuardSpawnedEvent
    GuardControlModeChangedEvent
    GuardDiedEvent
    GuardDespawnedEvent

## 6. 依赖

- LevelContext 和 LevelEventBus；
- InputSystem 提供设备无关的指令；
- MechanismSystem 提供护送武器状态；
- EnemyManager 提供敌人查询；
- MapManager 与 MovementSystem 提供空间和移动事实；
- StatusManager 不向守卫应用食物、温暖或史莱姆状态修正；常规 Actor 受击契约仍需联调；
- GuardActor 提供个体行为。

## 7. 关键不变量

1. 每个守卫只属于一个 GuardManager。
2. 外部指令必须经过 GuardActor 的状态机，不得直接操作 Unity Transform。
3. 死亡、暂停和重开时，未执行的守卫指令必须失效。
4. 管理器不保存守卫生命和状态的第二份可写副本。

