/* FactionId 是角色阵营的公开值类型。 */
using System;

namespace SlimeBattle.Actors
{
    public readonly struct FactionId : IEquatable<FactionId>
    {
        public FactionId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(FactionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is FactionId && Equals((FactionId)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(FactionId left, FactionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FactionId left, FactionId right)
        {
            return !left.Equals(right);
        }
    }
}
