namespace SlimeBattle.Mechanisms
{
    public enum MechanismKind
    {
        None = 0,
        Food = 1,
        Furnace = 2,
        Obstacle = 3
    }

    public enum MechanismState
    {
        Available = 0,
        Cooldown = 1,
        Exhausted = 2,
        Removed = 3
    }

    public enum GuideSourceType
    {
        None = 0,
        Food = 1,
        Warmth = 2
    }

    public enum MechanismActivationKind
    {
        Contact = 0,
        Manual = 1,
        Timed = 2
    }

    public enum MechanismContactKind
    {
        Entered = 0,
        Exited = 1
    }
}
