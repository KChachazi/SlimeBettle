using System;

namespace SlimeBattle.Core
{
    public readonly struct LevelId : IEquatable<LevelId>
    {
        public LevelId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(LevelId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is LevelId && Equals((LevelId)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(LevelId left, LevelId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LevelId left, LevelId right)
        {
            return !left.Equals(right);
        }
    }

    public enum ApplicationState
    {
        Created,
        Initializing,
        Ready,
        Quitting,
        Stopped
    }

    public enum LevelState
    {
        Unloaded,
        Loading,
        Initializing,
        Running,
        Paused,
        Succeeded,
        Failed,
        Unloading
    }

    public enum LevelResult
    {
        Succeeded,
        Failed
    }

    public readonly struct ApplicationStateChangedEvent
    {
        public ApplicationStateChangedEvent(
            ApplicationState previousState,
            ApplicationState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public ApplicationState PreviousState { get; }
        public ApplicationState CurrentState { get; }
    }

    public readonly struct LevelStateChangedEvent
    {
        public LevelStateChangedEvent(
            LevelId levelId,
            LevelState previousState,
            LevelState currentState)
        {
            LevelId = levelId;
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public LevelId LevelId { get; }
        public LevelState PreviousState { get; }
        public LevelState CurrentState { get; }
    }

    public readonly struct LevelLoadStartedEvent
    {
        public LevelLoadStartedEvent(LevelId levelId, bool isRestart)
        {
            LevelId = levelId;
            IsRestart = isRestart;
        }

        public LevelId LevelId { get; }
        public bool IsRestart { get; }
    }

    public readonly struct LevelStartedEvent
    {
        public LevelStartedEvent(LevelId levelId, LevelContext context)
        {
            LevelId = levelId;
            Context = context;
        }

        public LevelId LevelId { get; }
        public LevelContext Context { get; }
    }

    public readonly struct LevelLoadFailedEvent
    {
        public LevelLoadFailedEvent(LevelId levelId, string reason)
        {
            LevelId = levelId;
            Reason = reason ?? string.Empty;
        }

        public LevelId LevelId { get; }
        public string Reason { get; }
    }

    public readonly struct LevelRestartedEvent
    {
        public LevelRestartedEvent(LevelId levelId)
        {
            LevelId = levelId;
        }

        public LevelId LevelId { get; }
    }

    public readonly struct LevelResultReportedEvent
    {
        public LevelResultReportedEvent(LevelId levelId, LevelResult result)
        {
            LevelId = levelId;
            Result = result;
        }

        public LevelId LevelId { get; }
        public LevelResult Result { get; }
    }

    public readonly struct ReturnToPreviousFlowRequestedEvent
    {
        public ReturnToPreviousFlowRequestedEvent(LevelId levelId)
        {
            LevelId = levelId;
        }

        public LevelId LevelId { get; }
    }
}
