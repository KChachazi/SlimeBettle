# 21 PresentationSystem 表现系统设计

系统编号：`PRESENT-02`  
系统名称：`PresentationSystem`  
默认命名空间：`SlimeBattle.Presentation`  
建议实现目录：`Assets/Game/Scripts/UI/`，表现资产位于 `Assets/Game/Art/` 和 `Assets/Game/Audio/`  
生命周期：当前关卡上下文生命周期，可有应用级资源缓存  
状态：表现待确认

## 1. 职责

- 监听玩法系统发布的已完成结果；
- 将角色、机关、状态和关卡结果映射为动画、特效、音效和震屏；
- 管理表现对象的创建、复用和销毁；
- 在表现资源缺失时提供占位或安全降级。

## 2. 不负责的内容

- 不决定伤害、死亡、胜负和机关触发；
- 不通过动画事件直接修改核心玩法数据；
- 不拥有角色和机关的最终运行时状态；
- 不要求摄像机或 UI 可见才执行核心规则。

## 3. 核心数据

```text
PresentationEvent
AnimationRequest
VfxRequest
AudioRequest
PresentationBinding
```

## 4. 必须接口

```csharp
void HandlePresentationEvent(PresentationEvent eventData);
void PlayAnimation(AnimationRequest request);
void PlayVfx(VfxRequest request);
void PlayAudio(AudioRequest request);
void ClearRuntimePresentation();
```

这些接口主要供表现层内部和测试使用。玩法系统发布的是结果事件，不应直接要求某个具体动画对象存在。

## 5. 对外事件

表现系统默认不向玩法系统发布规则事件。可以发布 `PresentationCompletedEvent` 供调试或 UI 使用，但玩法不能等待该事件才能继续。

## 6. 依赖

- `LevelEventBus` 的结果事件；
- 角色、机关和关卡的只读快照；
- 动画、粒子、音频和摄像机适配；
- 不反向依赖玩法规则系统的私有实现。

## 7. 关键不变量

1. 表现事件丢失不会改变玩法结果。
2. 场景重开时所有运行时表现对象和表现订阅都清理。
3. 表现层使用稳定 ID 或快照查找对象，不能依赖已经销毁的场景引用。

## 8. 待确认项

- 美术动画和特效资源规范；
- 是否引入对象池；
- 音频和震屏是否进入首个可玩闭环；
- 表现事件由各系统直接发布，还是由单独适配层转换。

