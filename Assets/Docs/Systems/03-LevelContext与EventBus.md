# 03 LevelContext 与 LevelEventBus 设计

系统编号：`CORE-03`  
系统名称：`LevelContext` / `LevelEventBus`  
默认命名空间：`SlimeBattle.Core.Context`  
建议实现目录：`Assets/Game/Scripts/Core/`  
生命周期：当前关卡加载完成至当前关卡卸载  
状态：核心架构

## 1. 职责

`LevelContext` 是当前关卡的运行时边界，保存本局系统之间的明确引用和关卡级资源。`LevelEventBus` 是该上下文内部的类型化通知通道。

LevelContext 明确持有当前关卡的地图、角色管理器、移动、状态、目标和其他关卡级服务。管理器本身仍然是独立模块，但不能脱离 LevelContext 作为跨关卡单例存在。

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
ObjectiveSystem objectiveSystem
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

EventBus 的概念接口：

```csharp
void Publish<TEvent>(TEvent eventData);
SubscriptionToken Subscribe<TEvent>(Action<TEvent> handler);
void Unsubscribe(SubscriptionToken token);
void Clear();
```

具体是 C# 事件、轻量 EventBus 还是其他实现，属于实现选项；公共约束是事件必须绑定到当前 `LevelContext` 的生命周期。

## 5. 对外事件

`LevelEventBus` 本身不定义一个无限扩展的全局事件名称清单。各系统只发布已经发生且确实需要被其他系统观察的结果事件，例如 `SlimeDiedEvent`、`MechanismTriggeredEvent` 和 `LevelObjectiveChangedEvent`。

## 6. 依赖

- `LevelManager` 创建和销毁当前上下文；
- `GameRoot` 只负责应用级入口，不持有本局 EventBus；
- 当前关卡系统通过上下文获得明确注入的服务和事件通道。

## 7. 初始化顺序

1. 创建上下文和 EventBus；
2. 由 `LevelManager` 收集并注册本关配置、调用 `ValidateAll()`，再创建地图查询服务；
3. 创建 MapManager、SlimeManager、EnemyManager、GuardManager、MovementSystem、StatusManager 和 ObjectiveSystem；
4. 创建输入、放置、镜头、UI、表现及已确认的机关适配；
5. 由 `LevelManager` 向需要配置的系统传入同一份已校验的 `ConfigRegistry`，并注入其他明确引用；
6. 让各系统执行自身初始化；
7. 由 `LevelManager` 发布关卡开始结果。

## 8. 关键不变量

1. EventBus 不被 `GameRoot`、静态字段或跨场景对象持有。
2. `Dispose` 后所有订阅和待处理任务都失效。
3. 系统之间的持久引用不能指向已经销毁的旧上下文。

## 9. 待确认项

- 系统连接使用显式构造、初始化容器还是场景引用；
- 是否需要在编辑器中显示当前上下文状态；
- 异步加载和延迟初始化是否进入首个可玩版本。

