# 02 LevelManager 系统设计

系统编号：`CORE-02`  
系统名称：`LevelManager`  
默认命名空间：`SlimeBattle.Core`  
建议实现目录：`Assets/Game/Scripts/Core/`  
生命周期：应用运行期间存在，管理一个时刻的关卡上下文  
状态：核心架构

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
void RequestPause();
void RequestResume();
LevelId GetCurrentLevelId();
LevelState GetLevelState();
```

请求接口只表达意图。场景是否加载成功、暂停是否完成和重开是否开始，通过结果事件表达。

## 5. 对外事件

```text
LevelLoadStartedEvent
LevelStartedEvent
LevelPausedEvent
LevelResumedEvent
LevelSucceededEvent
LevelFailedEvent
LevelRestartedEvent
LevelUnloadedEvent
```

`LevelStartedEvent` 表示系统初始化完成并且可以开始运行，不等于 Unity 场景刚刚打开。

## 6. 生命周期

1. 停止旧关卡更新；
2. 取消旧关卡待处理请求和任务；
3. 销毁旧的 `LevelContext` 与 `LevelEventBus`；
4. 加载目标场景；
5. 创建新的 `LevelContext`；
6. 收集当前关卡的配置资产，注册到 `ConfigRegistry` 并执行 `ValidateAll()`；校验未通过时报告错误，不进入 `Running`；
7. 将同一份已校验的 `ConfigRegistry` 交给需要配置的系统，创建并连接 LevelContext 持有的 Manager 和其他关卡服务；
8. 发布 `LevelStartedEvent`；`Level_test` 的场景生成组件收到该事件后向正式 Manager 提交初始对象生成请求。

P0 实现使用 `LevelManager` Inspector 中的 `ConfigAsset[]` 收集当前配置；最新第一关必须包含与场景名对应的 `LevelConfig`、终点统计/等待窗口结果配置及所需角色、地形、路线和机关配置。护送武器配置仅在兼容武器链路启用的关卡中需要；旧 `ObjectiveConfig` / `WeaponConfig` 仅作为迁移期间的历史类型，不应成为最新第一关结果规则的新依赖。缺失或校验失败时发布 `LevelLoadFailedEvent`，不进入 `Running`。`Level_test` 的初始 Actor 由场景中的 `LevelActorSpawner` 在 `LevelStartedEvent` 后调用正式 Manager 公开接口生成；其他关卡仍需自行配置初始对象来源。

`LevelManager` 还在 Inspector 中持有正式 `SlimeActor` 与 `EnemyActor` 预制体引用，并传给 `LevelContext`。`LevelContext` 再注入对应 Manager；缺少预制体时不进入运行态。当前 Bootstrap 场景已绑定 `PF_Slime_Green`、`PF_Enemy_Basic` 和 `Level_test` 所需的五个配置资产。Bootstrap 中的 `BootstrapMenuLoader` 加载 MainMenu，菜单 Start 按钮通过 `GameRoot.RequestEnterLevel` 请求进入 `Level_test`。

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

- 关卡场景命名和 `LevelId` 的映射方式；
- 是否需要异步加载进度 UI；
- 是否存在主菜单、关卡选择和下一关流程。
- 目标结果与重开/下一关 UI 的结算展示顺序。

