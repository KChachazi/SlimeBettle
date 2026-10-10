using SlimeBattle.Actors;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Status
{
    public readonly struct EffectRequest
    {
        public EffectRequest(EffectRequestId requestId, ActorId sourceActorId,
            ActorId targetActorId, EffectType type, float amount = 0f,
            StatusId statusId = default(StatusId), ConfigId contextConfigId = default(ConfigId),
            EffectRelationshipPolicy relationshipPolicy = EffectRelationshipPolicy.Any,
            bool requireLivingSource = true, float durationSeconds = -1f,
            Vector2 direction = default(Vector2), long requestRound = 0)
        {
            RequestId = requestId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            Type = type;
            Amount = amount;
            StatusId = statusId;
            ContextConfigId = contextConfigId;
            RelationshipPolicy = relationshipPolicy;
            RequireLivingSource = requireLivingSource;
            DurationSeconds = durationSeconds;
            Direction = direction;
            RequestRound = requestRound;
        }

        public EffectRequestId RequestId { get; }
        public ActorId SourceActorId { get; }
        public ActorId TargetActorId { get; }
        public EffectType Type { get; }
        public float Amount { get; }
        public StatusId StatusId { get; }
        public ConfigId ContextConfigId { get; }
        public EffectRelationshipPolicy RelationshipPolicy { get; }
        public bool RequireLivingSource { get; }
        public float DurationSeconds { get; }
        public Vector2 Direction { get; }
        public long RequestRound { get; }
    }
}
