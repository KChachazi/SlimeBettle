using System;

namespace SlimeBattle.Mechanisms
{
    public readonly struct MechanismId : IEquatable<MechanismId>
    {
        public MechanismId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public static MechanismId NewId() => new MechanismId(Guid.NewGuid().ToString("N"));

        public bool Equals(MechanismId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is MechanismId && Equals((MechanismId)obj);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(MechanismId left, MechanismId right) => left.Equals(right);
        public static bool operator !=(MechanismId left, MechanismId right) => !left.Equals(right);
    }
}
