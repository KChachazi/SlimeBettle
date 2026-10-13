using SlimeBattle.Core;

namespace SlimeBattle.UI
{
    public sealed class FirstLevelRuntimeStatusSource : IFirstLevelStatusSource
    {
        public FirstLevelStatusSnapshot ReadSnapshot(LevelContext context)
        {
            if (context == null) return new FirstLevelStatusSnapshot("ESCORT WEAPON  --");
            var resultText = context.GetLevelState() == LevelState.Succeeded
                ? "Escort complete"
                : context.GetLevelState() == LevelState.Failed
                    ? "Escort failed" : string.Empty;
            if (context.MechanismSystem == null ||
                !context.MechanismSystem.TryGetWeaponSnapshot(out var weapon))
                return new FirstLevelStatusSnapshot("ESCORT WEAPON  --", resultText);
            var weaponText = "ESCORT WEAPON  " + weapon.Health + "/" +
                weapon.MaxHealth + "  " + weapon.TransportState +
                "  Carriers " + weapon.CarrierCount + "/4";
            return new FirstLevelStatusSnapshot(weaponText, resultText);
        }
    }
}
