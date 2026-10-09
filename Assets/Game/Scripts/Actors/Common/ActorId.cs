/* ActorId 是运行时角色实例 ID，不等同于配置 ID。 */
using System;

namespace SlimeBattle.Actors
{
    public readonly struct ActorId : IEquatable<ActorId>
    {
        public ActorId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public ActorId(long value)
        {
            Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static ActorId NewId()
        {
            return new ActorId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(ActorId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ActorId && Equals((ActorId)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(ActorId left, ActorId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ActorId left, ActorId right)
        {
            return !left.Equals(right);
        }
    }
}
