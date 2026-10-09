# P0 小宸部分：可重复测试

目标编辑器：**Unity 2022.3.62f2c1**。测试只覆盖输入、镜头、基础 UI 与现有 Core 的流程接入，不代替团队完整战斗验收。

## 启动

1. 用指定 Unity 打开项目，等待包恢复。确认 Input System 为 1.7.0，Console 没有编译错误；不要改为旧 Input Manager。
2. 退出 Play Mode，选择菜单 **SlimeBattle → Tests → Run P0 Player Checks**。成功时 Console 输出 `P0 Player Checks: PASS (14 assertions)`。异常表示检查失败，不应继续合并。
3. 选择 **SlimeBattle → Tests → Open P0 Player Bootstrap**。菜单保留现有 Build Settings 场景顺序，只启用或追加本目录两张测试场景，然后打开 Bootstrap_P0Player。
4. 点击 Play。测试启动脚本创建/使用现有 GameRoot，并通过 RequestEnterLevel 加载 Test_PlayerPresentation。应看到网格、青绿色 InputProbe、目标 HUD、暂停按钮和底部两个 TEST 结果按钮。

菜单修改的 `ProjectSettings/EditorBuildSettings.asset` 属于本地测试设置，验证结束后检查 diff；不要顺手提交它到本 PR。也可手动把本目录两张场景加入 Build Settings，再打开 Bootstrap_P0Player。直接打开测试场景时会通过 GameRoot 重新加载一次，以建立真实 LevelContext。

## 桌面操作与预期

| 步骤 | 操作 | 预期 |
| --- | --- | --- |
| 基础输入 | 按 WASD / 方向键，松开 | 青色输入探针移动并停止；这是测试适配器，不是正式 Actor |
| 主操作 | 点击网格 | 输入探针移动到投影位置，Z=0 |
| 攻击请求 | 按一次空格 | 探针闪橙色，HUD 的 attack request 次数增加 1；不模拟伤害 |
| UI 屏蔽 | 在 HUD 或 TEST 按钮上按下，再拖到网格释放 | 不产生世界点击，不移动探针；UI 起始手势不会转成玩法手势 |
| 镜头 | 网格上按中键拖动；滚轮缩放 | 镜头移动/缩放，探针世界位置不变 |
| 镜头边界 | 持续向边缘拖动、缩放并改变 Game 视图宽高比 | 按视口尺寸限制边界；视口比地图大时地图居中，无 NaN 或反向夹取 |
| 暂停 | 按 Esc 或点击 Pause | 显示暂停面板；探针停止，方向/空格/点击不提交游戏请求 |
| 恢复 | 按 Esc 或点击 Resume | 回到 Running；暂停前按住的方向需要先松开再按，不应自动恢复旧移动 |
| 成功 | Running 时点击 TEST: SUCCESS | Core 进入 Succeeded，显示结果和 Restart；禁止游戏输入 |
| 失败 | 重开后点击 TEST: FAILURE | Core 进入 Failed，显示结果和 Restart；禁止游戏输入 |
| 重开 | 暂停或结果面板点击 Restart | 重新加载当前测试场景，探针/请求次数/镜头/结果恢复初始值 |
| 重复重开 | 连续快速点击 Restart，再重复成功/失败/重开至少 5 次 | 仅一个加载流程；没有重复 HUD、镜头、EventSystem 或成倍输入回调 |

编辑器检查自动验证：投影往返、小地图居中、保留 Z、可见视口边界、缩放下限、非法边界拒绝、虚拟键盘语义移动、快照非消费读取、UI 模式屏蔽、跨模式按键释放、禁用不产生指令、无关卡重开拒绝、UI 不改变关卡状态、UI 重新绑定时查询当前状态。检查只创建临时对象与虚拟键盘，在 finally 中销毁并恢复原 Input System 更新模式。

## 异常与生命周期

1. **失焦**：持续移动时切换到另一窗口，再切回。探针不应继续旧方向；按键回中立后才能继续。
2. **按住键重开**：保持移动或空格时，通过 UI 重开。新关卡不应继承旧按下状态。
3. **晚启用 UI**：Play 中禁用 Systems 上的 UISystem，点击 TEST 结果按钮，再启用。它应查询当前 Core 阶段并显示相应结果，不需要重放事件历史。
4. **UI 缺失**：Play 中禁用 BattleHUD；输入探针仍可处理玩法输入，底部测试按钮仍可向 Core 提交模拟结果。恢复 BattleHUD 后按钮监听应恢复。
5. **缺失快照源**：在独立接入场景不传 IUiSnapshotSource；应显示 Waiting for objective data 与 `--` 数量，禁止把未接入当作零存活。
6. **加载失败**：在 Play 前从 Build Settings 禁用测试关卡，使用 Bootstrap 启动。现有 Core 发布 LevelLoadFailed，Console/事件可观察错误；没有测试关卡 UI 时不承诺显示错误面板。恢复注册后再测试。关卡内重开失败则 HUD 显示加载失败提示，并解除按钮 pending。
7. **非法镜头边界**：编辑器检查调用零面积边界，必须抛 ArgumentException，不能把非法数据传到 Transform。

## 触摸与手柄

- 在真实触屏或 Unity Input Debugger / Device Simulator 中单指点击空白网格，确认主触点与鼠标使用相同的 PrimaryAction 事件；从 UI 区开始的手势不能穿透。
- 如有手柄，左摇杆产生 Move，南键产生 Attack，Start 切换暂停；UI 使用 InputSystemUIInputModule 默认导航。
- 本次没有虚拟移动摇杆、多指缩放与正式 Android 屏幕方向；编辑器鼠标通过不能代替触屏和真机验证。

## 团队集成验收

1. 把输入事件接到 Actor / MovementSystem 的公开入口，把 Attack 请求接到团队确定的公开攻击意图入口。
2. 把地图公开边界转为 CameraBounds；由 IUiSnapshotSource 适配 ObjectiveSystem / Manager 的公开快照。
3. 从团队正式 Bootstrap 进入包含真实史莱姆/敌人的测试关卡，确认攻击和状态结算后由目标系统确认结果，UI 显示原因并请求重开。
4. 检查重开后真实 Actor ID、伤害计时器、旧事件和输入指令失效。此项属于各系统联合验收，不由本目录的探针和模拟结果宣称完成。

## 当前执行记录

2026-10-09：开发电脑没有 Unity 编辑器；上述 Unity 检查、Play Mode、视觉与真机步骤**均未执行**。当前只完成仓库静态检查和 C# 语法检查（结果见 PR），Draft PR 在目标版本实际验收前不得合并。

测试场景所有运行时对象由测试夹具创建，正式场景与 Build Settings 未随 PR 改动。Assets/Game/Tests/ 下脚本没有自动运行属性，只在引用它们的测试场景或主动菜单调用时执行。
