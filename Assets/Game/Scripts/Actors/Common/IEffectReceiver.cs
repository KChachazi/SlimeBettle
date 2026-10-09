/* 当前关卡内部解析出的状态效果接收契约。 */
namespace SlimeBattle.Actors
{
    public interface IEffectReceiver
    {
        ActorId GetActorId();
        ActorType GetActorType();
        FactionId GetFactionId();
        bool IsAlive();
        ActorSnapshot GetSnapshot();
        EffectApplyResult ApplyResolvedEffect(ResolvedEffect effect);
        bool RemoveResolvedStatus(string statusId);
    }
}
