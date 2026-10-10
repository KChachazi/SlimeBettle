using SlimeBattle.Data;

namespace SlimeBattle.Status
{
    public readonly struct StatusEntrySnapshot
    {
        public StatusEntrySnapshot(StatusId statusId, ConfigId configId,
            float remainingDurationSeconds, int areaSourceCount, bool isPermanent,
            int stackCount, float movementSpeedMultiplier, float attackSpeedMultiplier,
            float damageDealtMultiplier, float damageTakenMultiplier, bool isRooted,
            StatusControlMode controlMode)
        {
            StatusId = statusId;
            ConfigId = configId;
            RemainingDurationSeconds = remainingDurationSeconds;
            AreaSourceCount = areaSourceCount;
            IsPermanent = isPermanent;
            StackCount = stackCount;
            MovementSpeedMultiplier = movementSpeedMultiplier;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            DamageDealtMultiplier = damageDealtMultiplier;
            DamageTakenMultiplier = damageTakenMultiplier;
            IsRooted = isRooted;
            ControlMode = controlMode;
        }

        public StatusId StatusId { get; }
        public ConfigId ConfigId { get; }
        public float RemainingDurationSeconds { get; }
        public int AreaSourceCount { get; }
        public bool IsPermanent { get; }
        public int StackCount { get; }
        public float MovementSpeedMultiplier { get; }
        public float AttackSpeedMultiplier { get; }
        public float DamageDealtMultiplier { get; }
        public float DamageTakenMultiplier { get; }
        public bool IsRooted { get; }
        public StatusControlMode ControlMode { get; }
    }
}
