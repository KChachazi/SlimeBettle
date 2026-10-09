/*
 * 文件总述：配置系统使用的稳定字符串 ID。
 * ID 是配置之间的公开引用边界，不依赖 Unity 对象引用。
 */
using System;

namespace SlimeBattle.Data
{
    [Serializable]
    public readonly struct ConfigId : IEquatable<ConfigId>
    {
        public ConfigId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(ConfigId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ConfigId && Equals((ConfigId)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(ConfigId left, ConfigId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ConfigId left, ConfigId right)
        {
            return !left.Equals(right);
        }
    }
}
