# 22 PlacementSystem 放置系统设计

系统编号：`PLAYER-02`
系统名称：`PlacementSystem`
默认命名空间：`SlimeBattle.Player.Placement`
建议实现目录：`Assets/Game/Scripts/Player/`
生命周期：当前关卡上下文生命周期
状态：首关采用 Grid + Snap Anchor + 独立网格可视化；普通格子/Anchor 替换边界已确认，交互与资源规则仍待联调

协作边界：`MechanismSystem` 是 Map 领域下辖子模块，由小智维护；本系统只通过其公开放置、替换、回收和快照接口协作，不访问机关内部状态。

2026-10-19 拆除实现：按当前需求接通 `FirstLevel` 所用 HUD Prefab 的“拆除”按钮。`BeginRemoval()` 进入持续拆除模式，`PreviewPlacement()` 按指针所在格查询活跃玩家机关，`ConfirmRemoval()` 重新校验关卡阶段和目标身份后通过可选的 `IPlacementRemovalBackend.RequestRemoval()` 回收；正式后端调用已有 `MechanismSystem.RequestRemove()`，由其清理碰撞阻挡、Anchor 占用和对象并启动配置冷却。空格、已消耗/已回收对象和场景预置机关不可拆除。点击取消、再次点击拆除、选择放置物、暂停、重开和结算退出拆除。预览和网格通过 `IsInteracting` 同时支持放置与拆除，`IsPlacing` 仍只表示放置。资源返还规则保持待确认；本次不添加库存/退款规则。

2026-10-16 实现状态：已消费现有 MapManager 的 Anchor 只读快照，支持像素比例换算、最近点吸附、类型限制、独立标记和确认复核；点击地图即提交。`PlacementPreview.Anchor` / `PlacementRequest.AnchorId` 保留原地图身份。当前没有公开的安全替换事务，因此已占用 Anchor 区分配置禁止替换与安全替换未接入，两者均拒绝并保留旧对象。下文的替换职责仍是目标设计，不能视为已实现。接口兼容细节见小宸实现与接入通知及全局 API 表 2026-10-16 补充。

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
- 不直接扣除资源，资源/次数/冷却所有权待策划方案确认；
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

`PlacementAnchorId` 和 `PlacementAnchorSnapshot` 目前是设计层候选数据名，用于表达“特殊点位事实”；最终类型、字段和查询接口须在公共 API 评审后冻结。

## 4. 必须接口

```csharp
void BeginPlacement(PlacementItemId itemId);
PlacementValidationResult PreviewPlacement(WorldPosition position);
PlacementResult ConfirmPlacement();
void CancelPlacement();
PlacementPreview GetCurrentPreview();
PlacementBudgetSnapshot GetBudgetSnapshot();
```

## 4.1 首关已确认的放置规则

当前第一关的可放置环境元素至少包括食物、障碍和熔炉；陷阱、补给如需玩家放置必须另行登记允许类型和所有者：

- 首关地图边界内的普通格子默认合法；
- 一个普通格子只允许一个玩家放置对象，已占用格子和边界外格子拒绝确认；
- L1/L2/L3、SL1/SL2/SL3 等特殊 Anchor 是策划点位白名单，必须按 Anchor 配置检查允许的机关类型和替换策略，不能把任意世界坐标自动视为 Anchor；
- 普通格子重复放置直接拒绝；Anchor 替换只有在配置允许时才执行。当前第一关 L/SL Anchor 的障碍允许替换旧障碍，旧对象必须先通过 MechanismSystem 回收；新对象创建失败时必须保留旧对象，不能出现空槽或双重占用；
- 首关不做地形兼容、复杂碰撞体积、动态 Actor 占用和窄路规则校验；
- 食物、熔炉和障碍的对象创建、运行时状态和触发效果由 `MechanismSystem` 所有；
- 熔炉首关只提供引导源，不向 `StatusManager` 申请寒冷/炙热状态；
- PlacementSystem 不直接修改路线选择、陷阱效果、Actor 状态或关卡胜负；它只提交放置/替换事实；
- 资源数量、总放置上限、单类冷却和拆除规则继续由策划/配置确认，不能在 PlacementSystem 中硬编码。

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
2. 首关普通格子只有地图边界和单格占用会使放置请求失败；Anchor 还必须通过类型白名单和配置的替换策略检查；复杂地形合法性暂不实现。
3. 取消和关卡重开必须销毁预览对象并清理当前选择。
4. `PlacementSystem` 不直接持有机关运行时数据；成功确认或替换必须通过公开请求交给 `MechanismSystem`。

## 8. 待确认项

- 首关测试是否允许三种机关无限选择，还是需要配置次数/冷却；
- 物品是否有数量、冷却或消耗；
- Grid 原点、单元尺寸、坐标轴方向和地图边界来自场景配置的具体字段；
- Anchor Authoring 的最终类型名、查询接口、稳定 ID 格式和场景装配方式；
- 地形区域、静态障碍、地图边界和动态 Actor 对不同放置物的后续占用过滤规则；
- 陷阱/补给是否允许玩家放置及其所有者；
- 放置对象是否属于 `MechanismSystem`（食物/熔炉/障碍已确认；陷阱/补给待确认）；
- 总放置上限和是否按场景/周目变化；
- 食物、障碍、熔炉的正式配置名、图标、范围和冷却；
- 玩家是否可以在暂停、战斗或结算阶段放置。


## 小宸首关实现契约通知（2026-10-11）

网格、Anchor、放置状态、显示与注入接口的新增契约见 `Assets/Docs/Tasks/首关最小地图-小宸实现与接入.md`。本次只修改 Player / UI 与隔离测试；当前机关创建请求、陷阱/补给所有权和武器/Core 结果接口存在文档与代码差异，正式适配须等待所有者确认，不在调用方绕过规则。

当前实现使用 `SlimeBattle.Player.Placement` 命名空间，确认返回 `PlacementResult`，预览返回 `PlacementValidationResult`；本地 `Changed / PlacementCompleted` 只用于显示与请求反馈，不替代第 5 节待接入的全局领域事实。`IPlacementBackend` 的成功返回必须同步提供有效机关 ID 及对应只读快照。运行阶段守卫、边界和单格占用在最终确认时复核；其他系统尚未完成的新规则不在调用方绕过。地图点击更新并锁定预览，确认由显式按钮或公开方法提交。

2026-10-12 交互修复：输入适配器在地图 PrimaryAction 后锁定当前世界格子，鼠标移向确认按钮或镜头变化不会改选；再次点击地图可替换目标。选格前仍允许悬停预览。取消、切换机关、暂停、失焦、禁用和重开清除锁定。锁定仅为输入适配层的交互状态，不改变 PlacementSystem 的公共预览/确认接口，也不替代最终合法性复核。

## 补充：场景引用与集成测试边界（2026-10-14）

`PlacementSystem` 是当前放置会话的 plain C# 模块，不直接作为场景组件序列化。`PlacementInputAdapter`、`FirstLevelPresentationRoot` 或专用 Backend Adapter 可以通过 `[SerializeField] private` 引用具体的 Grid、Camera、UI Prefab、地图 Authoring 适配器和实现接口的 `MonoBehaviour`，再在 `LevelStarted` 后连接当前 Context。

小宸可以先用 Mock Backend 在独立测试夹具中验证输入、投影、吸附、预览、确认和清理；集成测试场景由指定集成者把真实地图事实和 `MechanismSystem` 公开请求接入。Placement/UI 不得直接序列化或访问 `LevelContext`、`MechanismSystem` 私有字段，也不得通过场景引用绕过边界、占用、替换和生命周期校验。

## 首关通行区域校验补充（2026-10-19）

FirstLevelPresentationRoot 将 MapManager 的占位查询注入 PlacementSystem。预览网格中心不在任何 Road 多边形中时，返回 `InvalidArea` 并禁止确认；道路内仍继续执行 Anchor、格子占用、地图边界和 Backend 检查。MechanismSystem 在最终创建机关时再次检查地图边界、道路区域中心点和静态阻挡体，避免只依赖预览结果。

道路区域检查仅表示可通行/允许放置的地图事实，不会自动定义 Anchor 白名单。首关当前数据未提供 Anchor，所以普通放置仍按现有配置和机关规则处理；后续拿到策划 Anchor 数据后再录入 MapManager。
