# 小宸第一关负责部分：补齐记录

日期：2026-10-16；验证基线包含 `origin/master 21a0409`，合并提交 `50fd34a`；本记录与实现、测试证据一并提交评审。范围为 Player/Input/Camera/Grid/Placement/UI、对应 Prefab、测试与接入文档；先前用户授权的彩色参考图 UI 修改已保留。

当前能在小宸侧独立完成的缺项已补齐。可签署“Player/UI 阶段交付通过，跨系统替换与真实终点结果待联调”；不能签署完整第一关验收。

## 已补齐

| 项目 | 当前行为与证据 |
| --- | --- |
| Snap Anchor 接入 | 组合根读取当前 MapManager.GetPlacementAnchors；六个真实 L/SL Anchor 已通过定向测试。像素半径按同一地图定义比例换算，吸附到精确地图位置，保留原 AnchorId |
| 类型和边界 | 最近 Anchor 先吸附再验证允许类型，同距按 ID 确定；类型不匹配不会绕回普通格，指针/目标越界拒绝，普通格仍保持单格唯一 |
| 替换反馈 | 配置禁止替换和安全替换接口未接入是不同状态；两者均不调用创建或删除旧对象。等待所属系统提供保证失败保留旧对象的事务 |
| 网格和 Anchor 视图 | 仅在运行中的放置状态显示编号、边框、占用和预览；取消、暂停、结果、禁用与卸载按已有生命周期清理自有视图。彩色布局保留 |
| HUD 关联信息 | 无上游 RouteHint 时显示选中 Anchor 的真实路线/点位关联；不推断阻断/陷阱状态。入洞人数和等待窗口缺少真实来源时仍显示未接入 |
| Prefab 与接线 | PF_FirstLevelPlayer 已引用 MAP_FirstLevel 的比例；初始化可显式传入地图定义，缺比例而存在 Anchor 时拒绝提交 |
| 当前输入与重开 | 点击/轻触合法地图位置即提交；真实 LevelManager 重开丢弃旧注入后端，默认从新 Context 的正式 MechanismSystem 创建；静态/GUI 检查覆盖 UI 屏蔽、镜头、焦点、阶段、重绑定与重载 |
| 文档 | 新增兼容扩展通知；标明旧锁定/确认、Pending 后端和上限 30 的历史身份，当前正式配置为 20；全局 API 表和 Placement 设计补充当前实现边界 |

## 本轮验证

Unity 2022.3.62f2c1。证据位于 `Assets/Game/Tests/PlayerPresentation/PlayerCompletion/`。

| 验证 | 结果 | 范围 |
| --- | --- | --- |
| Player/UI EditMode | 53 / 53 | 原 33 项及新增 20 项 Anchor/关联信息回归 |
| 定向集成 EditMode | 2 / 2 | 真实地图 Prefab 六个 Anchor、正式 MechanismSystem 创建与上限 |
| UI PlayMode | 6 / 6 | 实际彩色 UI Prefab、示例 Anchor 精确位置、拒绝替换、模态与清理 |
| GUI 生命周期 | 57 / 57 断言 | 虚拟鼠标/触屏、失焦回调、五次隔离场景重载、真实 Bootstrap/Level_test 暂停与重开、新 Context 正式创建、显式结果清理 |
| 既有 Player 静态检查 | 20 / 20 断言 | 与 GUI 入口一起执行，镜头/输入订阅/UI 适配等 |

真实地图测试在已有 P0 测试程序集内沿用 Map 夹具构造权限；生产 Player 只消费公开查询，没有增加 friend 权限。GUI 首次尝试在 Food 选择处失败，保持 Unity 焦点后重跑完整通过。测试中的显式结果报告仍为模拟上游，不等同于新 Core 自动胜负。没有重跑全仓测试或 Android 真机。

## 尚需对应负责人提供

1. 小智：公开安全替换事务，确保新对象创建失败保留旧对象；Player 当前明确拒绝该路径，不能用 RequestRemove + RequestPlace 拼接替代。
2. 小茶/终点规则所有者：真实入洞计数、等待窗口、最新结果来源及首关组合场景。现有 IFirstLevelStatusSource / UiState 展示入口已准备好。
3. 后续设备验收：Android 真机、多指和安全区域。本轮虚拟 Touchscreen 不能替代设备测试。

本轮没有修改 Core、World、Mechanism、Actors、Status 实现或 Build Settings，没有保存正式集成场景。`TestScene.unity.meta` 既有冲突标记仍由原维护者/总计划确认 GUID 后处理。前序 UI 任务的 MainMenu 修改不属于本轮新增的场景改动。
