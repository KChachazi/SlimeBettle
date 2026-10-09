using System;

namespace SlimeBattle.World
{
    public readonly struct MapAreaId : IEquatable<MapAreaId>
    {
        public MapAreaId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(MapAreaId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MapAreaId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(MapAreaId left, MapAreaId right) => left.Equals(right);
        public static bool operator !=(MapAreaId left, MapAreaId right) => !left.Equals(right);
    }
}
