using SlimeBattle.Core;
using UnityEngine;

namespace SlimeBattle.UI
{
    /// <summary>Inspector-compatible adapter for the current level's result facts.</summary>
    public sealed class FirstLevelStatusProvider : MonoBehaviour, IFirstLevelStatusSource
    {
        [SerializeField] private bool showLegacyWeapon;
        private readonly FirstLevelRuntimeStatusSource current = new FirstLevelRuntimeStatusSource();
        private readonly FirstLevelRuntimeStatusSource legacy = new FirstLevelRuntimeStatusSource(true);
        public FirstLevelStatusSnapshot ReadSnapshot(LevelContext context)
        {
            return showLegacyWeapon ? legacy.ReadSnapshot(context) : current.ReadSnapshot(context);
        }
    }
}
