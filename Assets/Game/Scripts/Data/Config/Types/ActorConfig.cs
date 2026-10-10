/* ActorConfig 是 SlimeConfig 和 EnemyConfig 共用的基础属性配置。 */
using UnityEngine;

namespace SlimeBattle.Data
{
    public class ActorConfig : ConfigAsset
    {
        [SerializeField]
        private int attackPower = 1;

        [SerializeField]
        private int maxHealth = 1;

        [SerializeField]
        private float moveSpeed = 1f;

        [SerializeField]
        private float attackInterval = 1f;

        [SerializeField]
        private float attackRange = 1f;

        [SerializeField]
        private float detectionRange = 1.5f;

        [SerializeField]
        private float attackWindup = 0.15f;

        public int AttackPower => attackPower;
        public int MaxHealth => maxHealth;
        /// <summary>Pixels per second in the assigned Sprite's source pixels.</summary>
        public float MoveSpeed => moveSpeed;
        public float AttackInterval => attackInterval;
        public float AttackRange => attackRange;
        /// <summary>Temporary target detection radius in world units; tune when design values arrive.</summary>
        public float DetectionRange => detectionRange;
        public float AttackWindup => attackWindup;

        // Authoring helper used by tests and editor-side setup.
        public void ConfigureStats(
            int newAttackPower,
            int newMaxHealth,
            float newMoveSpeed,
            float newAttackInterval)
        {
            EnsureAuthoring();
            attackPower = newAttackPower;
            maxHealth = newMaxHealth;
            moveSpeed = newMoveSpeed;
            attackInterval = newAttackInterval;
        }

        public void SetAttackRange(float value)
        {
            EnsureAuthoring();
            attackRange = value;
        }

        public void SetDetectionRange(float value)
        {
            EnsureAuthoring();
            detectionRange = value;
        }

        public void SetAttackWindup(float value)
        {
            EnsureAuthoring();
            attackWindup = value;
        }

        protected override void ValidateSpecific(ConfigValidationReport report)
        {
            if (attackPower < 0)
            {
                report.AddError($"{name}: AttackPower must be non-negative.");
            }

            if (maxHealth <= 0)
            {
                report.AddError($"{name}: MaxHealth must be greater than zero.");
            }

            if (float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed) || moveSpeed < 0f)
            {
                report.AddError($"{name}: MoveSpeed must be non-negative.");
            }

            if (float.IsNaN(attackInterval) || float.IsInfinity(attackInterval) || attackInterval <= 0f)
            {
                report.AddError($"{name}: AttackInterval must be greater than zero.");
            }

            if (float.IsNaN(attackRange) || float.IsInfinity(attackRange) || attackRange <= 0f)
            {
                report.AddError($"{name}: AttackRange must be greater than zero.");
            }
            if (float.IsNaN(detectionRange) || float.IsInfinity(detectionRange) ||
                detectionRange <= attackRange)
            {
                report.AddError($"{name}: DetectionRange must be greater than AttackRange.");
            }
            if (float.IsNaN(attackWindup) || float.IsInfinity(attackWindup) || attackWindup < 0f)
            {
                report.AddError($"{name}: AttackWindup must be non-negative.");
            }
        }
    }
}
