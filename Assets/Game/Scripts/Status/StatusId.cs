using System;

namespace SlimeBattle.Status
{
    public readonly struct StatusId : IEquatable<StatusId>
    {
        public StatusId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(StatusId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is StatusId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(StatusId left, StatusId right) => left.Equals(right);
        public static bool operator !=(StatusId left, StatusId right) => !left.Equals(right);
    }
}
