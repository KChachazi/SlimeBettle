/* WeaponRuntimeData 保存目标武器的当前生命值和位置。 */
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Objectives
{
    public sealed class WeaponRuntimeData
    {
        internal WeaponRuntimeData(
            WeaponId weaponId,
            ConfigId configId,
            float maxHealth,
            Vector2 position)
        {
            WeaponId = weaponId;
            ConfigId = configId;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            Position = position;
        }

        public WeaponId WeaponId { get; }
        public ConfigId ConfigId { get; }
        public float MaxHealth { get; }
        public float CurrentHealth { get; internal set; }
        public Vector2 Position { get; internal set; }
        public bool IsDestroyed => CurrentHealth <= 0f;

        public WeaponSnapshot GetSnapshot()
        {
            return new WeaponSnapshot(
                WeaponId,
                ConfigId,
                MaxHealth,
                CurrentHealth,
                Position,
                IsDestroyed);
        }
    }
}
