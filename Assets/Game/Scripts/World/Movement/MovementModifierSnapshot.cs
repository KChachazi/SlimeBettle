namespace SlimeBattle.World.Movement
{
    public readonly struct MovementModifierSnapshot
    {
        public MovementModifierSnapshot(float speedMultiplier, bool isRooted)
        {
            SpeedMultiplier = speedMultiplier;
            IsRooted = isRooted;
        }

        public float SpeedMultiplier { get; }
        public bool IsRooted { get; }

        public static MovementModifierSnapshot Neutral =>
            new MovementModifierSnapshot(1f, false);
    }
}
