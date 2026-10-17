using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using UnityEngine;

namespace SlimeBattle.UI
{
    public sealed class FirstLevelRuntimeStatusSource : IFirstLevelStatusSource
    {
        private readonly bool showLegacyWeapon;

        public FirstLevelRuntimeStatusSource(bool showLegacyWeapon = false)
        {
            this.showLegacyWeapon = showLegacyWeapon;
        }

        public FirstLevelStatusSnapshot ReadSnapshot(LevelContext context)
        {
            if (context == null) return new FirstLevelStatusSnapshot(string.Empty);
            var isEscort = context.LevelConfig != null &&
                context.LevelConfig.ObjectiveKind == Data.LevelObjectiveKind.EscortWeapon;
            var resultText = context.GetLevelState() == LevelState.Succeeded
                ? "Level complete"
                : context.GetLevelState() == LevelState.Failed
                    ? "Level failed" : string.Empty;
            var rules = context.ResultRules;
            var weaponText = string.Empty;
            EscortWeaponSnapshot weapon = default;
            var hasWeapon = context.MechanismSystem != null &&
                context.MechanismSystem.TryGetWeaponSnapshot(out weapon);
            if ((isEscort || showLegacyWeapon) && hasWeapon)
                weaponText = "ESCORT WEAPON  " + weapon.Health + "/" +
                    weapon.MaxHealth + "  " + weapon.TransportState +
                    "  Carriers " + weapon.CarrierCount + "/4";
            var goalText = string.Empty;
            var waitingText = string.Empty;
            if (isEscort)
            {
                goalText = hasWeapon && weapon.HasReachedGoal
                    ? "WEAPON  ARRIVED"
                    : hasWeapon ? "WEAPON  ESCORTING" : "WEAPON  NOT CONFIGURED";
            }
            else
            {
                goalText = rules == null ? string.Empty :
                    "CAVE  " + rules.GoalOccupantCount + "/" + rules.RequiredCount;
                waitingText = rules != null && rules.IsWaiting ?
                    "WAIT  " + Mathf.CeilToInt(rules.RemainingSeconds) + "s" : string.Empty;
            }
            return new FirstLevelStatusSnapshot(weaponText, resultText,
                goalText, waitingText);
        }
    }
}
