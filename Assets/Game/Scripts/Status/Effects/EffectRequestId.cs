using System;

namespace SlimeBattle.Status
{
    public readonly struct EffectRequestId : IEquatable<EffectRequestId>
    {
        public EffectRequestId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static EffectRequestId NewId() => new EffectRequestId(Guid.NewGuid().ToString("N"));
        public bool Equals(EffectRequestId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EffectRequestId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(EffectRequestId left, EffectRequestId right) => left.Equals(right);
        public static bool operator !=(EffectRequestId left, EffectRequestId right) => !left.Equals(right);
    }
}
