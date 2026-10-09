/* 群体实例 ID 与成员 ActorId 分离，解散后不复用。 */
using System;

namespace SlimeBattle.Actors.SlimeGroups
{
    public readonly struct GroupId : IEquatable<GroupId>
    {
        public GroupId(string value) { Value = value ?? string.Empty; }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        public static GroupId NewId() => new GroupId(Guid.NewGuid().ToString("N"));
        public bool Equals(GroupId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GroupId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public static bool operator ==(GroupId a, GroupId b) => a.Equals(b);
        public static bool operator !=(GroupId a, GroupId b) => !a.Equals(b);
    }
}
