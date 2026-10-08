# 23 CameraSystem 镜头系统设计

系统编号：`PRESENT-01`  
系统名称：`CameraSystem`  
默认命名空间：`SlimeBattle.Presentation.Camera`  
建议实现目录：`Assets/Game/Scripts/Player/`  
生命周期：当前场景或当前关卡上下文生命周期  
状态：核心架构，镜头交互待确认

## 1. 职责

- 管理 2D 正交摄像机的位置、缩放和边界；
- 将屏幕坐标转换为世界坐标；
- 响应输入产生的拖动、缩放或聚焦请求；
- 保证镜头移动不改变玩法模拟；
- 为放置和 UI 提供统一的坐标转换。

## 2. 不负责的内容

- 不决定角色是否可以移动；
- 不决定放置是否合法；
- 不暂停或推进游戏规则；
- 不因为目标离开屏幕而销毁对象。

## 3. 核心数据

```text
CameraState
CameraBounds
CameraCommand
ScreenPosition
WorldPosition
```

## 4. 必须接口

```csharp
WorldPosition ScreenToWorld(ScreenPosition screenPosition);
ScreenPosition WorldToScreen(WorldPosition worldPosition);
CameraState GetState();
void SubmitCommand(CameraCommand command);
void SetBounds(CameraBounds bounds);
```

## 5. 对外事件

```text
CameraStateChangedEvent
CameraBoundsChangedEvent
```

## 6. 依赖

- Unity `Camera`；
- `MapManager` 提供地图边界；
- `InputSystem` 提供镜头语义输入；
- UI 仅通过坐标转换或镜头状态查询使用它。

## 7. 关键不变量

1. 坐标转换必须使用当前摄像机和当前地图配置。
2. 摄像机位置、缩放和屏幕适配不能改变规则世界坐标。
3. 镜头离开对象时，角色和机关仍然继续遵守关卡更新规则。

## 8. 待确认项

- 镜头是否可拖动和缩放；
- 镜头边界和跟随对象；
- 屏幕方向、分辨率和适配策略；
- 是否需要镜头震动和过渡动画。

