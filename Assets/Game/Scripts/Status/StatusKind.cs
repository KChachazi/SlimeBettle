namespace SlimeBattle.Status
{
    /// <summary>Identifies the gameplay rule implemented by a StatusConfig.</summary>
    public enum StatusKind
    {
        None = 0,
        Hunger = 1,
        Rage = 2,
        Fear = 3,
        Cold = 4,
        Frozen = 5,
        Hot = 6,
        Burning = 7
    }

    public enum PeriodicAmountMode
    {
        Flat = 0,
        MaxHealthFraction = 1
    }
}
