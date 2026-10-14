# 首关网格放置隔离验证

本场景位于 `Assets/Game/Tests/PlayerPresentation/Test_FirstLevelPlacement.unity`，仅用于小宸的输入、网格、放置状态与 UI 验收。全部机关对象是 `TEST_ONLY` 彩色方块；武器、史莱姆数量和胜负来自测试夹具。它没有真实 Actor、伤害、机关效果或 Core 结果判定，不能作为正式首关完成的证据。

## 自己操作

使用 Unity **2022.3.62f2c1** 打开项目，双击上面的测试场景，进入 Play Mode 并聚焦 Game 视图。无需修改 Build Settings。

1. 点击底部 FOOD：网格出现。先将鼠标移到地图中央观察吸附预览，再点击目标格子（触屏则点一下地图）：立即出现绿色测试食物方块，退出单次放置并隐藏网格。
2. 选择 FURNACE 后指向已占格：红色预览，点击也不放置；点击空格则立即出现橙色测试方块。测试后端拒绝请求时保留选择，下一次点击空格可重试。
3. 选择 OBSTACLE 后点击另一空格：立即出现灰色方块。指向地图外：紫色预览，点击不放置。边界内点击点及吸附后的格子中心都必须位于边界内；地图边缘不完整格可能因此不可放。
4. 放置中点击 CANCEL 或按 Escape：网格与预览消失。点击 PAUSE：选择面板隐藏；RESUME 后需要重新选择机关。开始拖动 UI 按钮再移到地图时，不应产生世界预览移动。
5. 放置中切换到其他普通应用，再回到 Game：旧选择取消。自动检查通过 Unity 生命周期回调模拟失焦；手工切换窗口需自行复核。
6. 点击右侧 TEST Success / TEST Failure：只模拟上游结果，结果面板显示 TEST ONLY，网格与选择面板隐藏。点击 RESTART 重载此测试场景，确认只保留一套 UI、镜头和网格，旧方块与占用清空。连续重复五次。
7. HUD 应显示测试史莱姆 `4 / 20`、测试武器未接入提示和当前放置提示。测试值不会修改正式配置或正式玩法。

悬停预览随鼠标和镜头移动更新；点击合法世界格子即请求放置，不再锁定待确认格子。取消、重新选择机关、暂停、失焦和重开会清除预览。测试夹具实例化正式 uGUI 选择面板 prefab，但机关仍是 `TEST_ONLY` 后端对象。

测试 Grid：原点 `(0,0,0)`、矩形 XY、无间隙、单位格；地图边界 `(-6,-3)` 到 `(6,3)`；一次选择放置一个对象。正式 `Level_test` 也采用世界原点、`1×1` 单元和一次选择放置一个对象；连续模式仅在 EditMode 覆盖。

## 自动执行

- Test Runner → EditMode → `SlimeBattle.Placement.EditMode.Tests`：网格变换、负坐标、边界、占用复核、失败不占格、单次／连续、重入与释放等。
- 菜单 `SlimeBattle > Tests > Run First Level Placement Play Checks`：先执行已有的 20 项 Player 静态检查，再进入本场景，以虚拟 Mouse / Keyboard / Touchscreen 驱动实际 Input System、uGUI、GridView；执行五次真实测试场景重载，其中一次通过测试夹具的公开重载入口保留按住的触点，另四次点击结果 RESTART。不要在自动检查中操作鼠标，以免干扰虚拟输入。
- 隔离交互检查后，自动入口只读加载现有已注册的 Bootstrap → MainMenu → Level_test，在独立 TEST_ONLY 持久根中装配 FirstLevelPresentationRoot。它验证真实 LevelManager 暂停恢复、输入适配器禁用时的面板恢复、组合根启停、真实重开后的 Context 隔离与重新注入，以及显式模拟结果的 UI 清理；不会保存正式场景。测试机关后端依然是模拟实现。
- 如场景丢失，可用 `SlimeBattle > Tests > Create First Level Placement Scene` 生成，只写入本测试路径。

命令行运行 GUI 检查（报告放在项目外；关闭其他项目编辑器实例）：

```text
Unity.exe -projectPath <project-root> -executeMethod SlimeBattle.Tests.PlayerPresentation.Editor.FirstLevelPlacementPlayChecks.Run -placementReportPath <report-path>.json -placementExitAfterChecks -logFile <log-path>
```

不加 `-batchmode`、`-nographics` 或 `-quit`。编辑器进入 Play Mode 后，关闭可能遮挡 Game 视图的 MCP Setup 提示，再聚焦 Game。报告记录准确 Unity 版本、通过断言与失败位置；旁边的 PNG 为运行画面。

旧 `Run P0 Play Mode Checks` 会临时注册旧场景，不属于本阶段验证入口。本阶段隔离场景使用 Editor 按路径重载；装配检查仅使用已有 Build Settings 和公开 `LevelManager.RequestRestartCurrentLevel()`。两者都不写 Build Settings；现有 Level_test 的装配检查不等于正式首关完整链路联调。

## 正式接入边界

正式 `Level_test` 的放置后端接到 MechanismSystem，由其创建并维护机关；本隔离场景继续使用模拟后端。Android 真机、多指、中文字体、安全区域和最终移动端适配仍需单独验证。

## 历史结果（2026-10-12，已合入 master 548abbb；旧确认交互）

目标版本编译通过；GUI Play Mode 61 / 61（48 项隔离交互 + 13 项真实装配检查）、原有 Player 静态检查 20 / 20 通过。新增 5 项回归覆盖鼠标跨格移动保持选择、再次点击更换目标、确认使用已选格子、触屏选择保持和镜头移动保持世界目标。输入适配器禁用时暂停恢复后选择面板不显示的问题也已修复并覆盖。2026-10-11 的原生窗口操作复核通过鼠标选格确认、真实失焦取消、暂停／恢复、模拟失败结果和重开；本轮在独立项目副本重新执行 GUI 自动回归，保留用户编辑器中的本地场景改动。Edit Mode 计数与既有 Actor AI 失败详情见同目录 FirstLevelPlacement-editmode.json。
