using System;

namespace SlimeBattle.World
{
    public readonly struct MapPointId : IEquatable<MapPointId>
    {
        public MapPointId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(MapPointId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MapPointId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(MapPointId left, MapPointId right) => left.Equals(right);
        public static bool operator !=(MapPointId left, MapPointId right) => !left.Equals(right);
    }
}
