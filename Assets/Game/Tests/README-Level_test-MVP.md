# Level_test 首关运行时装配与验收

> 2026-10-13 装配状态。从 `Assets/Game/Scenes/Bootstrap.unity` 开始 Play，经 MainMenu 进入 `Level_test`；直接从 `Level_test` 启动不会创建持久的 GameRoot。

## 已装配

- Bootstrap 的 LevelManager 引用首关守卫、食物、熔炉、障碍配置及守卫 prefab。MechanismSystem 拥有武器和正式机关。
- MechanismsRoot 下只有一把正式 `PF_EscortWeaponMechanism`，初始位置 `(-5, 0)`。临时黄色精灵仅负责显示。武器带实心碰撞体、搬运触发范围和 Kinematic Rigidbody2D，避免被 MapManager 登记为自身的静态路线障碍。
- 临时固定路线：`RouteStart (-5, 0)` → `RouteTurn (-3, 2)` → 原有 `GoalPoint (0, 2)`，ID 为 `level_test_temporary_route`。地图边界、区域、地形和原有静态障碍未改。
- LevelActorSpawner 引用四个史莱姆出生点及一个敌人出生点。两只史莱姆位于武器搬运半径内，预计以最大速度的 50% 起步；另两只分散在后方。敌人位于 `(2.5, 1.5)`。角色由正式 Manager 在运行时生成。
- `CFG_Guard` 首关只生成一名守卫，出生点相对武器为 `(0, -1.2)`，由 GuardManager 管理，运行时实例归入 `EntitiesRoot/GuardRoot`。
- 场景没有独立玩家 Actor 或 PlayerRoot。PresentationRoot 承载 PlayerPresentationRoot、InputSystem、UISystem、FirstLevelPresentationRoot，以及世界坐标放置 Grid；主相机接入 CameraSystem。Grid 使用世界原点、`1×1` 单元、单次确认。运行时 BattleHUD 创建到 `UIRoot/HUDRoot`，放置选择面板和 EventSystem 创建到 `UIRoot`；UI 读取真实快照。缺省父节点仍供隔离测试夹具使用。

## 人工 PlayMode 验收

1. 从 Bootstrap 进入首关；Console 不应有配置缺失、无效路线、武器不在起点或 Player/Grid 初始化错误。
2. Hierarchy 中确认四只史莱姆、一只敌人和一名守卫；Console 应输出 `Level_test initial actors: 4 Slimes, 1 Enemies.`。
3. 检查黄色武器与 HUD：初始搬运人数预计为 `2/4`，武器沿固定路线移动；脱离后速度随人数变化。
4. 检查敌人攻击最近可攻击目标、守卫防守、武器受击、武器摧毁、史莱姆耗尽、抵达终点及结果界面。同帧胜负按已确认的成功优先处理。
5. 检查食物、熔炉、障碍的 Grid 显示、边界、单格占用、总数上限和单次放置。障碍挡住路线时武器应停下，移除后继续。
6. 检查暂停、恢复、失败后重开和场景退出，确认新局重建武器、守卫、史莱姆、事件与 UI 状态。

## 本次验证记录

- 静态：场景 fileID 无重复且局部引用完整；新增 GUID 有对应 `.meta`；武器、路线、Bootstrap、守卫数量、Grid 与 UI 父节点引用已检查；PlayerRoot 已移除；`git diff --check` 通过。
- 编译：`SlimeBattle.Runtime.csproj`、`SlimeBattle.P0.Tests.csproj` 与 `SlimeBattle.Placement.EditMode.Tests.csproj` 的 `dotnet build --no-restore` 均为 0 警告、0 错误；放置测试工程先执行本地 `dotnet restore --ignore-failed-sources` 补齐生成的 assets 文件。Unity 日志曾记录程序集编译和场景导入，但早于最后一次武器 prefab 修改。
- EditMode：本次未在 Unity Test Runner 执行。
- PlayMode / 人工运行：本次未执行。以上行为是验收目标，不代表已通过。最终武器 prefab 仍需在 Unity 重新导入并检查碰撞与路线推进。

## 后续依赖

临时路线待地图负责人确定正式路线；武器黄色显示和三个结构机关 prefab 待美术精灵替换。地图原有区域、地形及静态障碍仍保留。正式场景验收前需完成上述人工 PlayMode 检查。
