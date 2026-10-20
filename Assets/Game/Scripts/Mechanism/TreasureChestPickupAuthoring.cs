using SlimeBattle.Actors.Slimes;
using SlimeBattle.Core;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    /// <summary>A one-use Slime contact pickup that awards campaign food.</summary>
    [DisallowMultipleComponent]
    public sealed class TreasureChestPickupAuthoring : MonoBehaviour
    {
        private LevelManager levelManager;
        private int foodReward;
        private bool collected;

        public void Initialize(LevelManager manager, int reward)
        {
            levelManager = manager;
            foodReward = Mathf.Max(0, reward);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || other == null || foodReward <= 0) return;
            var slime = other.GetComponentInParent<SlimeActor>();
            if (slime == null || !slime.IsAlive || levelManager == null ||
                levelManager.GetLevelState() != LevelState.Running) return;

            var context = levelManager.GetCurrentContext();
            if (context == null || context.CampaignResources == null ||
                context.CampaignResources.AddFood(foodReward) <= 0) return;

            collected = true;
            gameObject.SetActive(false);
            Debug.Log($"Treasure chest collected by Slime {slime.ActorId}; " +
                $"food reward: {foodReward}.", this);
        }
    }
}
