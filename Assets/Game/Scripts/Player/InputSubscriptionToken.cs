using System;

namespace SlimeBattle.Player.Input
{
    /// <summary>A subscription owned by one input channel, never a level event bus.</summary>
    public readonly struct InputSubscriptionToken : IEquatable<InputSubscriptionToken>
    {
        private readonly object owner;
        internal int Id { get; }

        internal InputSubscriptionToken(object owner, int id)
        {
            this.owner = owner;
            Id = id;
        }

        public bool IsValid { get { return owner != null && Id > 0; } }
        internal bool BelongsTo(object channelOwner) { return ReferenceEquals(owner, channelOwner); }
        public bool Equals(InputSubscriptionToken other) { return ReferenceEquals(owner, other.owner) && Id == other.Id; }
        public override bool Equals(object other) { return other is InputSubscriptionToken token && Equals(token); }
        public override int GetHashCode() { return (owner?.GetHashCode() ?? 0) ^ Id; }
        public static bool operator ==(InputSubscriptionToken left, InputSubscriptionToken right) { return left.Equals(right); }
        public static bool operator !=(InputSubscriptionToken left, InputSubscriptionToken right) { return !left.Equals(right); }
    }
}
