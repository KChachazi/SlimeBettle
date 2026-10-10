using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    /// <summary>Read-only status queries exposed to actor decisions and UI.</summary>
    public interface IActorStatusQuery
    {
        bool HasStatus(ActorId actorId, StatusId statusId);
        bool HasStatus(ActorId actorId, StatusKind kind);
        bool TryGetStatus(ActorId actorId, StatusId statusId,
            out StatusEntrySnapshot status);
        bool TryGetStatusSnapshot(ActorId actorId, out StatusSnapshot snapshot);
    }
}
