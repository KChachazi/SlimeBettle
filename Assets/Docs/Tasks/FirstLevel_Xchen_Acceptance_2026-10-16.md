# 小宸第一关负责部分：验收核对

> 后续补齐状态：用户随后授权补齐可独立完成项，已完成 Anchor 消费/显示/类型校验/安全拒绝、当前输入生命周期与真实后端定向验证。最新结论和证据见 [补齐记录](FirstLevel_Xchen_Completion_2026-10-16.md)。下文保留补齐前的审核快照，不再代表最新缺项清单。

核对日期：2026-10-16。对象：当前 `xchen/feat-first-level-ui-prefabs` 工作区，HEAD 为 `50fd34a`，已包含 `origin/master 21a0409`；核对包含尚未提交的彩色 UI 修改。

**结论：普通 Grid、输入/镜头基础、三种机关选择、基础 UI 和 Prefab 可作为阶段性交付验收；最新第一关的完整 Player/UI 验收尚未通过。主要缺项是 Snap Anchor、类型/替换反馈、真实终点/等待数据接入及集成运行证据。**

本轮只进行了文档、代码、资产和既有测试证据核对，没有重新执行 Unity 测试，没有修改玩法实现，也没有把测试夹具结果提升为正式首关闭环证据。

## 1. 验收依据与证据口径

主要依据为 `首关最小地图-小宸候选任务单.md`（正文状态为 2026-10-15 已确认分工），特别是第 1、6、6.1、7 节；补充读取小宸实现与接入通知、小智实现与接入记录、小茶候选任务单和第一关可行性审查。

历史文档对仓库状态的描述，以当前代码和最新测试文件核实；用户在本会话要求按照八张例图的版式调整彩色 UI，作为主菜单及展示页面的后续授权。该授权不自动改变首关 Anchor、机关生命周期或结果规则。

| 证据 | 已核实结果 | 能证明的范围 |
| --- | --- | --- |
| `Assets/Game/Tests/PlayerPresentation/ReferenceUI/EditMode.xml` | 2026-10-16 14:05（北京时间），33 通过、0 失败：Placement 25 项、UI 8 项 | 普通网格变换/边界/占用/确认、失败不占格、单次/连续、释放、Prefab 引用、中文字体和显示数据边界 |
| `Assets/Game/Tests/PlayerPresentation/ReferenceUI/PlayMode.xml` | 2026-10-16 14:06（北京时间），5 通过、0 失败 | 实际 UI Prefab、按钮重绑定、三种选择、取消、模拟暂停/结果/重开、页面开关和 EventSystem raycast |
| `FirstLevelUiFixture.cs` | 明确使用 `TestPlacementBackend`、固定测试边界、`SetPhase` 和本地 `Restart` | 上述 5 项不是正式 LevelManager 重载、真实机关创建或新 Core 自动胜负的验证 |
| `FirstLevelPlacement-playmode.json` | 历史 61 项断言，包含选格锁定/确认按钮和旧装配检查 | 历史证据；当前已改成点击合法格立即放置，不能整体复用为当前版本通过记录 |
| `FirstLevelMechanismPlacementEditModeTests.cs` | 存在真实 MechanismSystem 后端的独立测试 | 不在上述 33 项 XML 中；本轮未执行，不补记为通过 |

旧文档中的 55/56 全量 EditMode、20 项静态检查、61 项 GUI 检查均保留历史身份；本轮没有复跑全量项目，也没有核实旧 Actor AI 失败在当前主分支的状态。

## 2. 交付物与验收项逐项核对

“通过”以下表所列证据范围为限；“部分通过”意味着已有基础实现，但不能签署整项完成。

| 项目 | 结论 | 当前证据和缺口 |
| --- | --- | --- |
| Input → Camera → Grid 坐标链 | 基础通过 | `PlacementInputAdapter.cs:81` 调用 `CameraSystem.ScreenToWorld`，`PlacementGridAuthoring.cs:27` 使用 Unity Grid；当前 EditMode 覆盖负坐标、原点、尺寸、旋转、缩放。新点击语义下的完整实物输入、镜头变化与移动端复验仍缺 |
| Food / Furnace / Obstacle 选择与单次/连续模式 | 通过 | `PlacementTypes.cs` 仅三种机关，`PlacementSystem` 维护选择与确认；当前 Prefab 为 Single，连续模式有 EditMode 证据；未把资源规则硬编码到选择 UI |
| 普通格边界、单格占用、失败反馈 | 通过 | `PlacementSystem.cs:136` 重新检查边界与后端只读占用快照；普通占用格与越界格不能提交，失败不占格；当前 25 项 Placement 单测覆盖相关路径 |
| 网格/预览出现及取消、暂停、结果清理 | 隔离范围通过 | `PlacementGridView.cs:53` 根据放置/阶段显示并销毁预览；当前 PlayMode 覆盖选择、取消、模拟暂停/结果和夹具清理；真实关卡重开、场景卸载和新 Context 重新绑定需当前版本集成验证 |
| 可用 Snap Anchor 显示、吸附和身份区分 | 未实现 | Player/UI 无 `GetPlacementAnchors` / `TryGetPlacementAnchor` 消费路径，GridView 只绘制格线、占用格与普通格预览 |
| Anchor 允许类型、替换策略与拒绝预览 | 未实现 | `PlacementValidationStatus` 没有 Anchor、类型不匹配、可替换/替换拒绝状态；`PlacementRequest` 只有类型、格子、世界中心；普通 Occupied 会直接拒绝，不能进入 L/SL 替换流程 |
| 通过公开接口提交机关创建并反馈 | 静态接线通过，正式联调待验 | `MechanismPlacementBackend.cs:27` 调用公开 `MechanismSystem.RequestPlace(configId, position)`，创建归机关系统；结果返回 `PlacementResult`。最新 5 项 PlayMode 使用 Mock，不能证明正式创建已运行通过 |
| 史莱姆数量与上限显示 | 显示实现通过 | `FirstLevelUiSnapshotSource.cs:35` 读取公开 SlimeManager 快照和真实上限；未知显示 `--`，非 20 显示配置差异；当前 `CFG_Level_Test.asset:16` 已为 20。上限生成规则属于 Actor/配置负责人，不由 UI 验收代替 |
| 终点洞穴人数、等待窗口、路线/Anchor 提示 | 展示位置完成，真实接入未完成 | `FirstLevelStatusProvider.cs:11` 默认返回空状态；`BattleHudView.cs:80` 到 `:82` 显示未接入。没有生产状态源填入这三项真实数据 |
| 当前成功/失败与旧武器兼容显示 | 部分通过 | UI 按 LevelManager 阶段展示，不自行从武器生命推断首关结果；旧武器显示可选。当前 Core 仍监听武器事件，最新洞穴人数/等待规则尚未接入，不能签署正式首关结果验收 |
| Camera / Grid / HUD / 选择栏 / Player 组合交付 | 通过 | 四个首关 Prefab 存在，`PF_FirstLevelPlayer` 预接线，使用具体 MonoBehaviour 后端/状态源；当前 EditMode 检查缺失脚本、引用与组件重复，UI README 提供 Inspector 接线清单 |
| 不复制机关、Actor 或胜负规则 | 通过 | Player/UI 的正式放置路径只发公开请求；GridView 创建/销毁的是视图资源；测试机关和模拟结果明确标记 TEST_ONLY |
| 不修改正式场景、Build Settings 或他人系统 | 有后续授权扩展 | 与 `origin/master` 比较，Core/World/Mechanism/Actors/Status 和 Build Settings 无本分支差异；MainMenu 有本次 UI 改动，由用户八图 UI 请求授权。不能把原任务的“正式场景零修改”按字面记为通过，也不应判为未经授权越界 |
| PR 描述、公共契约和完整运行验收记录 | 待补齐 | 当前新 UI 存在未提交修改；本聊天无已附 PR，本轮未查询远端 PR。现有实现通知与当前交互/API 有差异；正式机关、Anchor、真实结果和当前生命周期联调证据仍缺 |

## 3. 主要阻塞与责任归属

### A. Anchor 是当前 Player/UI 的实际缺项

当前 Map 已提供 `MapManager.TryGetPlacementAnchor`（`:203`）和 `GetPlacementAnchors`（`:213`）。`PlacementAnchorSnapshot` 包含 Position、SnapRadiusPixels、AcceptedMechanismKinds、AllowsReplacement 和路线/点关联；`MAP_FirstLevel.asset:326` 起包含 L1–L3、SL1–SL3 六个 Anchor。

因此，不能继续把整个 Anchor 缺项概括为“地图没有查询 API”。小宸仍需完成对已提供只读事实的吸附、可视化、允许类型和预览反馈；若公开契约仍待团队确认，应明确记录确认状态。接入还需核对像素半径到世界单位的转换，不能直接把 SnapRadiusPixels 当世界距离。

替换方面，当前 MechanismSystem 有 RequestPlace 和 RequestRemove，未找到保证失败时保留旧对象的公开原子替换契约。该事务由小智维护，小茶负责集成适配；小宸不得用“先删再放”绕过。完整 L/SL 验收由这三部分共同阻塞。

### B. 真实终点和等待数据受 Core/Actors 依赖阻塞

小宸已经提供显示字段与未接入降级。当前 `LevelResultRules.cs:22` 到 `:24` 仍订阅 WeaponReachedGoalEvent、WeaponDestroyedEvent 和 SlimeDepletedEvent；未形成最新“至少 6 只入洞 + 等待窗口”的结果输入。

终点统计事实和 Core 结果来源由小茶负责接入；小宸消费公开快照/事件并显示。缺少来源不要求 UI 自行推算，但此项验收仍应保持待验。

### C. 当前集成与生命周期证据不足

最新 5 项 PlayMode 中，暂停、结果和重开来自夹具；不能作为正式 LevelManager 新 Context、真实 Map/Mechanism、场景卸载或新终点规则的通过证据。`FirstLevelPresentationRoot` 已具备 CreateSession、LevelStarted、阶段取消、禁用释放路径，但需要在当前代码上补正式集成检查。

小宸负责模块、适配入口和测试交付；按任务单，小茶负责首关组合场景及跨模块装配。仓库未找到命名为 `FirstLevelComposition_TEST` 的场景，不能把此前脚本中的临时组合根等同于最新完整集成场景。

## 4. 文档需要同步的事实

1. 小宸实现通知 `:13`、`:59` 仍写选格锁定、确认按钮；当前 `PlacementInputAdapter.cs:51` 到 `:57` 是合法地图点击立即确认。
2. 实现通知 `:19` 所述“只能传入已生成 Authoring”的创建限制已过时；当前已有公开 ConfigId + position 创建入口，创建也确实归 MechanismSystem。
3. 实现通知 `:64` 所述 Pending/不可用后端已过时；当前 `MechanismPlacementBackend` 在三种配置齐全时可用，并向正式机关系统提交请求。
4. 实现通知 `:65` 的首关上限仍为 30 已过时；当前 Level_test 配置为 20。另一个 PlayerPresentation 测试配置仍为 30，不能据此判定首关仍为 30。
5. 实现通知 `:74` 与可行性审查中的 Map/Anchor 状态早于最新地图合并；应记录“已有只读查询，Player/UI 尚未消费，替换事务仍缺”的当前事实。
6. 背包、图鉴和主菜单属于后续 UI 请求的展示扩展，没有库存、掉落或通用道具系统。原任务排除这些玩法系统的边界仍有效。

上述原文未在本轮改写，以保留历史记录；本报告作为截至当前工作区的核对补充。

## 5. 额外发现与验收建议

`Assets/Game/Tests/TestScene.unity.meta:2`、`:4`、`:6` 仍有已提交的合并冲突标记。此问题在任务文档中已经列出，应由总计划/原维护者确认 GUID 后处理，本轮未覆盖任意一方。它不抹去 33+5 项局部通过证据，但仍是全仓交付卫生问题。

建议本次签署为“基础 Player/UI 阶段性交付通过，最新第一关完整验收待完成”。后续顺序：先对齐当前文档和公开契约 → 接入已有 Anchor 查询与预览反馈 → 接入所属系统提供的安全替换和真实终点/等待快照 → 在小茶维护的组合夹具中复验真实放置、重开、结算及卸载。Android 真机、触摸与安全区保持未验证。
