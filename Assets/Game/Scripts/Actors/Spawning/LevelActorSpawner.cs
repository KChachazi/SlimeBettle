/* 场景初始角色清单；Level_test 注入随机漫游感知，Actor 生命周期仍由正式 Manager 管理。 */
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Slimes;
using UnityEngine;

namespace SlimeBattle.Actors.Spawning
{
    public sealed class LevelActorSpawner : MonoBehaviour
    {
        [SerializeField] private SlimeConfig slimeConfig;
        [SerializeField] private EnemyConfig enemyConfig;
        [SerializeField] private Transform slimeRoot;
        [SerializeField] private Transform enemyRoot;
        [SerializeField] private Transform[] slimeSpawnPoints;
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Vector2 enemyBoundsMinimum = new Vector2(-6f, -4f);
        [SerializeField] private Vector2 enemyBoundsMaximum = new Vector2(6f, 4f);

        private LevelManager levelManager;
        private LevelContext spawnedContext;

        private void OnEnable()
        {
            var root = FindObjectOfType<GameRoot>();
            if (root == null) return;
            levelManager = root.GetLevelManager();
            if (levelManager == null) return;
            levelManager.LevelStarted += OnLevelStarted;
            if (levelManager.GetLevelState() == LevelState.Running)
                Spawn(levelManager.GetCurrentContext());
        }

        private void OnDisable()
        {
            if (levelManager != null) levelManager.LevelStarted -= OnLevelStarted;
            levelManager = null;
            spawnedContext = null;
        }

        private void OnLevelStarted(LevelStartedEvent started)
        {
            if (started.LevelId.Value == gameObject.scene.name) Spawn(started.Context);
        }

        private void Spawn(LevelContext context)
        {
            if (context == null || context == spawnedContext) return;
            if (slimeConfig == null || enemyConfig == null ||
                slimeRoot == null || enemyRoot == null ||
                slimeSpawnPoints == null || enemySpawnPoints == null)
            {
                Debug.LogError("LevelActorSpawner: configs, actor roots or spawn points are missing.", this);
                return;
            }
            if (!context.SlimeManager.TrySetSpawnParent(slimeRoot) ||
                !context.EnemyManager.TrySetSpawnParent(enemyRoot))
            {
                Debug.LogError("LevelActorSpawner: actor roots could not be bound.", this);
                return;
            }

            spawnedContext = context;
            var slimeCount = 0;
            var enemyCount = 0;
            var bounds = new EnemyActivityBounds(enemyBoundsMinimum, enemyBoundsMaximum);
            context.BindActorPorts(new RandomWalkPerceptionSource(
                context.SlimeManager, context.EnemyManager, context.MovementSystem, bounds), null);
            foreach (var point in slimeSpawnPoints)
            {
                if (point == null) continue;
                var id = context.SlimeManager.SpawnSlime(
                    new SlimeSpawnRequest(slimeConfig.ConfigId, point.position));
                if (id.IsValid) slimeCount++;
                else Debug.LogError("Slime spawn failed: " +
                    context.SlimeManager.LastFailureReason, this);
            }
            foreach (var point in enemySpawnPoints)
            {
                if (point == null) continue;
                var id = context.EnemyManager.SpawnEnemy(
                    new EnemySpawnRequest(enemyConfig.ConfigId, point.position,
                        activityBounds: bounds));
                if (id.IsValid) enemyCount++;
                else Debug.LogError("Enemy spawn failed: " +
                    context.EnemyManager.LastFailureReason, this);
            }
            Debug.Log("Level_test initial actors: " + slimeCount +
                " Slimes, " + enemyCount + " Enemies.", this);
        }
    }
}
