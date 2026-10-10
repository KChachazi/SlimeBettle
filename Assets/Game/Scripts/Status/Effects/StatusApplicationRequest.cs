using SlimeBattle.Actors;
using SlimeBattle.World;

namespace SlimeBattle.Status
{
    public readonly struct StatusApplicationRequest
    {
        public StatusApplicationRequest(ActorId sourceActorId, ActorId targetActorId,
            StatusId statusId, float durationOverrideSeconds = -1f,
            MapAreaId sourceAreaId = default(MapAreaId),
            EffectRequestId requestId = default(EffectRequestId))
        {
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            StatusId = statusId;
            DurationOverrideSeconds = durationOverrideSeconds;
            SourceAreaId = sourceAreaId;
            RequestId = requestId.IsValid ? requestId : EffectRequestId.NewId();
        }

        public ActorId SourceActorId { get; }
        public ActorId TargetActorId { get; }
        public StatusId StatusId { get; }
        public float DurationOverrideSeconds { get; }
        public MapAreaId SourceAreaId { get; }
        public bool HasAreaSource => SourceAreaId.IsValid;
        public EffectRequestId RequestId { get; }
    }
}
