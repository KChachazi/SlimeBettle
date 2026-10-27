# 02 LevelManager 系统设计

系统编号：`CORE-02`  
系统名称：`LevelManager`  
默认命名空间：`SlimeBattle.Core`  
建议实现目录：`Assets/Game/Scripts/Core/`  
生命周期：应用运行期间存在，管理一个时刻的关卡上下文  
状态：负责已实现的关卡加载、重开、暂停、结果结算和战役账本生命周期

> 2026-10-28 代码核对：下文中带日期的旧集成记录保留历史语境。正式 `FirstLevel` / `Castle` 当前启用护送武器结果；Build Settings 的场景清单见项目场景与配置目录说明。

## 1. 职责

- 加载、卸载和重新加载关卡场景；
- 创建和销毁当前关卡的 `LevelContext`；
- 收集当前关卡需要的配置资产，创建并填充 `ConfigRegistry`，调用 `ValidateAll()`，再将同一份 Registry 交给需要配置的关卡系统；
- 管理关卡运行阶段：加载、初始化、进行中、暂停、胜利、失败和卸载；
- 消费 Core 结果规则组件已确认的胜利/失败结果，并把结果转换为关卡阶段；
- 转发暂停、重开、进入下一关和返回请求；
- 维护当前关卡 ID 和当前关卡的生命周期状态。

## 2. 不负责的内容

- 不保存史莱姆或敌人的具体运行数据；
- 不计算胜负条件的细节；
- 不复制护送武器和史莱姆数量的运行时数据；
- 不创建全局静态 EventBus；
- 不实现地图、机关、攻击行为、状态效果和 UI 的内部规则。

## 3. 核心数据

```text
LevelId currentLevelId
LevelState levelState
LevelContext currentContext
```

`LevelState` 至少需要表达 `Loading`、`Initializing`、`Running`、`Paused`、`Succeeded`、`Failed` 和 `Unloading`。最终枚举名称可在代码实现前统一。

## 4. 必须接口

```csharp
void RequestLoadLevel(LevelId levelId);
void RequestRestartCurrentLevel();
void RequestReturnToPreviousFlow();
bool RequestUnloadToScene(string sceneName);
void RequestPause();
void RequestResume();
LevelId GetCurrentLevelId();
LevelState GetLevelState();
```

请求接口只表达意图。场景是否加载成功、暂停是否完成和重开是否开始，通过结果事件表达。

返回流程（2026-10-20）：`RequestReturnToPreviousFlow()` 在 Running、Paused、Succeeded、Failed 且无场景切换时发布 `ReturnToPreviousFlowRequestedEvent`，由 GameRoot 观察并选择 MainMenu。`RequestUnloadToScene` 供应用流程调用，先验证目标场景，再进入 Unloading、Dispose 旧 Context/事件/Actor，清空关卡 ID，异步切换到目标场景后进入 Unloaded；不为菜单创建 Context。重复请求在切换期间拒绝，LevelManager 和战役资源账本继续存在，可从菜单重新进入关卡。`Dispose()` 仍仅用于应用终止。

## 5. 对外事件

```text
LevelLoadStartedEvent
LevelStartedEvent
LevelRestartedEvent
LevelLoadFailedEvent
LevelStateChangedEvent
LevelResultReportedEvent
ReturnToPreviousFlowRequestedEvent
```

结果规则通过 `LevelResultRules.ResultDecided(LevelResult)` 通知 `LevelManager`；`LevelManager` 更新为 Succeeded/Failed 并发布 `LevelResultReportedEvent`。`LevelStartedEvent` 表示系统初始化完成并可以开始运行，不等于 Unity 场景刚刚打开。

## 6. 生命周期

1. 停止旧关卡更新；
2. 取消旧关卡待处理请求和任务；
3. 销毁旧的 `LevelContext` 与 `LevelEventBus`；
4. 加载目标场景；
5. 创建新的 `LevelContext`；
6. 收集当前关卡的配置资产，注册到 `ConfigRegistry` 并执行 `ValidateAll()`；校验未通过时报告错误，不进入 `Running`；
7. 将同一份已校验的 `ConfigRegistry` 交给需要配置的系统，创建并连接 LevelContext 持有的 Manager 和其他关卡服务；
8. 发布 `LevelStartedEvent`；`Level_test` 的场景生成组件收到该事件后向正式 Manager 提交初始对象生成请求。

当前实现由 `LevelManager.levelConfigs` 注册关卡配置，战役资源配置通过单独引用注入并在 `StartNewCampaign` 时初始化。正式第一关/城堡配置包含护送目标和对应场景武器；`LevelResultRules` 使用 `LevelConfig.ObjectiveKind` 选择规则，虽然 `ObjectiveConfig` 仍被注册校验，但其字段未传入结果规则。配置缺失/校验失败时发布 `LevelLoadFailedEvent`，不进入 `Running`。`FirstLevelCompositionRoot` / `CastleCompositionRoot` 在关卡开始后接入场景地图与 Actor 感知。

`LevelManager` 在 Inspector 中持有 `SlimeActor`、`EnemyActor` 和可选 `GuardActor` Prefab，并通过 `LevelConfig`/默认字段解析守卫 Prefab。`BootstrapMenuLoader` 加载 MainMenu；菜单可请求进入 `FirstLevel` 或 `Castle`。`Level_test` 保留代码入口但 Build Settings 禁用；主菜单当前不提供该入口。

正式场景若已加载且未通过 Build Settings 作为目标加载，`StartCurrentSceneLevel()` 可用同一套 ConfigRegistry、LevelContext、结果规则与资源生命周期初始化当前场景。

重开关卡采用重新加载当前关卡场景的方案。第一版不要求为每个请求携带本局 ID，但必须保证旧上下文、旧订阅和旧任务已经失效。

## 7. 依赖

- `GameRoot`；
- Unity `SceneManager` 或等价场景加载适配；
- `LevelContext`；
- Core 结果规则组件和当前关卡 `MechanismSystem`；
- `ConfigRegistry` 和当前关卡的配置资产来源。

## 8. 关键不变量

1. 同一时刻最多只有一个当前关卡上下文。
2. `Paused` 时规则模拟停止，UI 和调试查询仍可运行。
3. `Succeeded` 或 `Failed` 是终止状态，直到重开、进入下一关或返回流程。
4. 旧关卡不能向新关卡的 EventBus 发布事件。

## 9. 待确认项

- 是否需要异步加载进度 UI；
- 是否添加通用的自动下一关流程；当前结果页重开/返回入口已接入，Next Level 流程未列为现状。
- `ObjectiveConfig` 中武器引用与 `LevelConfig.ObjectiveKind` 的职责是否合并；Castle 奖励 ID 与 `Castle` LevelId 不一致。

## 补充：场景装配协作边界（2026-10-14）

`LevelManager` 仍是跨场景的生命周期所有者，不作为每个关卡场景的可序列化运行时服务。集成测试场景可以通过 `[SerializeField] private` 引用 `LevelManager` 组件，由场景组合组件在 `LevelStarted` 后取得当前 `LevelContext` 并连接地图、Actor、机关和表现层适配器。

场景组合组件不得创建第二个 `LevelManager`、`LevelContext` 或 Manager；只能做引用校验、依赖注入、阶段转发和卸载清理。场景组合测试通过不代表正式场景已经登记 Build Settings，也不替代 `LevelManager` 对重开、暂停、结算和场景卸载的生命周期控制。

