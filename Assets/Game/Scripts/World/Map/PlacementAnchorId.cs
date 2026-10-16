using System;

namespace SlimeBattle.World
{
    public readonly struct PlacementAnchorId : IEquatable<PlacementAnchorId>
    {
        public PlacementAnchorId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(PlacementAnchorId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PlacementAnchorId other && Equals(other);
        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(PlacementAnchorId left, PlacementAnchorId right) =>
            left.Equals(right);
        public static bool operator !=(PlacementAnchorId left, PlacementAnchorId right) =>
            !left.Equals(right);
    }
}
