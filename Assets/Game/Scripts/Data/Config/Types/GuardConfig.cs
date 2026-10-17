using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Guard", fileName = "CFG_Guard")]
    public sealed class GuardConfig : ActorConfig
    {
        [SerializeField, Min(1)] private int initialCount = 2;
        [SerializeField, Min(0f)] private float escortDistance = 1.2f;
        [SerializeField, Min(0f)] private float leashDistance = 8f;
        [SerializeField] private Vector2[] spawnOffsets =
            { new Vector2(-0.8f, 0f), new Vector2(0.8f, 0f) };

        public int InitialCount => initialCount;
        public float EscortDistance => escortDistance;
        public float LeashDistance => leashDistance;
        public System.Collections.Generic.IReadOnlyList<Vector2> SpawnOffsets =>
            spawnOffsets == null ? System.Array.Empty<Vector2>() :
            System.Array.AsReadOnly(spawnOffsets);

        public void ConfigureEscort(int count, float distance)
        {
            EnsureAuthoring();
            initialCount = count;
            escortDistance = distance;
        }

        public void SetSpawnOffsets(Vector2[] offsets)
        {
            EnsureAuthoring();
            spawnOffsets = offsets == null ? null : (Vector2[])offsets.Clone();
        }

        protected override void ValidateSpecific(ConfigValidationReport report)
        {
            base.ValidateSpecific(report);
            if (initialCount < 1)
                report.AddError($"{name}: InitialCount must be positive.");
            if (float.IsNaN(escortDistance) || float.IsInfinity(escortDistance) ||
                escortDistance < 0f)
                report.AddError($"{name}: EscortDistance must be finite and non-negative.");
            if (float.IsNaN(leashDistance) || float.IsInfinity(leashDistance) ||
                leashDistance <= 0f)
                report.AddError($"{name}: LeashDistance must be finite and positive.");
            if (spawnOffsets == null || spawnOffsets.Length != initialCount)
                report.AddError($"{name}: SpawnOffsets must match InitialCount.");
            else
                foreach (var offset in spawnOffsets)
                    if (float.IsNaN(offset.x) || float.IsInfinity(offset.x) ||
                        float.IsNaN(offset.y) || float.IsInfinity(offset.y))
                        report.AddError($"{name}: SpawnOffsets must be finite.");
        }
    }
}
