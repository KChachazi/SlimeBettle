namespace SlimeBattle.Actors
{
    /// <summary>Lets the status service apply temporary maximum-health modifiers.</summary>
    public interface IActorHealthLimitReceiver
    {
        void SetMaxHealthMultiplier(float multiplier);
    }
}
