using System;

namespace SlimeBattle.Core
{
    public sealed class LevelContext : IDisposable
    {
        private LevelState state;
        private bool disposed;

        public LevelContext(LevelId levelId)
        {
            if (!levelId.IsValid)
            {
                throw new ArgumentException("A valid level ID is required.", nameof(levelId));
            }

            LevelId = levelId;
            EventBus = new LevelEventBus();
            state = LevelState.Unloaded;
        }

        public LevelId LevelId { get; }

        public LevelEventBus EventBus { get; }

        public LevelId GetLevelId()
        {
            return LevelId;
        }

        public LevelState GetLevelState()
        {
            return state;
        }

        public ILevelEventBus GetEventBus()
        {
            return EventBus;
        }

        public void Initialize()
        {
            ThrowIfDisposed();
            SetState(LevelState.Initializing);
        }

        internal void SetState(LevelState nextState)
        {
            ThrowIfDisposed();

            if (state == nextState)
            {
                return;
            }

            var previousState = state;
            state = nextState;
            EventBus.Publish(new LevelStateChangedEvent(LevelId, previousState, nextState));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            EventBus.Dispose();
            state = LevelState.Unloaded;
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LevelContext));
            }
        }
    }
}
