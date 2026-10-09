/* Manager 生命周期测试：只通过公开生成/查询/销毁接口，效果通过关卡内部解析替身施加。 */
using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Slimes;
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
            Assert.IsNotNull(firstComponent.GetComponent<SpriteRenderer>());
            Assert.AreNotSame(slimePrefab, firstComponent);
            Assert.IsTrue(enemies.TryGetActor(hostile, out var enemyComponent));
            Assert.IsNotNull(enemyComponent.GetComponent<SpriteRenderer>());
            Assert.AreNotSame(enemyPrefab, enemyComponent);
            Assert.IsFalse(rejected.IsValid);
            StringAssert.Contains("limit", slimes.LastFailureReason);
            Assert.AreEqual(2, slimes.GetAliveSnapshots().Count);
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
            return actorObject.AddComponent<SlimeActor>();
        }

        private EnemyActor NewEnemyPrefab()
        {
            var actorObject = new GameObject("Test Enemy Prefab");
            assets.Add(actorObject);
            return actorObject.AddComponent<EnemyActor>();
        }
    }
}
