using System;

namespace SlimeBattle.World.Movement
{
    public readonly struct MovementRequestId : IEquatable<MovementRequestId>
    {
        public MovementRequestId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => Guid.TryParseExact(Value, "N", out _);

        public static MovementRequestId NewId()
        {
            return new MovementRequestId(Guid.NewGuid().ToString("N"));
        }

        public static bool TryParse(string value, out MovementRequestId requestId)
        {
            requestId = default(MovementRequestId);
            if (!Guid.TryParseExact(value, "N", out _)) return false;
            requestId = new MovementRequestId(value);
            return true;
        }

        public bool Equals(MovementRequestId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MovementRequestId && Equals((MovementRequestId)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(MovementRequestId left, MovementRequestId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MovementRequestId left, MovementRequestId right)
        {
            return !left.Equals(right);
        }
    }
}
