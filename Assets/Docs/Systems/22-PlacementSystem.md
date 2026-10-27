# 22 PlacementSystem 放置系统设计

系统编号：`PLAYER-02`
系统名称：`PlacementSystem`
默认命名空间：`SlimeBattle.Player.Placement`
建议实现目录：`Assets/Game/Scripts/Player/`
生命周期：当前关卡上下文生命周期
状态：放置、Anchor 安全替换和拆除均已有运行时实现；当前通过场景适配器接入 `MechanismSystem`，战役库存与同时在场上限来自配置

协作边界：`MechanismSystem` 是 Map 领域下辖子模块，由小智维护；本系统只通过其公开放置、替换、回收和快照接口协作，不访问机关内部状态。

当前实现要点：

- 选择食物、熔炉或障碍后，`PlacementGridView` 显示独立网格；地图点击经过投影、格子/Anchor 吸附和后端复核后立即提交放置。
- Anchor 替换由 `MechanismSystem.RequestPlaceAtAnchor()` 执行安全事务；失败时保留原机关。`MechanismPlacementBackend` 通过 `IAnchorReplacementBackend` 报告是否支持替换。
- `BeginRemoval()` / `ConfirmRemoval()` 提供持续拆除模式；正式后端调用 `MechanismSystem.RequestRemove()`，清理机关对象、Anchor 占用和动态碰撞阻挡。场景预置机关和已耗尽对象不能拆除。
- 放置库存由 `CampaignResourceLedger` 扣除；`LevelConfig.MaximumActivePlacedMechanisms` 限制同时在场的玩家机关。拆除、消耗或耗尽不返还战役库存；重开本关会恢复入关快照。
- `IsPlacing`、`IsRemoving` 与 `IsInteracting` 分别表示放置、拆除和任一地图交互状态。暂停、重开、结算和取消都会清理交互预览。
- 公共方法返回 `PlacementResult` / `PlacementValidationResult`；`IPlacementBackend` 成功响应需要同步提供机关 ID 和只读快照。`Changed`、`PlacementCompleted`、`RemovalCompleted` 是放置模块的本地反馈，不替代领域事件。

2026-10-16 与 2026-10-19 的实现记录保留为历史进度，文中“安全替换未接入”“资源返还规则待确认”等描述已被当前代码和后续决定取代。

## 1. 职责

- 接收玩家选择的放置物和目标位置；
- 将输入位置通过 `CameraSystem` 转换为世界坐标，再通过 Unity `Grid` 或等价 Grid Authoring 转换为格子坐标；
- 创建、更新和销毁放置预览；
- 在进入放置状态时显示独立的 `PlacementGridView`，在退出、取消、暂停、重开和结算时隐藏并清理；
- 查询 MapManager 提供的地图边界和格子配置，再检查首关放置规则；
- 查询策划配置的特殊 Anchor，并维护普通格子和 Anchor 的占用索引；
- 维护当前关卡的格子占用索引，一个普通格子最多一个玩家放置对象；
- 对允许替换的 Anchor 编排旧对象回收与新对象提交，不能直接修改 MechanismSystem 内部状态；
- 确认合法放置并提交结果；
- 将成功放置通知机关或场景系统。

## 2. 不负责的内容

- 不直接解析底层触摸；
- 不绕过规则把预制体塞进场景；
- 不实现背包、通用掉落物或通用道具系统；第一关放置选择至少包括食物、熔炉和障碍，陷阱/补给是否作为玩家放置物必须按策划和接口确认；
- 不持有战役资源库存；合法放置由后端流程调用 `CampaignResourceLedger` 扣减库存，拆除不返还；机关冷却由 `MechanismSystem` 管理；
- 不决定机关触发后的伤害和状态效果；
- 不让 UI 直接修改地图对象。

## 3. 核心数据

```text
PlacementItemId
PlacementConfig
GridCell
PlacementGridConfig
PlacementPreview
PlacementRequest
PlacementValidationResult
PlacementResult
PlacementAnchorId
PlacementAnchorSnapshot
PlacementBudgetSnapshot
PlacementCooldownSnapshot
```

`PlacementAnchorId` 和 `PlacementAnchorSnapshot` 是当前地图 API 的正式只读数据类型，具体锚点由关卡地图定义提供。

## 4. 必须接口

```csharp
void BeginPlacement(PlacementItemId itemId);
PlacementValidationResult PreviewPlacement(WorldPosition position);
PlacementResult ConfirmPlacement();
void BeginRemoval();
PlacementResult ConfirmRemoval();
bool RotateCurrentPlacement();
void CancelPlacement();
PlacementPreview GetCurrentPreview();
PlacementBudgetSnapshot GetBudgetSnapshot();
```

地图点击由 `PlacementInputAdapter` 转发；玩家点击合法目标时直接调用确认，不需要额外的“确认放置”按钮。`PreviewPlacement(Vector3)` 只负责预览和复核，不会单独创建机关。

## 4.1 首关已确认的放置规则

当前第一关的可放置环境元素至少包括食物、障碍和熔炉；陷阱、补给如需玩家放置必须另行登记允许类型和所有者：

- 首关地图边界内的普通格子必须同时满足地图可放置查询、格子占用、机关配置和后端规则；道路/场景阻挡由 `MapManager.CanOccupy` 提供，而不是 UI 根据贴图推断；
- 一个普通格子只允许一个玩家放置对象，已占用格子和边界外格子拒绝确认；
- L1/L2/L3、SL1/SL2/SL3 等特殊 Anchor 是策划点位白名单，必须按 Anchor 配置检查允许的机关类型和替换策略，不能把任意世界坐标自动视为 Anchor；
- 普通格子重复放置直接拒绝；Anchor 替换只有地图配置允许且后端支持安全替换时才提交。`MechanismSystem.RequestPlaceAtAnchor()` 负责预检、旧对象回收和新对象提交，任何失败都保留旧对象；
- 首关不做地形兼容、复杂碰撞体积、动态 Actor 占用和窄路规则校验；
- 食物、熔炉和障碍的对象创建、运行时状态和触发效果由 `MechanismSystem` 所有；
- 熔炉首关只提供引导源，不向 `StatusManager` 申请寒冷/炙热状态；
- PlacementSystem 不直接修改路线选择、陷阱效果、Actor 状态或关卡胜负；它只提交放置/替换事实；
- PlacementSystem 只读取放置预算并通过后端提交请求；战役库存由 `CampaignResourceLedger` 管理，机关冷却由 `MechanismSystem` 管理。

进入放置状态后，网格覆盖层显示当前地图格子和可用 Anchor；指针移动时预览对象吸附到普通格子或 Anchor，空闲格子/合法 Anchor 显示可放置状态，已占用、越界、类型不匹配或不允许替换的位置显示拒绝状态。预览通过不代表最终确认一定成功；最终确认仍需再次检查当前关卡状态、占用和替换事务。

武器、守卫和史莱姆不是玩家放置对象，不通过本系统创建。它们的场景/运行时位置不计入“一个格子一个玩家放置对象”的首关占用规则；如果后续要求动态 Actor 也占格子，需要重新确认 `MapManager` 和 `MovementSystem` 的契约。

## 5. 对外事件

```text
PlacementStartedEvent
PlacementPreviewChangedEvent
PlacementConfirmedEvent
PlacementRejectedEvent
PlacementCancelledEvent
```

若跨系统需要区分 Anchor 占用或替换，候选事实名可以是 `PlacementAnchorOccupiedEvent` / `PlacementReplacedEvent`；这两个名称在公共 API 评审前不视为已冻结接口，不能直接作为其他系统的隐式依赖。

## 6. 依赖

- `InputSystem`；
- `CameraSystem`；
- `MapManager`；
- `ConfigSystem`；
- `MechanismSystem`；
- Anchor 的稳定位置、允许类型和替换策略来源（由 MapManager/关卡 Authoring 提供只读事实，最终查询接口待评审）；
- `LevelManager`/Core 结果规则组件提供当前关卡阶段和结果状态；
- `SlimeGroupActor` 不直接依赖放置 UI，只读取放置结果和环境查询。

## 7. 关键不变量

1. 预览状态不等于已经放置。
2. 普通格子必须通过地图可放置查询、边界和单格占用检查；Anchor 还必须通过类型白名单和安全替换能力检查。
3. 取消和关卡重开必须销毁预览对象并清理当前选择。
4. `PlacementSystem` 不直接持有机关运行时数据；成功确认或替换必须通过公开请求交给 `MechanismSystem`。

## 8. 待确认项

- 首关与 Castle 的实际 Anchor 列表和地图定义是否与最终策划点位一致；
- Grid 原点、单元尺寸、坐标轴方向以及各场景可放置区域的最终美术验收；
- Castle 奖励配置写 `SecondLevel`，而实际关卡 ID 是 `Castle`；库存发奖需在修正 ID 映射后实测；
- 陷阱/补给配置资产尚未进入 Bootstrap 注册表，运行时适配器当前不读取这些配置；
- 多格机关占位、Actor 动态占用过滤和更复杂地形规则未由当前 PlacementSystem 提供。


## 历史记录：小宸首关实现契约通知（2026-10-11）

本段记录当时的协作边界；“只修改 Player / UI”“机关创建请求尚未接入”等描述不代表当前实现。当前放置规则以本文第 4.1 节及 2026-10-28 代码现状为准。网格、Anchor、放置状态、显示与注入接口的原始契约见 `Assets/Docs/Tasks/首关最小地图-小宸实现与接入.md`。

2026-10-12 的“锁定格子后按确认按钮”交互属于历史版本；当前正式交互已调整为点击合法地图目标后直接放置。

## 补充：场景引用与集成测试边界（2026-10-14）

`PlacementSystem` 是当前放置会话的 plain C# 模块，不直接作为场景组件序列化。`PlacementInputAdapter`、`FirstLevelPresentationRoot` 或专用 Backend Adapter 可以通过 `[SerializeField] private` 引用具体的 Grid、Camera、UI Prefab、地图 Authoring 适配器和实现接口的 `MonoBehaviour`，再在 `LevelStarted` 后连接当前 Context。

小宸可以先用 Mock Backend 在独立测试夹具中验证输入、投影、吸附、预览、确认和清理；集成测试场景由指定集成者把真实地图事实和 `MechanismSystem` 公开请求接入。Placement/UI 不得直接序列化或访问 `LevelContext`、`MechanismSystem` 私有字段，也不得通过场景引用绕过边界、占用、替换和生命周期校验。

## 历史记录：首关通行区域校验补充（2026-10-19）

以下记录的是当时 Road-only 放置校验方案；它不代表当前地图的放置范围。当前合法性由本文第 4.1 节所述地图查询和后端规则共同决定。

FirstLevelPresentationRoot 将 MapManager 的占位查询注入 PlacementSystem。预览网格中心不在任何 Road 多边形中时，返回 `InvalidArea` 并禁止确认；道路内仍继续执行 Anchor、格子占用、地图边界和 Backend 检查。MechanismSystem 在最终创建机关时再次检查地图边界、道路区域中心点和静态阻挡体，避免只依赖预览结果。

道路区域检查仅表示可通行/允许放置的地图事实，不会自动定义 Anchor 白名单。首关当前数据未提供 Anchor，所以普通放置仍按现有配置和机关规则处理；后续拿到策划 Anchor 数据后再录入 MapManager。
