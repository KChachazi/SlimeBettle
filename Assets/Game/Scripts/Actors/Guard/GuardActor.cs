using System;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Data;
using SlimeBattle.Guards;
using UnityEngine;

namespace SlimeBattle.Actors.Guards
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class GuardActor : MonoBehaviour, IEffectReceiver
    {
        private ActorCore core;
        private GuardConfig config;
        private ActorDecisionTree decisionTree;
        private ActorStateMachine stateMachine;
        private IActorActionPort lastActions;
        private ActorVisualFeedback visualFeedback;
        private GuardVisualPresenter visualPresenter;
        private bool disposed;
        private Vector2 standPosition;

        public event Action<GuardActor, ResolvedEffect> Died;
        public ActorId ActorId => core == null ? default(ActorId) : core.GetActorId();
        public bool IsAlive => !disposed && core != null && core.IsAlive();

        public void Initialize(GuardConfig guardConfig, ActorId actorId,
            Vector2 position, FactionId factionId)
        {
            if (core != null || guardConfig == null || !actorId.IsValid)
                throw new ArgumentException("Guard initialization requires config and fresh ActorId.");
            visualFeedback = GetComponent<ActorVisualFeedback>();
            if (visualFeedback == null) visualFeedback = gameObject.AddComponent<ActorVisualFeedback>();
            visualFeedback.Initialize();
            visualPresenter = GetComponentInChildren<GuardVisualPresenter>(true);
            visualPresenter?.Initialize(this);
            transform.position = position;
            standPosition = position;
            config = guardConfig;
            core = new ActorCore(actorId, factionId, ActorType.Guard,
                config.ConfigId, position, config.MaxHealth);
            decisionTree = new ActorDecisionTree();
            decisionTree.SetRoot(new GuardDecision(config));
            stateMachine = new ActorStateMachine(config.AttackRange, config.AttackPower,
                config.AttackInterval, config.AttackWindup);
        }

        public void Tick(ActorContext context)
        {
            if (!IsAlive || context.Perception == null) return;
            lastActions = context.Actions;
            var snapshot = core.GetSnapshot();
            var intent = decisionTree.Evaluate(new DecisionContext(snapshot,
                context.Perception.GetPerception(ActorId)));
            var stateContext = new StateContext(context.DeltaTime, snapshot,
                context.Perception, context.Actions);
            if (intent.IsValid && stateMachine.SubmitIntent(intent, stateContext) ==
                StateMachineResult.Accepted && intent.Type == ActionIntentType.Attack)
                visualPresenter?.PlayAttack();
            stateMachine.Tick(stateContext);
        }

        public GuardSnapshot GetSnapshot() => new GuardSnapshot(core.GetSnapshot(),
            config.ConfigId, stateMachine.CurrentState, standPosition);

        public EffectApplyResult ApplyResolvedEffect(ResolvedEffect effect)
        {
            if (!IsAlive || (effect.Type != EffectType.Damage &&
                effect.Type != EffectType.Heal))
                return EffectApplyResult.Rejected("Guard only accepts health effects.");
            var result = core.ApplyResolvedEffect(effect);
            var deathAnimationDuration = 0f;
            if (result.DidKill)
            {
                decisionTree.Stop();
                stateMachine.RequestInterrupt(InterruptReason.Death, lastActions);
                deathAnimationDuration = visualPresenter == null
                    ? 0f : visualPresenter.PlayDeath();
            }
            else if (effect.IsDamage && result.Changed)
                visualPresenter?.PlayHit();
            visualFeedback?.NotifyEffect(effect, result, deathAnimationDuration);
            if (result.DidKill) Died?.Invoke(this, effect);
            return result;
        }

        public bool RemoveResolvedStatus(string statusId) => false;
        public void NotifyMovementResult(Vector2 position)
        {
            if (IsAlive) core.SetPosition(position);
        }
        public bool NotifyMovementCompleted(string requestId) =>
            IsAlive && stateMachine.NotifyMovementCompleted(requestId);
        public void Pause()
        {
            if (IsAlive) stateMachine.RequestInterrupt(InterruptReason.Pause, lastActions);
        }
        public void Resume()
        {
            if (IsAlive) { decisionTree.Reset(); stateMachine.Resume(); }
        }
        public void Dispose()
        {
            if (disposed) return;
            decisionTree?.Stop();
            stateMachine?.RequestInterrupt(InterruptReason.Despawn, lastActions);
            Died = null;
            disposed = true;
        }
        private void OnDestroy() { Dispose(); }

        ActorId IEffectReceiver.GetActorId() => ActorId;
        ActorType IEffectReceiver.GetActorType() => ActorType.Guard;
        FactionId IEffectReceiver.GetFactionId() => core == null
            ? default(FactionId) : core.GetFactionId();
        bool IEffectReceiver.IsAlive() => IsAlive;
        ActorSnapshot IEffectReceiver.GetSnapshot() => core == null
            ? default(ActorSnapshot) : core.GetSnapshot();
    }
}
