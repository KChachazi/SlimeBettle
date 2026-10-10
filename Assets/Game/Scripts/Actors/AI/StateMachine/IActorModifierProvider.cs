namespace SlimeBattle.Actors.AI.StateMachine
{
    public interface IActorModifierProvider
    {
        float GetAttackSpeedMultiplier(ActorId actorId);
    }
}
