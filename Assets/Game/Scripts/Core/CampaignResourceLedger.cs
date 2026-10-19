using System;
using System.Collections.Generic;
using SlimeBattle.Data;
using SlimeBattle.Mechanisms;

namespace SlimeBattle.Core
{
    /// <summary>Mutable resources for one in-memory campaign run.</summary>
    public sealed class CampaignResourceLedger
    {
        private readonly CampaignResourceConfig config;
        private readonly HashSet<string> rewardedLevels =
            new HashSet<string>(StringComparer.Ordinal);
        private ResourceSnapshot entrySnapshot;
        private bool hasEntrySnapshot;

        public CampaignResourceLedger(CampaignResourceConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            if (!config.TryValidate(out var error))
                throw new ArgumentException(error, nameof(config));
        }

        public bool IsStarted { get; private set; }
        public int SlimeCount { get; private set; }
        public int MaximumSlimes => config.MaximumSlimes;
        public int FoodCount { get; private set; }
        public int FurnaceCount { get; private set; }
        public int ObstacleCount { get; private set; }
        public bool FoodReproductionEnabled => config.FoodReproductionEnabled;

        public void StartNewCampaign()
        {
            SlimeCount = config.InitialSlimes;
            FoodCount = config.InitialFood;
            FurnaceCount = config.InitialFurnaces;
            ObstacleCount = config.InitialObstacles;
            rewardedLevels.Clear();
            ActiveLevelId = default;
            hasEntrySnapshot = false;
            IsStarted = true;
        }

        public LevelId ActiveLevelId { get; private set; }

        public void BeginLevel(LevelId levelId, bool isRestart)
        {
            if (!levelId.IsValid) throw new ArgumentException("A level ID is required.", nameof(levelId));
            if (!IsStarted) StartNewCampaign();

            if (isRestart && ActiveLevelId == levelId && hasEntrySnapshot)
            {
                Restore(entrySnapshot);
                rewardedLevels.Remove(levelId.Value);
                return;
            }

            ActiveLevelId = levelId;
            entrySnapshot = Capture();
            hasEntrySnapshot = true;
        }

        public int GetMechanismCount(MechanismKind kind)
        {
            switch (kind)
            {
                case MechanismKind.Food: return FoodCount;
                case MechanismKind.Furnace: return FurnaceCount;
                case MechanismKind.Obstacle: return ObstacleCount;
                default: return 0;
            }
        }

        public bool TrySpendMechanism(MechanismKind kind)
        {
            if (!IsStarted) return false;
            switch (kind)
            {
                case MechanismKind.Food:
                    if (FoodCount <= 0) return false;
                    FoodCount--;
                    return true;
                case MechanismKind.Furnace:
                    if (FurnaceCount <= 0) return false;
                    FurnaceCount--;
                    return true;
                case MechanismKind.Obstacle:
                    if (ObstacleCount <= 0) return false;
                    ObstacleCount--;
                    return true;
                default: return false;
            }
        }

        public void AwardLevel(LevelId levelId, int livingSlimes)
        {
            if (!IsStarted || ActiveLevelId != levelId ||
                !config.TryGetReward(levelId, out var reward) ||
                !rewardedLevels.Add(levelId.Value)) return;

            SlimeCount = Math.Min(config.MaximumSlimes,
                Math.Max(0, livingSlimes) + reward.SlimeReward);
            FoodCount = Math.Min(config.MaximumFood, FoodCount + reward.FoodReward);
            FurnaceCount = Math.Min(config.MaximumFurnaces,
                FurnaceCount + reward.FurnaceReward);
            ObstacleCount = Math.Min(config.MaximumObstacles,
                ObstacleCount + reward.ObstacleReward);
        }

        private ResourceSnapshot Capture() => new ResourceSnapshot(
            SlimeCount, FoodCount, FurnaceCount, ObstacleCount);

        private void Restore(ResourceSnapshot snapshot)
        {
            SlimeCount = snapshot.Slimes;
            FoodCount = snapshot.Food;
            FurnaceCount = snapshot.Furnaces;
            ObstacleCount = snapshot.Obstacles;
        }

        private readonly struct ResourceSnapshot
        {
            public ResourceSnapshot(int slimes, int food, int furnaces, int obstacles)
            { Slimes = slimes; Food = food; Furnaces = furnaces; Obstacles = obstacles; }
            public int Slimes { get; }
            public int Food { get; }
            public int Furnaces { get; }
            public int Obstacles { get; }
        }
    }
}
