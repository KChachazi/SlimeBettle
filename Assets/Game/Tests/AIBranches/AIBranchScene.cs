/* 独立场景入口：经 GameRoot/LevelManager 加载自身，再用正式 Manager 生成预制体 Actor。 */
using System.Collections;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Slimes;
using SlimeBattle.Status;
using UnityEngine;

namespace SlimeBattle.Tests.AIBranches
{
    public sealed class AIBranchScene : MonoBehaviour
    {
        [SerializeField] private AIBranchScenario scenario;
        [SerializeField] private SlimeConfig slimeConfig;
        [SerializeField] private EnemyConfig enemyConfig;
        [SerializeField] private Transform slimeRoot;
        [SerializeField] private Transform enemyRoot;

        private LevelContext context;
        private SlimeActor observedSlime;
        private EnemyActor observedEnemy;
        private string lastReason = string.Empty;
        private string verdict = "等待关卡初始化";
        private bool firstDecisionChecked;
        private int effectRequests;
        private int healthChanges;
        private SubscriptionToken appliedSubscription;
        private SubscriptionToken rejectedSubscription;
        private SubscriptionToken healthSubscription;
        private Texture2D markerTexture;
        private Sprite markerSprite;

        private IEnumerator Start()
        {
            // 首次直接 Play 会重载一次自身；重载后 LevelManager 才拥有正式 LevelContext。
            while (true)
            {
                var root = FindObjectOfType<GameRoot>();
                var manager = root == null ? null : root.GetLevelManager();
                if (manager != null)
                {
                    var active = manager.GetCurrentContext();
                    if (active != null && manager.GetLevelState() == LevelState.Running &&
                        active.LevelId.Value == gameObject.scene.name)
                    {
                        Setup(active);
                        yield break;
                    }
                    if (manager.GetLevelState() == LevelState.Unloaded)
                        root.RequestEnterLevel(new LevelId(gameObject.scene.name));
                    if (manager.GetLevelState() == LevelState.Failed)
                    {
                        Debug.LogError("[AIBranch] 关卡加载失败；检查 Build Settings 与场景配置。", this);
                        yield break;
                    }
                }
                yield return null;
            }
        }

        private void Setup(LevelContext active)
        {
            if (context != null) return;
            context = active;
            if (slimeConfig == null || enemyConfig == null || slimeRoot == null || enemyRoot == null ||
                !context.SlimeManager.TrySetSpawnParent(slimeRoot) ||
                !context.EnemyManager.TrySetSpawnParent(enemyRoot))
            {
                Debug.LogError("[AIBranch] 场景装配缺少配置或 ActorRoot。", this);
                return;
            }

            var enemyScenario = IsEnemyScenario;
            var spawnSlime = !enemyScenario || scenario != AIBranchScenario.EnemyWander;
            var spawnEnemy = enemyScenario || (scenario != AIBranchScenario.SlimeWait &&
                scenario != AIBranchScenario.SlimeAgitatedHuman);
            var slimePosition = enemyScenario
                ? new Vector2(scenario == AIBranchScenario.EnemyAttack ? .65f : 1.3f, 0f)
                : Vector2.zero;
            var enemyPosition = enemyScenario ? Vector2.zero
                : new Vector2(scenario == AIBranchScenario.SlimeAttack ? .65f : 1.3f, 0f);
            var bounds = scenario == AIBranchScenario.EnemyOutsideActivityBounds
                ? new EnemyActivityBounds(new Vector2(-.5f, -1f), new Vector2(.5f, 1f))
                : new EnemyActivityBounds(new Vector2(-4f, -3f), new Vector2(4f, 3f));

            ActorId slimeId = default(ActorId);
            ActorId enemyId = default(ActorId);
            if (spawnSlime)
                slimeId = context.SlimeManager.SpawnSlime(
                    new SlimeSpawnRequest(slimeConfig.ConfigId, slimePosition));
            if (spawnEnemy)
                enemyId = context.EnemyManager.SpawnEnemy(
                    new EnemySpawnRequest(enemyConfig.ConfigId, enemyPosition,
                        activityBounds: bounds));
            if ((spawnSlime && !slimeId.IsValid) || (spawnEnemy && !enemyId.IsValid))
            {
                Debug.LogError("[AIBranch] 生成失败：" + context.SlimeManager.LastFailureReason +
                    " " + context.EnemyManager.LastFailureReason, this);
                return;
            }
            observedSlime = FindSlime(slimeId);
            observedEnemy = FindEnemy(enemyId);

            // 停住对方，但不替换其预制体、ActorCore 或 Manager；观察方仍完整 Tick。
            if (enemyScenario) observedSlime?.SetControlMode(ActorControlMode.Paused);
            else observedEnemy?.SetControlMode(ActorControlMode.Paused);
            // 本场景的靶子保留实体碰撞，但不会被观察方的刚体推走；正式预制体不变。
            var targetBody = enemyScenario
                ? observedSlime?.GetComponent<Rigidbody2D>()
                : observedEnemy?.GetComponent<Rigidbody2D>();
            if (targetBody != null)
            {
                targetBody.velocity = Vector2.zero;
                targetBody.constraints |= RigidbodyConstraints2D.FreezePosition;
            }
            if (!SetScenarioPreconditions()) return;
            var formalTargets = new ActorTargetPerceptionSource(
                context.SlimeManager, context.EnemyManager, context.ConfigRegistry);
            var perception = new AIBranchPerception(formalTargets, scenario, slimeId);
            appliedSubscription = context.EventBus.Subscribe<EffectAppliedEvent>(OnEffectApplied);
            rejectedSubscription = context.EventBus.Subscribe<EffectRejectedEvent>(OnEffectRejected);
            healthSubscription = context.EventBus.Subscribe<HealthChangedEvent>(OnHealthChanged);
            // 正式行动端口已经把攻击送到 StatusManager；场景只观察结算事件。
            context.BindActorPorts(perception, null);
            CreateMarkers(perception.HumanPosition);
            verdict = "运行中，等待首次决策";
            Debug.Log("[AIBranch] " + scenario + "：正式 Manager/Actor/AI/Movement/Status 已接入", this);
        }

        private bool SetScenarioPreconditions()
        {
            if (observedSlime == null) return true;
            // 测试只设定起点；状态添加与扣血仍走本关 StatusManager 的公开结算入口。
            string status = null;
            if (scenario == AIBranchScenario.SlimeHungry) status = SlimeStatusIds.Hungry;
            if (scenario == AIBranchScenario.SlimeCold) status = SlimeStatusIds.Cold;
            if (scenario == AIBranchScenario.SlimeAfraid) status = SlimeStatusIds.Afraid;
            if (scenario == AIBranchScenario.SlimeAgitatedHuman) status = SlimeStatusIds.Agitated;
            if (status != null)
            {
                if (!context.StatusManager.ApplyStatus(new StatusApplicationRequest(
                    default(ActorId), observedSlime.ActorId, new StatusId(status))))
                {
                    Debug.LogError("[AIBranch] 前置状态添加失败：" + status +
                        "；检查场景 LevelManager 是否注册对应 StatusConfig。", this);
                    return false;
                }
            }
            var damage = 0f;
            if (scenario == AIBranchScenario.SlimeFleeBelow50 ||
                scenario == AIBranchScenario.SlimeFightAfterFailedFlee)
                damage = slimeConfig.MaxHealth * .6f;
            if (scenario == AIBranchScenario.SlimeFleeBelow30)
                damage = slimeConfig.MaxHealth * .75f;
            if (damage <= 0f) return true;
            var result = context.StatusManager.ApplyEffect(new EffectRequest(
                EffectRequestId.NewId(), default(ActorId), observedSlime.ActorId,
                EffectType.Damage, damage));
            if (result.IsAccepted) return true;
            Debug.LogError("[AIBranch] 前置生命设置失败：" + result.Reason, this);
            return false;
        }

        private void Update()
        {
            if (context == null || context.GetLevelState() != LevelState.Running) return;
            var reason = IsEnemyScenario ? observedEnemy?.LastDecisionReason : observedSlime?.LastDecisionReason;
            if (string.IsNullOrEmpty(reason) || reason == lastReason) return;
            lastReason = reason;
            Debug.Log("[AIBranch] " + scenario + " 决策树 → " + reason, this);
            if (firstDecisionChecked) return;
            firstDecisionChecked = true;
            verdict = reason.Contains(ExpectedReason) ? "首个分支：通过" :
                "首个分支：不符，预期包含「" + ExpectedReason + "」";
            if (verdict == "首个分支：通过" && IsDamageScenario && healthChanges > 0)
                verdict = "选路与正式扣血：通过";
            Debug.Log("[AIBranch] " + verdict, this);
        }

        private void OnEffectApplied(EffectAppliedEvent applied)
        {
            if (applied.Request.Type != EffectType.Damage || !IsObservedAttacker(applied.Request.SourceActorId))
                return;
            effectRequests++;
            Debug.Log("[AIBranch] StatusManager 已结算攻击 #" + effectRequests +
                " 目标=" + applied.Request.TargetActorId +
                " 实际伤害=" + applied.Result.AppliedAmount +
                " 剩余生命=" + applied.Result.RemainingHealth, this);
        }

        private void OnEffectRejected(EffectRejectedEvent rejected)
        {
            if (!IsObservedAttacker(rejected.SourceActorId)) return;
            effectRequests++;
            Debug.Log("[AIBranch] StatusManager 拒绝攻击 #" + effectRequests +
                " 目标=" + rejected.TargetActorId + " 原因=" + rejected.Result.Reason, this);
            if (IsDamageScenario && firstDecisionChecked)
                verdict = "选路通过，正式结算被拒绝：" + rejected.Result.Reason;
        }

        private void OnHealthChanged(HealthChangedEvent changed)
        {
            if (!IsObservedAttacker(changed.Effect.SourceActorId)) return;
            healthChanges++;
            Debug.Log("[AIBranch] 目标生命 " + changed.Before.CurrentHealth +
                " → " + changed.After.CurrentHealth, this);
            if (IsDamageScenario && firstDecisionChecked && verdict == "首个分支：通过")
                verdict = "选路与正式扣血：通过";
        }

        private bool IsDamageScenario => scenario == AIBranchScenario.SlimeAttack ||
            scenario == AIBranchScenario.EnemyAttack;
        private bool IsObservedAttacker(ActorId actorId) => IsEnemyScenario
            ? observedEnemy != null && actorId == observedEnemy.ActorId
            : observedSlime != null && actorId == observedSlime.ActorId;

        private bool IsEnemyScenario => scenario >= AIBranchScenario.EnemyWander;
        private string ExpectedReason
        {
            get
            {
                switch (scenario)
                {
                    case AIBranchScenario.SlimeWait: return "等待";
                    case AIBranchScenario.SlimeChase: return "接近战斗";
                    case AIBranchScenario.SlimeAttack: return "攻击";
                    case AIBranchScenario.SlimeHungry: return "优先靠近食物";
                    case AIBranchScenario.SlimeCold: return "优先靠近热源";
                    case AIBranchScenario.SlimeAfraid: return "离开危险";
                    case AIBranchScenario.SlimeFleeBelow50: return "低于50%";
                    case AIBranchScenario.SlimeFleeBelow30: return "低于30%";
                    case AIBranchScenario.SlimeFightAfterFailedFlee: return "接近战斗";
                    case AIBranchScenario.SlimeAgitatedHuman: return "攻击附近人类";
                    case AIBranchScenario.EnemyWander: return "随机游走";
                    case AIBranchScenario.EnemyChase: return "追击最近目标";
                    case AIBranchScenario.EnemyAttack: return "攻击最近目标";
                    default: return "留在活动范围内";
                }
            }
        }

        private static SlimeActor FindSlime(ActorId id)
        {
            foreach (var actor in FindObjectsOfType<SlimeActor>())
                if (actor.ActorId == id) return actor;
            return null;
        }

        private static EnemyActor FindEnemy(ActorId id)
        {
            foreach (var actor in FindObjectsOfType<EnemyActor>())
                if (actor.ActorId == id) return actor;
            return null;
        }

        private void CreateMarkers(Vector2 humanPosition)
        {
            markerTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color[32 * 32];
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                    pixels[y * 32 + x] = (x - 15.5f) * (x - 15.5f) +
                        (y - 15.5f) * (y - 15.5f) < 220f ? Color.white : Color.clear;
            markerTexture.SetPixels(pixels);
            markerTexture.Apply();
            markerSprite = Sprite.Create(markerTexture, new Rect(0, 0, 32, 32),
                new Vector2(.5f, .5f), 32f);
            if (scenario == AIBranchScenario.SlimeHungry || scenario == AIBranchScenario.SlimeAfraid)
                Marker("Food", new Vector2(scenario == AIBranchScenario.SlimeAfraid ? 3f : -3f, 1f), Color.yellow);
            if (scenario == AIBranchScenario.SlimeCold)
                Marker("Warmth", new Vector2(-3f, -1f), new Color(1f, .45f, .1f));
            if (scenario == AIBranchScenario.SlimeAfraid ||
                scenario == AIBranchScenario.SlimeFleeBelow50 ||
                scenario == AIBranchScenario.SlimeFleeBelow30 ||
                scenario == AIBranchScenario.SlimeFightAfterFailedFlee)
                Marker("Escape", new Vector2(-3f, 0f), Color.cyan);
            if (scenario == AIBranchScenario.SlimeAgitatedHuman)
                Marker("Human test target", humanPosition, Color.yellow);
            if (scenario == AIBranchScenario.EnemyWander)
                Marker("Wander", new Vector2(2f, 1f), Color.cyan);
        }

        private void Marker(string label, Vector2 position, Color color)
        {
            var marker = new GameObject(label);
            marker.transform.SetParent(transform);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * .35f;
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.color = color;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(10, 10, 540, 115), "AI 分支独立场景：" + scenario);
            GUI.Label(new Rect(22, 35, 510, 25), "预期：" + ExpectedReason);
            GUI.Label(new Rect(22, 62, 510, 25), "当前：" + lastReason);
            GUI.Label(new Rect(22, 89, 510, 25), verdict + "；效果请求：" + effectRequests +
                "；生命变化：" + healthChanges);
        }

        private void OnDestroy()
        {
            if (context != null)
            {
                context.EventBus.Unsubscribe(appliedSubscription);
                context.EventBus.Unsubscribe(rejectedSubscription);
                context.EventBus.Unsubscribe(healthSubscription);
            }
            if (markerSprite != null) Destroy(markerSprite);
            if (markerTexture != null) Destroy(markerTexture);
        }
    }
}
