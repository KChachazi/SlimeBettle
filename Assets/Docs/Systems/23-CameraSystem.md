# 23 CameraSystem 镜头系统设计

系统编号：`PRESENT-01`  
系统名称：`CameraSystem`  
默认命名空间：`SlimeBattle.Presentation.Camera`  
建议实现目录：`Assets/Game/Scripts/Player/`  
生命周期：当前场景或当前关卡上下文生命周期  
状态：核心架构，镜头交互待确认

P0 实现更新：2026-10-09；负责人：小宸。对应旧分工中的镜头系统，按名称映射，不按旧数字操作其他模块。

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

## 9. P0 实现与接入契约

实现：`Assets/Game/Scripts/Player/CameraSystem.cs`，数据：`CameraState.cs`。概念类型 ScreenPosition / WorldPosition 在首版明确映射为 `UnityEngine.Vector2`（屏幕像素）/ `UnityEngine.Vector3`（世界坐标）；不在其他系统未定义共享空间类型时创建同名包装类型。

```csharp
void Initialize();
Vector3 ScreenToWorld(Vector2 screenPosition);
Vector2 WorldToScreen(Vector3 worldPosition);
CameraState GetState();
void SubmitCommand(CameraCommand command);
void SetBounds(CameraBounds bounds);
```

- CameraSystem 与 Camera 挂在同一对象上，由场景接入组件显式 Initialize。初始化为面向 XY 平面的正交相机；默认投影平面 Z=0，可在 Inspector 配置。已有合法相机 Z 保留；处在投影平面上或前方时移至平面后方 10 单位。
- 屏幕转世界使用相机射线与玩法平面求交，不把相机位置/近裁剪面当世界 Z；无法求交抛 `InvalidOperationException`。调用方先完成初始化，输入必须为有限像素坐标。
- `CameraCommandKind.Pan`：PositionOrDelta 为世界 XY 位移；`Focus`：世界 XY 目标；`Zoom`：ZoomDelta 为正交尺寸增量（正数缩小画面）。不改变世界对象。非法命令种类或非有限值忽略。
- `CameraBounds(Vector2 minimum, Vector2 maximum)` 为地图世界 XY 矩形。地图负责人通过 GetMapBounds 的公开结果构造它，再调用 SetBounds。未绑定时允许自由移动；零面积、反向、NaN 或 Infinity 边界抛 `ArgumentException`，保留原边界。
- 边界按可见视口的半宽/半高约束相机中心；地图小于视口时对应轴居中，允许显示地图外背景；不为填满视口强行放大镜头。LateUpdate 在宽高比变化后重新限制位置。
- 默认尺寸范围 2—12；仅测试绑定支持中键拖动、滚轮缩放。跟随、震动、平滑过渡、多指缩放和最终屏幕方向未纳入 P0。
- `CameraStateChanged` / `CameraBoundsChanged` 是本组件的 C# 事件，携带只读快照；订阅方负责在禁用时移除，禁止跨场景保留旧相机。

可重复验证见 `Assets/Game/Tests/PlayerPresentation/README.md`。

