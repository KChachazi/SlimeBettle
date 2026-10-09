# Level_test MVP 手动验收

1. 在 Unity 中从 `Assets/Game/Scenes/Bootstrap.unity` 开始 Play（不要直接从 `Level_test` 开始；正式 `GameRoot` 在 Bootstrap）。
2. Bootstrap 应自动进入 MainMenu。点击 Start，应进入 `Level_test`。
3. Hierarchy 中的 `EntitiesRoot/SlimeRoot` 下应有四个 `PF_Slime_Green(Clone)`，`EntitiesRoot/EnemyRoot` 下应有四个 `PF_Enemy_Basic(Clone)`；它们由 `LevelContext` 中的 `SlimeManager` / `EnemyManager` 从正式预制体生成，不是预先摆放的场景角色。Console 应输出 `Level_test initial actors: 4 Slimes, 4 Enemies.`
4. 场景中央的黄色 `PF_EscortWeapon` 是护送对象的静态可视预制体；生命值由 `ObjectiveSystem` 的 `WeaponConfig` 初始化为 100，不保存在视觉预制体中。

本次暂不绑定 MovementSystem、StatusManager 或攻击效果端口，因此角色出生后不会移动/攻击；这里验证的是菜单入口、正式配置读取、Manager 生成及目标初始化，不验证 AI 分支。`GoalPoint` 暂不触发抵达判定。史莱姆/敌人的速度配置沿用策划暂定值 200/300，单位到世界速度的换算须待 MovementSystem 接入时确认。

`Level_test` 的 `ObjectiveConfig` 关闭了“史莱姆全部死亡也失败”，因此这局只在护送对象生命归零时判负；其他关卡保持现有默认规则。由于 StatusManager 尚未接入，Play 模式中目前没有正式伤害路径可以打掉护送对象生命；EditMode 测试通过 `ObjectiveSystem` 的公开效果接口验证该判定。
