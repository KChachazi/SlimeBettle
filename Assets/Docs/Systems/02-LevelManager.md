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
- 管理关卡运行阶段：加载、初始化、进行中、暂停、胜利、失败和卸载；
- 消费 `ObjectiveSystem` 已确认的胜利/失败结果，并把结果转换为关卡阶段；
- 转发暂停、重开、进入下一关和返回请求；
- 维护当前关卡 ID 和当前关卡的生命周期状态。

## 2. 不负责的内容

- 不保存史莱姆或敌人的具体运行数据；
- 不计算胜负条件的细节；
- 不复制护送武器和史莱姆数量的运行时数据；
- 不创建全局静态 EventBus；
- 不实现地图、机关、战斗和 UI 的内部规则。

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
6. 按初始化顺序连接系统并生成初始对象；
7. 发布 `LevelStartedEvent`。

重开关卡采用重新加载当前关卡场景的方案。第一版不要求为每个请求携带本局 ID，但必须保证旧上下文、旧订阅和旧任务已经失效。

## 7. 依赖

- `GameRoot`；
- Unity `SceneManager` 或等价场景加载适配；
- `LevelContext`；
- `ObjectiveSystem`；
- 关卡配置系统。

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

