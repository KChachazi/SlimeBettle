# 03 LevelContext 与 LevelEventBus 设计

系统编号：`CORE-03`  
系统名称：`LevelContext` / `LevelEventBus`  
默认命名空间：`SlimeBattle.Core.Context`  
建议实现目录：`Assets/Game/Scripts/Core/`  
生命周期：当前关卡加载完成至当前关卡卸载  
状态：核心架构

## 1. 职责

`LevelContext` 是当前关卡的运行时边界，保存本局系统之间的明确引用和关卡级资源。`LevelEventBus` 是该上下文内部的类型化通知通道。

LevelContext 明确持有当前关卡的地图、角色管理器、移动、状态、机关和其他关卡级服务。它还创建一个位于 `Core` 的轻量关卡结果规则组件；该组件不是独立的 LevelContext Manager，不拥有武器或史莱姆运行时数据。管理器本身仍然是独立模块，但不能脱离 LevelContext 作为跨关卡单例存在。

当前代码已落地 `ConfigRegistry`、`SlimeManager`、`EnemyManager`、`GuardManager`、`MechanismSystem`、`LevelResultRules`、`MapManager`、`MovementSystem`、`StatusManager` 和 `LevelEventBus`，同时仍保留旧 `ObjectiveSystem`/目标配置的迁移痕迹。护送武器事实链路已由 `MechanismSystem` 提供，但最新第一关结果仍需迁移为史莱姆终点统计、等待窗口和 6 只目标；本段不把该迁移写成已完成事实。MovementSystem 使用动态 `Rigidbody2D` 执行直线移动，并查询 StatusManager 的移速/定身修正；StatusManager 结算 Actor 生命与状态，消费 MapManager 区域事件。

`LevelContext` 从 `LevelManager` 接收正式 SlimeActor / EnemyActor 预制体，并注入两个 Manager；运行时 Actor 是预制体实例，ActorCore 和 AI 运行时对象由实例自身初始化。Dispose 时 Manager 负责销毁这些实例。

- 创建、连接和销毁当前关卡的系统；
- 持有当前 `LevelId`、关卡状态和关卡服务引用；
- 提供当前关卡的 EventBus；
- 在重开、失败和卸载时统一释放订阅、计时器和待处理任务。

## 2. 不负责的内容

- 不替系统拥有史莱姆、敌人或机关运行数据；
- 不根据事件内容修改玩法规则；
- 不作为永久的全局服务定位器；
- 不允许跨关卡保留本局事件订阅。

## 3. 核心数据

```text
LevelId levelId
LevelState levelState
LevelEventBus eventBus
LevelServices services
SlimeManager slimeManager
EnemyManager enemyManager
GuardManager guardManager
MapManager mapManager
MovementSystem movementSystem
StatusManager statusManager
MechanismSystem mechanismSystem
CoreLevelOutcomeRules outcomeRules
```

`LevelServices` 只保存已确认需要注入的系统引用。不能提供任意字符串查找和无限制的 `GetService` 作为默认连接方式。

## 4. 必须接口

```csharp
LevelId GetLevelId();
LevelState GetLevelState();
ILevelEventBus GetEventBus();
void Initialize();
void Dispose();
```

P0 增加 `BindActorPorts(IActorPerceptionSource, IActorActionPort)`：由关卡装配方传入当前关卡的感知及移动/效果公开 API 适配器；未绑定感知或可用行动端口时不 Tick Actor AI。MovementSystem 自动接管移动请求；`actionPort` 为 null 时表示只启用移动，不处理效果。`Level_test` 由 `LevelActorSpawner` 注入仅提供随机漫游点的感知源，用于检查决策树到 Rigidbody2D 的移动链路，不代表正式目标感知。`Tick(float)` 只在 `Running` 状态驱动已绑定的 Manager。`Dispose()` 清理 Manager、Actor、目标、端口引用和全部事件订阅。

EventBus 的概念接口：

```csharp
void Publish<TEvent>(TEvent eventData);
SubscriptionToken Subscribe<TEvent>(Action<TEvent> handler);
void Unsubscribe(SubscriptionToken token);
void Clear();
```

具体是 C# 事件、轻量 EventBus 还是其他实现，属于实现选项；公共约束是事件必须绑定到当前 `LevelContext` 的生命周期。

## 5. 对外事件

`LevelEventBus` 本身不定义一个无限扩展的全局事件名称清单。各系统只发布已经发生且确实需要被其他系统观察的结果事件，例如 `SlimeDiedEvent`、`SlimeDepletedEvent`、终点进入事实、等待窗口结束事实、`MechanismTriggeredEvent`、`WeaponDestroyedEvent` 和 `WeaponReachedGoalEvent`。其中武器事件是兼容/后续链路；最新第一关 Core 结果规则优先消费史莱姆终点统计与等待窗口事实，再发布 `LevelSucceededEvent` / `LevelFailedEvent`。

## 6. 依赖

- `LevelManager` 创建和销毁当前上下文；
- `GameRoot` 只负责应用级入口，不持有本局 EventBus；
- 当前关卡系统通过上下文获得明确注入的服务和事件通道。

## 7. 初始化顺序

1. 创建上下文和 EventBus；
2. 由 `LevelManager` 收集并注册本关配置、调用 `ValidateAll()`，再创建地图查询服务；
3. 创建 MapManager、StatusManager、MovementSystem、SlimeManager、EnemyManager 和 MechanismSystem；StatusManager 订阅区域事件，MovementSystem 接收其移动修正查询；
4. 创建 Core 结果规则组件，并让它订阅本局 LevelEventBus；
5. 创建输入、放置、镜头、UI、表现及已确认的机关适配；
6. 由 `LevelManager` 向需要配置的系统传入同一份已校验的 `ConfigRegistry`，并注入其他明确引用；
7. 让各系统执行自身初始化；
8. 由 `LevelManager` 发布关卡开始结果。

## 8. 关键不变量

1. EventBus 不被 `GameRoot`、静态字段或跨场景对象持有。
2. `Dispose` 后所有订阅和待处理任务都失效。
3. 系统之间的持久引用不能指向已经销毁的旧上下文。

## 9. 待确认项

- 系统连接使用显式构造、初始化容器还是场景引用；
- 是否需要在编辑器中显示当前上下文状态；
- 异步加载和延迟初始化是否进入首个可玩版本。

## 补充：场景组合与当前上下文（2026-10-14）

已确认采用“独立模块/隔离测试 + 场景组合”的协作方式。场景中的 `SceneCompositionRoot`/`LevelSceneInstaller` 可以通过 `[SerializeField] private` 持有具体 `MonoBehaviour`、Prefab、Authoring 和配置引用，但不直接序列化或持有 `LevelContext`、`MapManager`、`MechanismSystem` 等 plain C# 模块。

装配组件必须在明确的 `LevelStarted`/安装阶段取得当前 Context，向现有 Manager 注入场景父节点、Prefab、点位和适配器；在重开、失败、结算、禁用和场景卸载时取消订阅并清理自身临时引用。它不能创建第二个 Context、复制 Manager 数据或通过私有字段访问其他系统。`LevelEventBus` 仍只属于当前 Context，不能因为场景组合而跨场景持有。

