using System;

namespace SlimeBattle.World
{
    public readonly struct MapRouteId : IEquatable<MapRouteId>
    {
        public MapRouteId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(MapRouteId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is MapRouteId other && Equals(other);
        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(MapRouteId left, MapRouteId right) => left.Equals(right);
        public static bool operator !=(MapRouteId left, MapRouteId right) => !left.Equals(right);
    }
}
