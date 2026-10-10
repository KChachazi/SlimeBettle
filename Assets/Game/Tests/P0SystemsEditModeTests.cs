/* 配置、关卡上下文与目标的纯逻辑验收；不依赖正式场景或其他成员系统。 */
using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Objectives;
using SlimeBattle.Slimes;
using SlimeBattle.Status;
using SlimeBattle.World.Movement;
using UnityEditor;
using UnityEngine;

namespace SlimeBattle.Tests
{
    public sealed class P0SystemsEditModeTests
    {
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void RegistryRejectsDuplicateInvalidAndMissingReferences()
        {
            var first = New<SlimeConfig>("same");
            var duplicate = New<EnemyConfig>("same");
            var invalid = New<WeaponConfig>("");
            invalid.SetMaxHealth(0);
            var level = New<LevelConfig>("level");
            level.SetObjectiveConfigId(new ConfigId("missing"));
            var registry = new ConfigRegistry(new ConfigAsset[] { first, duplicate, invalid, level, null });

            Assert.IsFalse(registry.ValidateAll().IsValid);
            Assert.IsFalse(registry.TryGet(new ConfigId("same"), out SlimeConfig ignored));
            Assert.IsFalse(registry.TryGet(new ConfigId("missing"), out ObjectiveConfig missing));
        }

        [Test]
        public void LevelSlimeLimitDefaultsToThirtyAndRejectsAboveFifty()
        {
            var level = New<LevelConfig>("level");
            var objective = New<ObjectiveConfig>("objective");
            var weapon = New<WeaponConfig>("weapon");
            level.SetObjectiveConfigId(objective.ConfigId);
            objective.ConfigureWeapon(weapon.ConfigId);
            var registry = new ConfigRegistry(new ConfigAsset[] { level, objective, weapon });
            Assert.AreEqual(30, level.MaxAliveSlimes);
            Assert.IsTrue(registry.ValidateAll().IsValid);
            level.SetMaxAliveSlimes(50);
            Assert.IsTrue(registry.ValidateAll().IsValid);
            level.SetMaxAliveSlimes(51);
            Assert.IsFalse(registry.ValidateAll().IsValid);
        }

        [Test]
        public void ActorConfigRejectsNonFiniteAttackRange()
        {
            var config = New<SlimeConfig>("slime");
            config.SetAttackRange(float.NaN);
            Assert.IsFalse(new ConfigRegistry(new ConfigAsset[] { config }).ValidateAll().IsValid);
        }

        [Test]
        public void StatusPreconditionsAndAttackUseFormalActorEffectPath()
        {
            var level = New<LevelConfig>("level");
            var slime = New<SlimeConfig>("slime");
            var enemy = New<EnemyConfig>("enemy");
            var objective = New<ObjectiveConfig>("objective");
            var weapon = New<WeaponConfig>("weapon");
            var hungry = New<StatusConfig>("hungry");
            var authoredStatus = new SerializedObject(hungry);
            authoredStatus.FindProperty("statusId").stringValue = SlimeStatusIds.Hungry;
            authoredStatus.ApplyModifiedPropertiesWithoutUndo();
            level.SetObjectiveConfigId(objective.ConfigId);
            objective.ConfigureWeapon(weapon.ConfigId);
            slime.ConfigureStats(5, 20, 100f, 1f);
            slime.SetAttackRange(1.2f);
            enemy.ConfigureStats(1, 20, 100f, 1f);
            var registry = new ConfigRegistry(new ConfigAsset[]
                { level, slime, enemy, objective, weapon, hungry });
            Assert.IsTrue(registry.ValidateAll().IsValid);

            var context = new LevelContext(new LevelId("level"), registry, level,
                NewSlimePrefab(), NewEnemyPrefab());
            try
            {
                context.Initialize();
                var slimeId = context.SlimeManager.SpawnSlime(
                    new SlimeSpawnRequest(slime.ConfigId, Vector2.zero));
                var enemyId = context.EnemyManager.SpawnEnemy(
                    new EnemySpawnRequest(enemy.ConfigId, Vector2.right));
                Assert.IsTrue(slimeId.IsValid && enemyId.IsValid);
                Assert.IsTrue(context.StatusManager.ApplyStatus(new StatusApplicationRequest(
                    default(ActorId), slimeId, new StatusId(SlimeStatusIds.Hungry))));
                Assert.IsTrue(context.StatusManager.TryGetStatusSnapshot(slimeId, out var status));
                Assert.AreEqual(new StatusId(SlimeStatusIds.Hungry), status.Statuses[0].StatusId);

                var healthChanges = 0;
                context.EventBus.Subscribe<HealthChangedEvent>(_ => healthChanges++);
                var targets = new ActorTargetPerceptionSource(
                    context.SlimeManager, context.EnemyManager, registry);
                Assert.IsTrue(targets.TryGetActorSnapshot(slimeId, out var attacker));
                var actions = new MovementActorActionPort(context.MovementSystem, null,
                    context.StatusManager);
                Assert.IsTrue(new AttackAction().ExecuteHit(
                    new AttackIntent(slimeId, enemyId, slime.ConfigId), attacker,
                    slime.AttackRange, slime.AttackPower, targets, actions));
                Assert.IsTrue(context.StatusManager.TryGetHealthSnapshot(enemyId, out var health));
                Assert.AreEqual(15f, health.CurrentHealth);
                Assert.AreEqual(1, healthChanges);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void DisposedLevelRejectsOldActorsAndSubscriptions()
        {
            var level = New<LevelConfig>("level");
            level.SetMaxAliveSlimes(2);
            var slime = New<SlimeConfig>("slime");
            var objective = New<ObjectiveConfig>("objective");
            var weapon = New<WeaponConfig>("weapon");
            objective.ConfigureWeapon(weapon.ConfigId);
            level.SetObjectiveConfigId(objective.ConfigId);
            var registry = new ConfigRegistry(new ConfigAsset[] { level, slime, objective, weapon });
            var slimePrefab = NewSlimePrefab();
            var enemyPrefab = NewEnemyPrefab();
            var oldLevel = new LevelContext(new LevelId("level"), registry, level,
                slimePrefab, enemyPrefab);
            oldLevel.Initialize();
            var oldId = oldLevel.SlimeManager.SpawnSlime(new SlimeSpawnRequest(slime.ConfigId, Vector2.zero));
            Assert.IsTrue(oldLevel.TryResolveActor(oldId, out var oldActor));
            var callbackCount = 0;
            oldLevel.EventBus.Subscribe<SlimeSpawnedEvent>(_ => callbackCount++);
            oldLevel.Dispose();

            var newLevel = new LevelContext(new LevelId("level"), registry, level,
                slimePrefab, enemyPrefab);
            newLevel.Initialize();
            var newId = newLevel.SlimeManager.SpawnSlime(new SlimeSpawnRequest(slime.ConfigId, Vector2.zero));
            Assert.AreNotEqual(oldId, newId);
            Assert.IsFalse(newLevel.TryResolveActor(oldId, out var ignored));
            Assert.IsFalse(oldActor.ApplyResolvedEffect(ResolvedEffect.Damage(1f)).IsAccepted);
            Assert.AreEqual(0, callbackCount);
            Assert.IsFalse(oldLevel.SlimeManager.TryGetSnapshot(oldId, out var oldSnapshot));
            newLevel.Dispose();
        }

        [Test]
        public void ObjectiveConfirmsWeaponFailureOnlyOnce()
        {
            var system = new ObjectiveSystem(new ObjectiveId("objective"), new WeaponId("weapon"), 10);
            var count = 0;
            system.Failed += _ => count++;
            system.NotifySlimeCountChanged(1);
            Assert.IsTrue(system.ApplyWeaponEffect(new WeaponId("weapon"),
                ResolvedEffect.Damage(10f)).DidKill);
            Assert.IsTrue(system.EvaluateResult().Failed);
            Assert.IsTrue(system.EvaluateResult().Failed);
            Assert.AreEqual(1, count);
            system.Dispose();
        }

        [Test]
        public void ObjectiveConfirmsLastSlimeFailureOnlyOnce()
        {
            var system = new ObjectiveSystem(new ObjectiveId("objective"), new WeaponId("weapon"), 10);
            var count = 0;
            system.Failed += _ => count++;
            system.NotifySlimeCountChanged(2);
            system.NotifySlimeCountChanged(0);
            system.NotifySlimeCountChanged(0);
            Assert.AreEqual(ObjectiveResultReason.AllSlimesDead, system.EvaluateResult().Reason);
            Assert.AreEqual(1, count);
            system.Dispose();
        }

        [Test]
        public void LevelObjectiveCanFailOnlyWhenEscortWeaponIsDestroyed()
        {
            var objective = New<ObjectiveConfig>("objective");
            var weapon = New<WeaponConfig>("weapon");
            objective.ConfigureWeapon(weapon.ConfigId);
            objective.SetFailWhenNoSlimes(false);
            weapon.SetMaxHealth(100);
            var system = new ObjectiveSystem();
            Assert.IsTrue(system.Initialize(objective, weapon, new WeaponId("escort")));
            var failureCount = 0;
            system.Failed += _ => failureCount++;

            system.NotifySlimeCountChanged(0);
            Assert.IsFalse(system.EvaluateResult().IsFinal);
            Assert.AreEqual(0, failureCount);
            Assert.IsTrue(system.ApplyWeaponEffect(new WeaponId("escort"),
                ResolvedEffect.Damage(100f)).DidKill);
            Assert.AreEqual(ObjectiveResultReason.WeaponDestroyed,
                system.EvaluateResult().Reason);
            Assert.AreEqual(1, failureCount);
            system.Dispose();
        }

        [Test]
        public void WeaponDestructionRemainsOnceUnderReentrantNotification()
        {
            var bus = new LevelEventBus();
            var system = new ObjectiveSystem(new ConfigRegistry(), bus);
            Assert.IsTrue(system.Initialize(new ObjectiveId("objective"),
                new WeaponId("weapon"), 10));
            system.NotifySlimeCountChanged(1);
            var destroyed = 0;
            var failed = 0;
            bus.Subscribe<WeaponDamagedEvent>(_ => system.ApplyWeaponEffect(
                new WeaponId("weapon"), ResolvedEffect.Damage(1f)));
            bus.Subscribe<WeaponDestroyedEvent>(_ => destroyed++);
            system.Failed += _ => failed++;
            system.ApplyWeaponEffect(new WeaponId("weapon"), ResolvedEffect.Damage(10f));
            Assert.AreEqual(1, destroyed);
            Assert.AreEqual(1, failed);
            system.Dispose();
            bus.Dispose();
        }

        private T New<T>(string id) where T : ConfigAsset
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.SetConfigId(id);
            assets.Add(asset);
            return asset;
        }

        private SlimeActor NewSlimePrefab()
        {
            var actorObject = new GameObject("Test Slime Prefab");
            assets.Add(actorObject);
            actorObject.AddComponent<CircleCollider2D>();
            var actor = actorObject.AddComponent<SlimeActor>();
            actorObject.GetComponent<SpriteRenderer>().sprite = NewTestSprite();
            return actor;
        }

        private EnemyActor NewEnemyPrefab()
        {
            var actorObject = new GameObject("Test Enemy Prefab");
            assets.Add(actorObject);
            actorObject.AddComponent<CircleCollider2D>();
            var actor = actorObject.AddComponent<EnemyActor>();
            actorObject.GetComponent<SpriteRenderer>().sprite = NewTestSprite();
            return actor;
        }

        private Sprite NewTestSprite()
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            assets.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            assets.Add(sprite);
            return sprite;
        }
    }
}
