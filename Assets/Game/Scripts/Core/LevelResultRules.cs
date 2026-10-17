using System;
using SlimeBattle.Data;
using SlimeBattle.Mechanisms;
using SlimeBattle.Slimes;

namespace SlimeBattle.Core
{
    /// <summary>Resolves the configured level objective at the end of each running frame.</summary>
    public sealed class LevelResultRules : IDisposable
    {
        private readonly ILevelEventBus bus;
        private readonly SubscriptionToken occupancyToken;
        private readonly SubscriptionToken reachedToken;
        private readonly SubscriptionToken destroyedToken;
        private readonly SubscriptionToken depletedToken;
        private readonly LevelObjectiveKind objectiveKind;
        private readonly int requiredCount;
        private readonly float waitSeconds;
        private bool depleted;
        private bool weaponReached;
        private bool weaponDestroyed;
        private bool waiting;
        private bool decided;
        private bool disposed;
        private float elapsed;

        public LevelResultRules(ILevelEventBus bus,
            LevelObjectiveKind objectiveKind = LevelObjectiveKind.CaveOccupancy,
            int requiredCount = 6,
            float waitSeconds = 10f)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            if (!Enum.IsDefined(typeof(LevelObjectiveKind), objectiveKind))
                throw new ArgumentOutOfRangeException(nameof(objectiveKind));
            if (requiredCount < 1) throw new ArgumentOutOfRangeException(nameof(requiredCount));
            if (float.IsNaN(waitSeconds) || float.IsInfinity(waitSeconds) || waitSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(waitSeconds));
            this.objectiveKind = objectiveKind;
            this.requiredCount = requiredCount;
            this.waitSeconds = waitSeconds;
            if (objectiveKind == LevelObjectiveKind.CaveOccupancy)
                occupancyToken = bus.Subscribe<SlimeGoalOccupancyChangedEvent>(OnOccupancyChanged);
            else
            {
                reachedToken = bus.Subscribe<WeaponReachedGoalEvent>(_ => weaponReached = true);
                destroyedToken = bus.Subscribe<WeaponDestroyedEvent>(_ => weaponDestroyed = true);
            }
            depletedToken = bus.Subscribe<SlimeDepletedEvent>(_ => depleted = true);
        }

        public event Action<LevelResult> ResultDecided;
        public bool HasResult => decided;
        public LevelResult Result { get; private set; }
        public LevelObjectiveKind ObjectiveKind => objectiveKind;
        public int GoalOccupantCount { get; private set; }
        public int RequiredCount => requiredCount;
        public bool IsWaiting => waiting && !decided;
        public float RemainingSeconds => IsWaiting ? Math.Max(0f, waitSeconds - elapsed) : 0f;

        public void EndFrame(float deltaTime)
        {
            if (disposed || decided) return;
            if (objectiveKind == LevelObjectiveKind.EscortWeapon)
            {
                if (weaponReached)
                    Decide(LevelResult.Succeeded);
                else if (weaponDestroyed || depleted)
                    Decide(LevelResult.Failed);
                return;
            }

            if (GoalOccupantCount >= requiredCount)
                Decide(LevelResult.Succeeded);
            else if (depleted)
                Decide(LevelResult.Failed);
            else if (waiting && deltaTime > 0f)
            {
                elapsed += deltaTime;
                if (elapsed >= waitSeconds) Decide(LevelResult.Failed);
            }
        }

        // Keep the original call shape for non-time-based callers.
        public void EndFrame()
        {
            EndFrame(0f);
        }

        private void Decide(LevelResult result)
        {
            Result = result;
            decided = true;
            ResultDecided?.Invoke(result);
        }

        private void OnOccupancyChanged(SlimeGoalOccupancyChangedEvent change)
        {
            if (disposed || decided) return;
            GoalOccupantCount = change.AliveInsideCount;
            if (change.IsInside && GoalOccupantCount > 0) waiting = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            bus.Unsubscribe(occupancyToken);
            bus.Unsubscribe(reachedToken);
            bus.Unsubscribe(destroyedToken);
            bus.Unsubscribe(depletedToken);
            ResultDecided = null;
            disposed = true;
        }
    }
}
