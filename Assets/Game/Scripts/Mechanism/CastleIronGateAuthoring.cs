using SlimeBattle.Core;
using SlimeBattle.Enemies;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    /// <summary>Opens the Castle route and removes its static movement blocker when its enemy dies.</summary>
    [DisallowMultipleComponent]
    public sealed class CastleIronGateAuthoring : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer gateRenderer;
        [SerializeField] private Collider2D gateCollider;
        [SerializeField, Min(1)] private int enemiesToDefeat = 2;

        private LevelManager levelManager;
        private ILevelEventBus eventBus;
        private SubscriptionToken enemyDiedToken;
        private bool isOpen;
        private int defeatedEnemyCount;

        private void OnEnable()
        {
            if (gateRenderer == null) gateRenderer = GetComponent<SpriteRenderer>();
            if (gateCollider == null) gateCollider = GetComponent<Collider2D>();
            AttachToLevelManager();
        }

        private void Start() => AttachToLevelManager();

        private void AttachToLevelManager()
        {
            if (levelManager != null) return;
            levelManager = FindObjectOfType<LevelManager>();
            if (levelManager == null) return;
            levelManager.LevelStarted += OnLevelStarted;
            var context = levelManager.GetCurrentContext();
            if (context != null && levelManager.GetLevelState() == LevelState.Running)
                Bind(context);
        }

        private void OnDisable()
        {
            if (levelManager != null) levelManager.LevelStarted -= OnLevelStarted;
            if (eventBus != null) eventBus.Unsubscribe(enemyDiedToken);
            eventBus = null;
            levelManager = null;
        }

        private void OnLevelStarted(LevelStartedEvent started)
        {
            if (started.LevelId.Value == gameObject.scene.name) Bind(started.Context);
        }

        private void Bind(LevelContext context)
        {
            if (context == null || eventBus == context.GetEventBus()) return;
            if (eventBus != null) eventBus.Unsubscribe(enemyDiedToken);
            eventBus = context.GetEventBus();
            isOpen = false;
            defeatedEnemyCount = 0;
            if (gateCollider != null) gateCollider.enabled = true;
            if (gateRenderer != null) gateRenderer.enabled = true;
            enemyDiedToken = eventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnEnemyDied(EnemyDiedEvent _)
        {
            if (isOpen) return;
            defeatedEnemyCount++;
            if (defeatedEnemyCount >= Mathf.Max(1, enemiesToDefeat)) Open();
        }

        private void Open()
        {
            if (isOpen) return;
            isOpen = true;
            if (gateCollider != null) gateCollider.enabled = false;
            if (gateRenderer != null) gateRenderer.enabled = false;
        }
    }
}
