/* Manager 生命周期测试：只通过公开生成/查询/销毁接口，效果通过关卡内部解析替身施加。 */
using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Guards;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Guards;
using SlimeBattle.Slimes;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Tests
{
    public sealed class ActorManagerEditModeTests
    {
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void DetectionRangeMustExceedAttackRange()
        {
            var config = New<SlimeConfig>("slime");
            config.SetAttackRange(1f);
            config.SetDetectionRange(1f);
            var report = new ConfigRegistry(new ConfigAsset[] { config }).ValidateAll();
            Assert.IsFalse(report.IsValid);
            config.SetDetectionRange(1.5f);
            Assert.IsTrue(new ConfigRegistry(new ConfigAsset[] { config }).ValidateAll().IsValid);
        }

        [Test]
        public void TargetPerceptionUsesManagerSnapshotsFactionAndConfiguredRadius()
        {
            var slimeConfig = New<SlimeConfig>("slime");
            var enemyConfig = New<EnemyConfig>("enemy");
            slimeConfig.SetDetectionRange(1.5f);
            enemyConfig.SetDetectionRange(1.5f);
            var registry = new ConfigRegistry(new ConfigAsset[] { slimeConfig, enemyConfig });
            var slimes = new SlimeManager(registry, null, 3, NewSlimePrefab());
            var enemies = new EnemyManager(registry, null, NewEnemyPrefab());
            try
            {
                var self = slimes.SpawnSlime(new SlimeSpawnRequest(slimeConfig.ConfigId, Vector2.zero));
                var friend = slimes.SpawnSlime(new SlimeSpawnRequest(slimeConfig.ConfigId, Vector2.right));
                var near = enemies.SpawnEnemy(new EnemySpawnRequest(enemyConfig.ConfigId,
                    new Vector2(1.2f, 0f)));
                var far = enemies.SpawnEnemy(new EnemySpawnRequest(enemyConfig.ConfigId,
                    new Vector2(2f, 0f)));
                var source = new ActorTargetPerceptionSource(slimes, enemies, registry);
                var seen = source.GetPerception(self);
                Assert.AreEqual(1, seen.Targets.Count);
                Assert.AreEqual(near, seen.Targets[0].Actor.ActorId);
                Assert.IsTrue(seen.Targets[0].IsHostile);
                Assert.AreNotEqual(friend, seen.Targets[0].Actor.ActorId);
                Assert.AreNotEqual(far, seen.Targets[0].Actor.ActorId);
                Assert.IsTrue(source.TryGetActorSnapshot(near, out var snapshot));
                Assert.IsTrue(snapshot.IsAlive);
                Assert.IsTrue(enemies.RequestDespawn(near, DespawnReason.Removed));
                Assert.AreEqual(0, source.GetPerception(self).Targets.Count);
                slimeConfig.SetDetectionRange(2.5f);
                Assert.AreEqual(far, source.GetPerception(self).Targets[0].Actor.ActorId);
            }
            finally { slimes.Dispose(); enemies.Dispose(); }
        }

        [Test]
        public void SpawnQueryAndDespawnUseFreshIdsAndRespectLimit()
        {
            var slime = New<SlimeConfig>("slime");
            var enemy = New<EnemyConfig>("enemy");
            var registry = new ConfigRegistry(new ConfigAsset[] { slime, enemy });
            var slimePrefab = NewSlimePrefab();
            var enemyPrefab = NewEnemyPrefab();
            var slimes = new SlimeManager(registry, null, 2, slimePrefab);
            var enemies = new EnemyManager(registry, null, enemyPrefab);
            var first = slimes.SpawnSlime(new SlimeSpawnRequest(slime.ConfigId, Vector2.zero));
            var second = slimes.SpawnSlime(new SlimeSpawnRequest(slime.ConfigId, Vector2.right));
            var rejected = slimes.SpawnSlime(new SlimeSpawnRequest(slime.ConfigId, Vector2.up));
            var hostile = enemies.SpawnEnemy(new EnemySpawnRequest(enemy.ConfigId, Vector2.zero));

            Assert.IsTrue(first.IsValid && second.IsValid && hostile.IsValid);
            Assert.AreNotEqual(first, second);
            Assert.AreNotEqual(first, hostile);
            Assert.IsTrue(slimes.TryGetActor(first, out var firstComponent));
            Assert.IsNull(firstComponent.GetComponent<SpriteRenderer>());
            Assert.IsNotNull(firstComponent.GetComponentInChildren<SpriteRenderer>());
            Assert.AreNotSame(slimePrefab, firstComponent);
            Assert.IsTrue(enemies.TryGetActor(hostile, out var enemyComponent));
            Assert.IsNotNull(enemyComponent.GetComponent<SpriteRenderer>());
            Assert.AreNotSame(enemyPrefab, enemyComponent);
            Assert.IsFalse(rejected.IsValid);
            StringAssert.Contains("limit", slimes.LastFailureReason);
            Assert.AreEqual(2, slimes.GetAliveSnapshots().Count);
            Assert.IsTrue(slimes.TryGetSnapshot(first, out var slimeSnapshot));
            Assert.AreEqual(64f, slimeSnapshot.PixelsPerUnit);
            Assert.AreEqual(100f, firstComponent.GetComponentInChildren<SpriteRenderer>().sprite.pixelsPerUnit);
            Assert.IsTrue(enemies.TryGetSnapshot(hostile, out var enemySnapshot));
            Assert.IsTrue(enemySnapshot.IsAlive);
            Assert.IsTrue(slimes.RequestDespawn(first, DespawnReason.Removed));
            Assert.IsFalse(slimes.TryGetSnapshot(first, out var missing));
            Assert.IsTrue(enemies.RequestDespawn(hostile, DespawnReason.Removed));
            Assert.IsFalse(enemies.TryGetSnapshot(hostile, out var missingEnemy));
            var replacement = slimes.SpawnSlime(new SlimeSpawnRequest(slime.ConfigId, Vector2.zero));
            Assert.AreNotEqual(first, replacement);
            slimes.Dispose();
            enemies.Dispose();
        }

        [Test]
        public void SpawnedActorsUseTheirSceneRoots()
        {
            var slimeConfig = New<SlimeConfig>("slime");
            var enemyConfig = New<EnemyConfig>("enemy");
            var registry = new ConfigRegistry(new ConfigAsset[] { slimeConfig, enemyConfig });
            var slimeRoot = new GameObject("SlimeRoot");
            var enemyRoot = new GameObject("EnemyRoot");
            assets.Add(slimeRoot);
            assets.Add(enemyRoot);
            var slimes = new SlimeManager(registry, null, 30, NewSlimePrefab());
            var enemies = new EnemyManager(registry, null, NewEnemyPrefab());

            Assert.IsTrue(slimes.TrySetSpawnParent(slimeRoot.transform));
            Assert.IsTrue(enemies.TrySetSpawnParent(enemyRoot.transform));
            var slimeId = slimes.SpawnSlime(new SlimeSpawnRequest(slimeConfig.ConfigId, Vector2.left));
            var enemyId = enemies.SpawnEnemy(new EnemySpawnRequest(enemyConfig.ConfigId, Vector2.right));
            Assert.IsTrue(slimes.TryGetActor(slimeId, out var slime));
            Assert.IsTrue(enemies.TryGetActor(enemyId, out var enemy));
            Assert.AreSame(slimeRoot.transform, slime.transform.parent);
            Assert.AreSame(enemyRoot.transform, enemy.transform.parent);
            Assert.AreEqual(Vector2.left, (Vector2)slime.transform.position);
            Assert.AreEqual(Vector2.right, (Vector2)enemy.transform.position);

            slimes.Dispose();
            enemies.Dispose();
        }

        [Test]
        public void ActorHealthAndStatusAreIndependentAndDeathStopsActor()
        {
            var slime = New<SlimeConfig>("slime");
            slime.ConfigureStats(10, 100, 1f, 1f);
            var registry = new ConfigRegistry(new ConfigAsset[] { slime });
            var manager = new SlimeManager(registry, null, 2, NewSlimePrefab());
            var first = manager.SpawnSlime(new SlimeSpawnRequest(slime.ConfigId, Vector2.zero));
            var second = manager.SpawnSlime(new SlimeSpawnRequest(slime.ConfigId, Vector2.right));
            Assert.IsTrue(manager.TryGetActor(first, out var firstActor));
            Assert.IsTrue(firstActor.ApplyResolvedEffect(ResolvedEffect.Damage(40f)).IsAccepted);
            Assert.IsTrue(firstActor.ApplyResolvedEffect(new ResolvedEffect(
                EffectType.Status, 0f, first, "slow")).Changed);
            Assert.IsTrue(manager.TryGetSnapshot(first, out var firstSnapshot));
            Assert.IsTrue(manager.TryGetSnapshot(second, out var secondSnapshot));
            Assert.AreEqual(60f, firstSnapshot.CurrentHealth);
            Assert.AreEqual(100f, secondSnapshot.CurrentHealth);
            CollectionAssert.Contains(firstSnapshot.StatusIds, "slow");
            CollectionAssert.DoesNotContain(secondSnapshot.StatusIds, "slow");
            Assert.IsTrue(firstActor.RemoveResolvedStatus("slow"));
            CollectionAssert.Contains(firstSnapshot.StatusIds, "slow");
            Assert.IsTrue(manager.TryGetSnapshot(first, out var afterRemoval));
            CollectionAssert.DoesNotContain(afterRemoval.StatusIds, "slow");
            Assert.IsTrue(firstActor.ApplyResolvedEffect(ResolvedEffect.Damage(60f)).DidKill);
            Assert.AreEqual(1, manager.GetAliveSnapshots().Count);
            Assert.IsFalse(firstActor.ApplyResolvedEffect(ResolvedEffect.Heal(1f)).IsAccepted);
            manager.Dispose();
        }

        [Test]
        public void EnemyDeathRemovesItFromAliveSetAndOldIdCannotBeReused()
        {
            var config = New<EnemyConfig>("enemy");
            config.ConfigureStats(10, 80, 1f, 1f);
            var manager = new EnemyManager(new ConfigRegistry(new ConfigAsset[] { config }),
                null, NewEnemyPrefab());
            var id = manager.SpawnEnemy(new EnemySpawnRequest(config.ConfigId, Vector2.zero));
            Assert.IsTrue(manager.TryGetActor(id, out var actor));
            Assert.IsTrue(actor.ApplyResolvedEffect(ResolvedEffect.Damage(80f)).DidKill);
            Assert.AreEqual(0, manager.GetAliveSnapshots().Count);
            Assert.IsTrue(manager.TryGetSnapshot(id, out var dead));
            Assert.IsFalse(dead.IsAlive);
            Assert.IsFalse(actor.ApplyResolvedEffect(ResolvedEffect.Heal(1f)).IsAccepted);
            Assert.IsTrue(manager.RequestDespawn(id, DespawnReason.Removed));
            Assert.IsFalse(manager.TryGetSnapshot(id, out var ignored));
            manager.Dispose();
        }

        [Test]
        public void ExplicitGroupTracksMembersWithoutCopyingHealth()
        {
            var config = New<SlimeConfig>("slime");
            var manager = new SlimeManager(new ConfigRegistry(new ConfigAsset[] { config }),
                null, 2, NewSlimePrefab());
            var first = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId, Vector2.zero));
            var second = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId, Vector2.right));
            Assert.IsTrue(manager.TryCreateGroup(new[] { first, second }, Vector2.up, out var groupId));
            Assert.IsTrue(manager.TryGetGroupSnapshot(groupId, out var group));
            Assert.AreEqual(2, group.MemberIds.Count);
            Assert.IsTrue(manager.TrySetGroupTarget(groupId, Vector2.left));
            Assert.IsTrue(manager.RequestDespawn(first, DespawnReason.Removed));
            Assert.IsTrue(manager.TryGetGroupSnapshot(groupId, out group));
            Assert.AreEqual(1, group.MemberIds.Count);
            Assert.IsTrue(manager.RequestDissolveGroup(groupId));
            Assert.IsFalse(manager.TryGetGroupSnapshot(groupId, out group));
            manager.Dispose();
        }

        [Test]
        public void FiftyConcurrentSlimesAreAcceptedAndFiftyFirstIsRejected()
        {
            var config = New<SlimeConfig>("slime");
            var manager = new SlimeManager(
                new ConfigRegistry(new ConfigAsset[] { config }), null, 50, NewSlimePrefab());
            var unique = new HashSet<ActorId>();
            for (var i = 0; i < 50; i++)
            {
                var id = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId, Vector2.zero));
                Assert.IsTrue(id.IsValid);
                Assert.IsTrue(unique.Add(id));
            }
            Assert.IsFalse(manager.SpawnSlime(
                new SlimeSpawnRequest(config.ConfigId, Vector2.zero)).IsValid);
            Assert.AreEqual(50, manager.GetAliveSnapshots().Count);
            manager.Dispose();
        }

        [Test]
        public void FirstLevelCapRejectsTwentyFirstSlimeWithoutChangingExistingActors()
        {
            var config = New<SlimeConfig>("slime");
            var manager = new SlimeManager(
                new ConfigRegistry(new ConfigAsset[] { config }), null, 20, NewSlimePrefab());
            try
            {
                var first = default(ActorId);
                for (var i = 0; i < 20; i++)
                {
                    var id = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId, Vector2.zero));
                    Assert.IsTrue(id.IsValid);
                    if (i == 0) first = id;
                }
                Assert.IsFalse(manager.SpawnSlime(
                    new SlimeSpawnRequest(config.ConfigId, Vector2.zero)).IsValid);
                Assert.AreEqual(20, manager.GetAliveSnapshots().Count);
                Assert.IsTrue(manager.TryGetSnapshot(first, out var snapshot));
                Assert.IsTrue(snapshot.IsAlive);
            }
            finally { manager.Dispose(); }
        }

        [Test]
        public void DepletedEventFiresOnlyAfterLastDeathAndResetsWithNewManager()
        {
            var config = New<SlimeConfig>("slime");
            var registry = new ConfigRegistry(new ConfigAsset[] { config });
            using (var bus = new LevelEventBus())
            {
                var count = 0;
                bus.Subscribe<SlimeDepletedEvent>(e =>
                {
                    Assert.AreEqual(new LevelId("first"), e.LevelId);
                    Assert.AreEqual(0, e.AliveCount);
                    count++;
                });
                var manager = new SlimeManager(registry, bus, 20, NewSlimePrefab(),
                    levelId: new LevelId("first"));
                manager.Tick(new LevelTickContext(.1f, null, null));
                manager.Tick(new LevelTickContext(.1f, null, null));
                Assert.AreEqual(0, count);
                manager.Dispose();

                manager = new SlimeManager(registry, bus, 20, NewSlimePrefab(),
                    levelId: new LevelId("first"));
                var first = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId,
                    Vector2.zero));
                var second = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId,
                    Vector2.right));
                Assert.IsTrue(manager.TryGetActor(first, out var slime));
                slime.ApplyResolvedEffect(ResolvedEffect.Damage(config.MaxHealth));
                Assert.AreEqual(0, count);
                Assert.IsTrue(manager.TryGetActor(second, out slime));
                slime.ApplyResolvedEffect(ResolvedEffect.Damage(config.MaxHealth));
                Assert.AreEqual(1, count);
                manager.Tick(new LevelTickContext(.1f, null, null));
                Assert.AreEqual(1, count);
                manager.Dispose();

                manager = new SlimeManager(registry, bus, 20, NewSlimePrefab(),
                    levelId: new LevelId("first"));
                var id = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId,
                    Vector2.zero));
                Assert.IsTrue(manager.RequestDespawn(id, DespawnReason.Removed));
                Assert.AreEqual(1, count);
                id = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId,
                    Vector2.zero));
                Assert.IsTrue(manager.TryGetActor(id, out slime));
                slime.ApplyResolvedEffect(ResolvedEffect.Damage(config.MaxHealth));
                Assert.AreEqual(2, count);
                manager.Dispose();
            }
        }

        [Test]
        public void GoalCountTracksOnlyLivingSlimesCurrentlyInside()
        {
            var config = New<SlimeConfig>("slime");
            var goalId = new MapAreaId("goal");
            var goal = new MapAreaSnapshot(goalId, MapAreaKind.Goal,
                default, default, default, 1f, string.Empty);
            using (var bus = new LevelEventBus())
            {
                var changes = 0;
                bus.Subscribe<SlimeGoalOccupancyChangedEvent>(_ => changes++);
                var manager = new SlimeManager(
                    new ConfigRegistry(new ConfigAsset[] { config }), bus,
                    20, NewSlimePrefab(), goalAreaId: goalId);
                var first = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId, Vector2.zero));
                var second = manager.SpawnSlime(new SlimeSpawnRequest(config.ConfigId, Vector2.right));
                bus.Publish(new MapAreaEnteredEvent(first, goal, Vector2.zero));
                bus.Publish(new MapAreaEnteredEvent(first, goal, Vector2.zero));
                bus.Publish(new MapAreaEnteredEvent(second, goal, Vector2.right));
                Assert.AreEqual(2, manager.GoalOccupantCount);
                Assert.AreEqual(2, changes);
                bus.Publish(new MapAreaExitedEvent(first, goal, Vector2.left));
                Assert.AreEqual(1, manager.GoalOccupantCount);
                Assert.IsTrue(manager.TryGetActor(second, out var slime));
                slime.ApplyResolvedEffect(ResolvedEffect.Damage(config.MaxHealth));
                Assert.AreEqual(0, manager.GoalOccupantCount);
                Assert.AreEqual(4, changes);
                manager.Dispose();
                bus.Publish(new MapAreaEnteredEvent(first, goal, Vector2.zero));
                Assert.AreEqual(4, changes);
            }
        }

        [Test]
        public void GuardRejectsStatusAndAcceptsDamageWithoutChangingItsDefenseState()
        {
            var config = New<GuardConfig>("guard");
            config.ConfigureStats(10, 100, 1f, 1f);
            var prefabObject = new GameObject("Test Guard Prefab");
            assets.Add(prefabObject);
            prefabObject.AddComponent<CircleCollider2D>();
            var prefab = prefabObject.AddComponent<GuardActor>();
            var manager = new GuardManager(
                new ConfigRegistry(new ConfigAsset[] { config }), null, prefab);
            try
            {
                var id = manager.SpawnGuard(new GuardSpawnRequest(config.ConfigId, Vector2.zero));
                Assert.IsTrue(id.IsValid);
                Assert.IsTrue(manager.TryGetActor(id, out var guard));
                var guardRoot = new GameObject("Guard Root");
                assets.Add(guardRoot);
                guardRoot.transform.position = new Vector3(3f, 2f, 0f);
                var spawnPosition = guard.transform.position;
                Assert.IsTrue(manager.TrySetSpawnParent(guardRoot.transform));
                Assert.AreSame(guardRoot.transform, guard.transform.parent);
                Assert.AreEqual(spawnPosition, guard.transform.position);
                var before = guard.GetSnapshot();
                Assert.IsFalse(guard.ApplyResolvedEffect(new ResolvedEffect(
                    EffectType.Status, 0f, id, "hot")).IsAccepted);
                Assert.IsFalse(guard.ApplyResolvedEffect(new ResolvedEffect(
                    EffectType.Status, 0f, id, "cold")).IsAccepted);
                Assert.IsTrue(guard.ApplyResolvedEffect(ResolvedEffect.Damage(1f)).IsAccepted);
                var after = guard.GetSnapshot();
                Assert.AreEqual(0, after.StatusIds.Count);
                Assert.AreEqual(before.CurrentHealth - 1f, after.CurrentHealth);
                Assert.AreEqual(before.ActionState, after.ActionState);
            }
            finally { manager.Dispose(); }
        }

        [Test]
        public void CarrierRemainsAliveUntilAttackThenDetachesWithoutLosingRuntimeHealth()
        {
            var slimeConfig = New<SlimeConfig>("slime");
            var enemyConfig = New<EnemyConfig>("enemy");
            var registry = new ConfigRegistry(new ConfigAsset[] { slimeConfig, enemyConfig });
            var slimes = new SlimeManager(registry, null, 20, NewSlimePrefab());
            var enemies = new EnemyManager(registry, null, NewEnemyPrefab());
            var weapon = new GameObject("test weapon");
            assets.Add(weapon);
            try
            {
                var slimeId = slimes.SpawnSlime(new SlimeSpawnRequest(
                    slimeConfig.ConfigId, Vector2.zero));
                var enemyId = enemies.SpawnEnemy(new EnemySpawnRequest(
                    enemyConfig.ConfigId, Vector2.up));
                Assert.IsTrue(slimes.TryAttachCarrier(slimeId, weapon.transform,
                    Vector2.zero));
                Assert.IsTrue(slimes.TryGetActor(slimeId, out var carrier));
                weapon.transform.position = Vector2.up;
                Assert.IsTrue(slimes.TryGetSnapshot(slimeId, out var snapshot));
                Assert.IsTrue(snapshot.IsAlive && snapshot.IsCarrying);
                Assert.AreEqual(Vector2.up, snapshot.Position);
                Assert.AreEqual(1, slimes.GetAliveSnapshots().Count);
                var perception = new ActorTargetPerceptionSource(slimes, enemies, registry);
                Assert.AreEqual(0, perception.GetPerception(enemyId).Targets.Count);
                var health = snapshot.CurrentHealth;
                Assert.IsFalse(carrier.ApplyResolvedEffect(
                    ResolvedEffect.Damage(100f, enemyId)).IsAccepted);
                Assert.IsTrue(slimes.TryGetSnapshot(slimeId, out snapshot));
                Assert.IsFalse(snapshot.IsCarrying);
                Assert.AreEqual(health, snapshot.CurrentHealth);
            }
            finally { slimes.Dispose(); enemies.Dispose(); }
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
            actorObject.AddComponent<CircleCollider2D>();
            var actor = actorObject.AddComponent<SlimeActor>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(actorObject.transform, false);
            var texture = new Texture2D(16, 16);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16),
                new Vector2(.5f, .5f), 100f);
            visual.AddComponent<SpriteRenderer>().sprite = sprite;
            assets.Add(sprite);
            assets.Add(texture);
            assets.Add(actorObject);
            return actor;
        }

        private EnemyActor NewEnemyPrefab()
        {
            var actorObject = new GameObject("Test Enemy Prefab");
            assets.Add(actorObject);
            actorObject.AddComponent<CircleCollider2D>();
            return actorObject.AddComponent<EnemyActor>();
        }
    }
}
