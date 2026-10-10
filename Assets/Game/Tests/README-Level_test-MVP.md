# Level_test MVP 手动验收

1. 在 Unity 中从 `Assets/Game/Scenes/Bootstrap.unity` 开始 Play（不要直接从 `Level_test` 开始；正式 `GameRoot` 在 Bootstrap）。
2. Bootstrap 应自动进入 MainMenu。点击 Start，应进入 `Level_test`。
3. Hierarchy 中的 `EntitiesRoot/SlimeRoot` 下应有一个 `PF_Slime_Green(Clone)`，`EntitiesRoot/EnemyRoot` 为空；史莱姆由 `LevelContext` 中的 `SlimeManager` 从正式预制体生成，不是预先摆放的场景角色。Console 应输出 `Level_test initial actors: 1 Slimes, 0 Enemies.`
4. 场景中央的黄色 `PF_EscortWeapon` 是护送对象的静态可视预制体；生命值由 `ObjectiveSystem` 的 `WeaponConfig` 初始化为 100，不保存在视觉预制体中。

`LevelActorSpawner` 为当前测试关卡绑定只提供随机漫游点的 `IActorPerceptionSource`，并通过 `LevelContext.BindActorPorts` 将移动请求接到 MovementSystem。SlimeActor / EnemyActor 的 `BasicSlimeDecision` / `BasicEnemyDecision` 会产生 Move 意图，ActorStateMachine 接受后提交移动；MovementSystem 在 FixedUpdate 驱动动态 Rigidbody2D。进入 Play 后，观察角色的位置持续变化即可确认这条链路。随机漫游不提供敌对目标或效果端口，因此本场景不验证攻击与伤害。`StaticMovementObstacle` 是静态 BoxCollider2D 场景样例；漫游路线随机，不能保证每次运行都撞到它。`GoalPoint` 暂不触发抵达判定。史莱姆/敌人的速度配置 200/300 单位为像素/秒，运行时按各自 Sprite 的 `pixelsPerUnit` 换算为 Unity 世界单位/秒。

直接从 Level_test 场景启动不会创建正式 GameRoot，应从 Bootstrap 开始。

`Level_test` 的 `ObjectiveConfig` 关闭了“史莱姆全部死亡也失败”，因此这局只在护送对象生命归零时判负；其他关卡保持现有默认规则。StatusManager 已接入 Actor 效果结算，但本场景的漫游感知没有敌对目标，护送对象也不是 Actor；Play 模式仍没有攻击护送对象的路径。EditMode 测试通过 `ObjectiveSystem` 的公开效果接口验证失败判定。

## 地图与状态验收

`LevelRoot/MapAreas` 下有三条纵向区域，颜色仅用于测试场景辨识：

- 蓝色 `Region_Cold`：`LowTemperature`，绑定 `status_cold`；史莱姆在区域内获得 `Cold`，状态移速倍率为 0.8。
- 绿色 `Terrain_Swamp`：`Swamp`，地形移速倍率为 0.5；与状态倍率相乘。
- 橙色 `Region_Warm`：`Warm`，绑定 `status_hot`；获得 `Hot` 后最大生命倍率为 0.7，并与 `Cold` 互相抵消。

唯一史莱姆从蓝色冷区出发并随机漫游。随机路线会受静态障碍影响，不保证每次运行都立即经过所有区域；状态离开区域后按配置保留 20 秒。Play Mode 固定位置采样验证结果：冷区速度为 2.50 世界单位/秒；带 `Cold` 穿过沼泽时为 1.25，正好是中性地面速度的一半。进入暖区后 `Hot` 将最大生命从 500 调到 350，重新进入冷区后 `Cold` 替换 `Hot`，最大生命恢复到 500。

史莱姆新获得状态时，Console 应输出 `[Level_test] Slime ... gained status ...`，包含状态 ID/类型、移动倍率和最大生命倍率。该日志只由 `Level_test` 的 `LevelActorSpawner` 监听本关 Slime 状态事件输出。
