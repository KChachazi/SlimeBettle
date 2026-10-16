using SlimeBattle.Core;
using UnityEngine;

namespace SlimeBattle.UI
{
    /// <summary>Inspector-compatible adapter. New goal/window facts await the Core public API.</summary>
    public sealed class FirstLevelStatusProvider : MonoBehaviour, IFirstLevelStatusSource
    {
        [SerializeField] private bool showLegacyWeapon;
        private readonly FirstLevelRuntimeStatusSource legacy = new FirstLevelRuntimeStatusSource();
        public FirstLevelStatusSnapshot ReadSnapshot(LevelContext context)
        {
            if (showLegacyWeapon) { return legacy.ReadSnapshot(context); }
            // Never interpret weapon health or local slime counts as first-level results.
            return new FirstLevelStatusSnapshot(string.Empty);
        }
    }
}
