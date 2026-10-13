using System;
using SlimeBattle.Mechanisms;
using SlimeBattle.Slimes;

namespace SlimeBattle.Core
{
    /// <summary>Resolves one level's fact events at the end of each running frame.</summary>
    public sealed class LevelResultRules : IDisposable
    {
        private readonly ILevelEventBus bus;
        private readonly SubscriptionToken reachedToken;
        private readonly SubscriptionToken destroyedToken;
        private readonly SubscriptionToken depletedToken;
        private bool reached;
        private bool failed;
        private bool decided;
        private bool disposed;

        public LevelResultRules(ILevelEventBus bus)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            reachedToken = bus.Subscribe<WeaponReachedGoalEvent>(_ => reached = true);
            destroyedToken = bus.Subscribe<WeaponDestroyedEvent>(_ => failed = true);
            depletedToken = bus.Subscribe<SlimeDepletedEvent>(_ => failed = true);
        }

        public event Action<LevelResult> ResultDecided;
        public bool HasResult => decided;
        public LevelResult Result { get; private set; }

        public void EndFrame()
        {
            if (disposed || decided || (!reached && !failed)) return;
            Result = reached ? LevelResult.Succeeded : LevelResult.Failed;
            decided = true;
            ResultDecided?.Invoke(Result);
        }

        public void Dispose()
        {
            if (disposed) return;
            bus.Unsubscribe(reachedToken);
            bus.Unsubscribe(destroyedToken);
            bus.Unsubscribe(depletedToken);
            ResultDecided = null;
            disposed = true;
        }
    }
}
