# 01 GameRoot 系统设计

系统编号：`CORE-01`  
系统名称：`GameRoot`  
默认命名空间：`SlimeBattle.Core`  
建议实现目录：`Assets/Game/Scripts/Core/`  
生命周期：应用启动至应用退出  
状态：核心架构

## 1. 职责

`GameRoot` 是应用级组合根，负责建立一次应用运行所需的全局服务，并把关卡生命周期交给 `LevelManager`。

- 初始化全局配置和基础服务；
- 保存跨关卡仍然有效的设置；
- 创建或持有 `LevelManager`；
- 接收应用级入口请求，例如进入首个关卡和退出；
- 订阅 `LevelManager.ReturnToPreviousFlowRequested`，选择 `MainMenu` 为返回目标，并交由 LevelManager 卸载当前局；
- 保证应用级服务只初始化一次。

## 2. 不负责的内容

- 不保存当前关卡中的史莱姆、敌人、机关和队列；
- 不直接实现关卡胜负、伤害、AI 或放置规则；
- 不持有当前关卡的 `LevelEventBus`；
- 不把关卡运行时数据放入静态字段；
- 不直接控制 UI 动画和表现播放。

## 3. 核心数据

```text
ApplicationState
GlobalSettings
LevelManager levelManager
```

`GlobalSettings` 的具体字段暂不锁定。输入设置、音量和画面设置是否跨场景保存，需要等平台方案确认。

## 4. 必须接口

```csharp
void InitializeApplication();
ApplicationState GetApplicationState();
void RequestEnterLevel(LevelId levelId);
void RequestQuit();
```

`GameRoot` 可以通过事件或结果回调报告初始化和退出结果，但不向外暴露可被任意修改的系统引用。

## 5. 对外事件

```text
ApplicationInitializedEvent
ApplicationQuitRequestedEvent
```

关卡开始、暂停和关卡结果属于 `LevelManager` 的事件，不由 `GameRoot` 重复发布。

## 6. 依赖

- Unity 应用生命周期；
- `LevelManager`；
- 全局配置读取能力；
- 不依赖史莱姆、机关和 UI 的具体实现。

## 7. 关键不变量

1. 应用级初始化最多成功执行一次。
2. `GameRoot` 销毁或退出时，不留下继续运行的关卡任务。
3. `GameRoot` 不通过静态全局对象保存本局数据。

## 8. 验收场景

- 启动后只创建一个 `GameRoot`；
- 从启动入口进入关卡一次，不重复创建 `LevelManager`；
- 重开关卡后不创建第二个应用级服务；
- 退出或切换时旧关卡的事件和计时器不再执行。

## 9. 待确认项

- 是否使用独立 `Bootstrap` 场景；
- 全局设置是否需要跨场景保存；
- 是否需要主菜单和多个启动入口。

