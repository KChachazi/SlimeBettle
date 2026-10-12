/* Actor 行动层的输出边界：适配器将请求转交 MovementSystem / StatusManager 的公开入口。 */
using SlimeBattle.Data;
using SlimeBattle.Status;
using UnityEngine;

namespace SlimeBattle.Actors.AI.StateMachine
{
    public readonly struct ActorMovementRequest
    {
        public ActorMovementRequest(ActorId actorId, Vector2 destination)
        {
            ActorId = actorId;
            Destination = destination;
        }
        public ActorId ActorId { get; }
        public Vector2 Destination { get; }
    }

    public readonly struct ActorEffectRequest
    {
        public ActorEffectRequest(ActorId sourceActorId, ActorId targetActorId,
            ConfigId abilityId, float baseAmount, EffectRequestId requestId = default(EffectRequestId))
        {
            RequestId = requestId.IsValid ? requestId : EffectRequestId.NewId();
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            AbilityId = abilityId;
            BaseAmount = baseAmount;
        }
        public EffectRequestId RequestId { get; }
        public ActorId SourceActorId { get; }
        public ActorId TargetActorId { get; }
        public ConfigId AbilityId { get; }
        public float BaseAmount { get; }
    }

    public interface IActorActionPort
    {
        // 返回空字符串表示移动系统未接受请求；有效 ID 由适配器映射到 MovementRequestId。
        string SubmitMovement(ActorMovementRequest request);
        void CancelMovement(string requestId);
        void ApplyEffect(ActorEffectRequest request);
    }

    public interface IActorStatusRequestPort
    {
        bool ApplyStatus(ActorId actorId, StatusId statusId, float durationSeconds = -1f);
    }

    public interface IActorPerceptionSource
    {
        DecisionTree.PerceptionSnapshot GetPerception(ActorId actorId);
        bool TryGetActorSnapshot(ActorId actorId, out ActorSnapshot snapshot);
    }
}
