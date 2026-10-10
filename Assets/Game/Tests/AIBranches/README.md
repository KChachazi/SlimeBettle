# AI 独立分支场景

在 Unity 中打开 `Scenes/Test_AI_*.unity` 的任意一个场景，直接按 Play。每个场景固定一个条件；首次 Play 会通过场景中的正式 `GameRoot → LevelManager` 重载自身，随后由正式 `LevelContext` 创建 `SlimeManager` / `EnemyManager`，从正式预制体生成 Actor 并驱动决策树、状态机、`MovementSystem` 和 `StatusManager`。左上角显示预期关键词、当前理由、首次分支结果、效果请求数和生命变化数；Console 搜索 `[AIBranch]` 可看具体结算。

`SlimeWait/Chase/Attack/Hungry/Cold/Afraid/FleeBelow50/FleeBelow30/FightAfterFailedFlee/AgitatedHuman` 和 `EnemyWander/Chase/Attack/OutsideActivityBounds` 各自对应独立场景。史莱姆是绿色正式预制体，敌人是红色正式预制体；黄/橙/蓝小圆分别标识测试提供的食物、热源或逃跑点。测试人类为黄色小圆，不是 GuardActor。

敌我目标由正式 `ActorTargetPerceptionSource` 从 Manager 快照发现，使用 `CFG_Slime_MVP` / `CFG_Enemy_MVP` 中暂定的 1.5 世界单位感知半径（攻击半径 1.2），暂不判断遮挡。测试脚本提供食物、热源、逃跑判定和暂缺的 Guard 人类目标；被观察 Actor 的对手仍由正式 Manager 创建，但暂停行动并固定刚体位置以保持条件稳定。移动由正式 MovementSystem 执行。四个中性测试 `StatusConfig` 仅定义 AI 前置状态 ID，不添加计时或数值修正；状态和低血量前置条件通过 `StatusManager` 正式入口设置，不再直接修改 Actor。史莱姆与敌人的攻击经正式 `AttackAction → StatusManager` 扣血并发出生命变化事件。测试人类不是已注册 Actor，因此其攻击意图会到达正式效果入口，但目标不可用，不能验证扣血。概率、状态自然生成和进食/取暖结果仍未验收。

当前食物、热源、逃跑点和测试人类的位置是测试感知中的固定坐标；运行时创建的彩色圆点只是视觉标记。拖动圆点不会改变感知快照或移动目的地。“首个分支：通过”仅检查决策理由关键词，**不证明动态目标追踪、抵达或机制触发**。这些要等待正式机关/感知接入后另行验收。

如果要重新生成缺失场景，编辑器菜单 `SlimeBattle/Tests/Generate AI Branch Scenes` 只创建不存在的场景和配置，不覆盖已有场景。生成器会把这些场景加入 Build Settings，供正式 `LevelManager.RequestLoadLevel` 使用。当前这些生成结果已包含在仓库，无需先运行菜单。

验收建议：先 Play `Test_AI_SlimeChase`，确认“接近战斗”且绿色 Actor 确实移动；再分别 Play `Test_AI_SlimeAttack` 与 `Test_AI_EnemyAttack`，确认显示“选路与正式扣血：通过”、生命变化数增加，且 Console 出现实际伤害与剩余生命；最后逐个打开状态和逃跑分支。`Test_AI_SlimeAgitatedHuman` 应显示目标不可用，而非虚构扣血。Console 中的“首个分支：通过”只验证选路关键词，玩家仍需观察移动方向/范围与场景表现。
