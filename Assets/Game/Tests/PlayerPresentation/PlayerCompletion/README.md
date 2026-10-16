# 2026-10-16 小宸补齐验证

- `EditMode.xml`：53 项 Player/UI 测试，含 20 项 Anchor/关联信息回归。
- `FormalIntegration.xml`：2 项定向 EditMode：现有地图 Prefab 的六个 Anchor 和正式 MechanismSystem 后端。
- `PlayMode.xml`：6 项实际 UI Prefab 测试；机关、示例 Anchor 和阶段仍为 TEST_ONLY。
- `PlacementLifecycle.json`：57 项 GUI 断言，含虚拟鼠标/触屏、镜头、焦点、五次场景重载、真实 LevelManager 重开后的新 Context 正式后端创建。结果由显式 ReportResult 模拟，未覆盖新 Core 自动胜负。
- `AnchorPreview.png`：Unity 1280×720 渲染，两个 TEST_ONLY 示例点位；彩色版式，精确 Anchor 位置与编号。

复现入口见上级 `FirstLevelPlacement-README.md`。首关安全替换、真实终点/等待窗口来源与 Android 真机不在通过范围。旧 `ReferenceUI/` 和 `FirstLevelPlacement-*.json` 保留其原始时间与历史身份。
