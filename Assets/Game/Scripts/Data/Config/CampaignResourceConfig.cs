using System;
using System.Collections.Generic;
using SlimeBattle.Core;
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Campaign Resources",
        fileName = "CFG_CampaignResources")]
    public sealed class CampaignResourceConfig : ScriptableObject
    {
        [Serializable]
        public sealed class LevelRewardEntry
        {
            [SerializeField] private string levelId;
            [SerializeField, Min(0)] private int slimeReward;
            [SerializeField, Min(0)] private int foodReward;
            [SerializeField, Min(0)] private int furnaceReward;
            [SerializeField, Min(0)] private int obstacleReward;

            public string LevelId => levelId ?? string.Empty;
            public int SlimeReward => slimeReward;
            public int FoodReward => foodReward;
            public int FurnaceReward => furnaceReward;
            public int ObstacleReward => obstacleReward;

            public LevelRewardEntry() { }

            public LevelRewardEntry(string newLevelId, int slimes, int food,
                int furnaces, int obstacles)
            {
                levelId = newLevelId;
                slimeReward = slimes;
                foodReward = food;
                furnaceReward = furnaces;
                obstacleReward = obstacles;
            }
        }

        [SerializeField, Min(1)] private int initialSlimes = 8;
        [SerializeField, Min(1)] private int maximumSlimes = 20;
        [SerializeField, Min(0)] private int initialFood = 10;
        [SerializeField, Min(0)] private int maximumFood = 30;
        [SerializeField, Min(0)] private int initialFurnaces = 3;
        [SerializeField, Min(0)] private int maximumFurnaces = 10;
        [SerializeField, Min(0)] private int initialObstacles = 5;
        [SerializeField, Min(0)] private int maximumObstacles = 15;
        [SerializeField] private bool foodReproductionEnabled;
        [SerializeField] private List<LevelRewardEntry> levelRewards =
            new List<LevelRewardEntry>();

        public int InitialSlimes => initialSlimes;
        public int MaximumSlimes => maximumSlimes;
        public int InitialFood => initialFood;
        public int MaximumFood => maximumFood;
        public int InitialFurnaces => initialFurnaces;
        public int MaximumFurnaces => maximumFurnaces;
        public int InitialObstacles => initialObstacles;
        public int MaximumObstacles => maximumObstacles;
        public bool FoodReproductionEnabled => foodReproductionEnabled;

        public bool TryGetReward(LevelId levelId, out LevelRewardEntry reward)
        {
            reward = null;
            if (!levelId.IsValid || levelRewards == null) return false;
            foreach (var entry in levelRewards)
                if (entry != null && string.Equals(entry.LevelId, levelId.Value,
                    StringComparison.Ordinal))
                {
                    reward = entry;
                    return true;
                }
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (initialSlimes < 1 || maximumSlimes < initialSlimes || maximumSlimes > 20 ||
                initialFood < 0 || maximumFood < initialFood ||
                initialFurnaces < 0 || maximumFurnaces < initialFurnaces ||
                initialObstacles < 0 || maximumObstacles < initialObstacles)
            {
                error = "Campaign resource starts and caps are inconsistent.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (levelRewards != null)
                foreach (var entry in levelRewards)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.LevelId) ||
                        entry.SlimeReward < 0 || entry.FoodReward < 0 ||
                        entry.FurnaceReward < 0 || entry.ObstacleReward < 0 ||
                        !ids.Add(entry.LevelId))
                    {
                        error = "Campaign rewards require unique level IDs and non-negative values.";
                        return false;
                    }
                }

            error = string.Empty;
            return true;
        }

        public void ConfigureInitialResources(int slimes, int maxSlimes,
            int food, int maxFood, int furnaces, int maxFurnaces,
            int obstacles, int maxObstacles)
        {
            EnsureAuthoring();
            initialSlimes = slimes;
            maximumSlimes = maxSlimes;
            initialFood = food;
            maximumFood = maxFood;
            initialFurnaces = furnaces;
            maximumFurnaces = maxFurnaces;
            initialObstacles = obstacles;
            maximumObstacles = maxObstacles;
        }

        public void SetFoodReproductionEnabled(bool value)
        {
            EnsureAuthoring();
            foodReproductionEnabled = value;
        }

        public void SetLevelRewards(IEnumerable<LevelRewardEntry> values)
        {
            EnsureAuthoring();
            levelRewards = values == null
                ? new List<LevelRewardEntry>()
                : new List<LevelRewardEntry>(values);
        }

        private void EnsureAuthoring()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Campaign config is read-only during play.");
        }
    }
}
