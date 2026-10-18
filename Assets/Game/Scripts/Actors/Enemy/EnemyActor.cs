/* 正式敌人预制体组件：个体持有索敌 AI 与状态机，生命周期由 EnemyManager 管理。 */
using System;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using UnityEngine;

namespace SlimeBattle.Actors.Enemies
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnemyActor : MonoBehaviour, IEffectReceiver, IActorControlReceiver,
        IActorHealthLimitReceiver
    {
        private ActorCore core;
        private EnemyConfig config;
        private EnemyRuntimeData runtime;
        private ActorDecisionTree decisionTree;
        private ActorStateMachine stateMachine;
        private IActorActionPort lastActions;
        private ActorVisualFeedback visualFeedback;
        private bool disposed;

        public event Action<EnemyActor, ResolvedEffect> Died;
        public event Action<EnemyActor> ControlModeChanged;
        public ActorId ActorId => core == null ? default(ActorId) : core.GetActorId();
        public bool IsAlive => !disposed && core != null && core.IsAlive();
        public string LastDecisionReason { get; private set; } = string.Empty;
        public ActorId GetActorId() => ActorId;
        public ActorType GetActorType() => ActorType.Enemy;
        public FactionId GetFactionId() => core == null ? default(FactionId) : core.GetFactionId();
        bool IEffectReceiver.IsAlive() => IsAlive;
        ActorSnapshot IEffectReceiver.GetSnapshot() => core == null
            ? default(ActorSnapshot) : core.GetSnapshot();

        public void Initialize(EnemyConfig enemyConfig, ActorId actorId, Vector2 position,
            FactionId factionId, EnemyTargetPolicy targetPolicy,
            EnemyActivityBounds activityBounds)
        {
            if (core != null || enemyConfig == null || !actorId.IsValid)
                throw new ArgumentException("Enemy initialization requires config and fresh ActorId.");
            visualFeedback = GetComponent<ActorVisualFeedback>();
            if (visualFeedback == null) visualFeedback = gameObject.AddComponent<ActorVisualFeedback>();
            visualFeedback.Initialize();
            transform.position = position;
            config = enemyConfig;
            core = new ActorCore(actorId, factionId, ActorType.Enemy,
                config.ConfigId, position, config.MaxHealth);
            runtime = new EnemyRuntimeData { TargetPolicy = targetPolicy,
                ActivityBounds = activityBounds };
            decisionTree = new ActorDecisionTree();
            decisionTree.SetRoot(new BasicEnemyDecision(runtime, config.AttackRange, config.ConfigId));
            stateMachine = new ActorStateMachine(config.AttackRange, config.AttackPower,
                config.AttackInterval, config.AttackWindup);
        }

        public void Tick(ActorContext context)
        {
            if (!IsAlive || context.Perception == null) return;
            lastActions = context.Actions;
            var snapshot = core.GetSnapshot();
            var perception = context.Perception.GetPerception(ActorId);
            var intent = decisionTree.Evaluate(new DecisionContext(snapshot, perception));
            LastDecisionReason = intent.Reason;
            var modifierProvider = context.Actions as IActorModifierProvider;
            var attackSpeedMultiplier = modifierProvider == null ? 1f
                : modifierProvider.GetAttackSpeedMultiplier(ActorId);
            var stateContext = new StateContext(context.DeltaTime, snapshot,
                context.Perception, context.Actions, attackSpeedMultiplier);
            if (intent.IsValid) stateMachine.SubmitIntent(intent, stateContext);
            stateMachine.Tick(stateContext);
        }

        public EnemySnapshot GetSnapshot() => new EnemySnapshot(core.GetSnapshot(),
            config.ConfigId, core.RuntimeData.CurrentHealth, core.RuntimeData.MaxHealth,
            (EnemyControlMode)core.RuntimeData.ControlMode, runtime.TargetPolicy,
            runtime.ActivityBounds, runtime.TargetId, stateMachine.CurrentState,
            runtime.IsAttacking);

        public EffectApplyResult ApplyResolvedEffect(ResolvedEffect effect)
        {
            if (disposed || core == null) return EffectApplyResult.Rejected("Enemy is unavailable.");
            var result = core.ApplyResolvedEffect(effect);
            if (result.DidKill)
            {
                runtime.IsAttacking = false;
                decisionTree.Stop();
                stateMachine.RequestInterrupt(InterruptReason.Death, lastActions);
            }
            visualFeedback?.NotifyEffect(effect, result);
            if (result.DidKill) Died?.Invoke(this, effect);
            return result;
        }

        public bool RemoveResolvedStatus(string statusId)
        {
            return !disposed && core != null && core.RemoveResolvedStatus(statusId);
        }

        public void SetMaxHealthMultiplier(float multiplier)
        {
            if (core != null && IsAlive) core.SetMaxHealthMultiplier(multiplier);
        }

        public void SetControlMode(ActorControlMode mode)
        {
            if (!IsAlive || mode == ActorControlMode.Dead) return;
            if (core.RuntimeData.ControlMode == mode) return;
            core.SetControlMode(mode);
            if (mode == ActorControlMode.Autonomous)
            { decisionTree.Reset(); stateMachine.Resume(); }
            else
            {
                runtime.IsAttacking = false;
                decisionTree.Stop();
                stateMachine.RequestInterrupt(mode == ActorControlMode.Paused
                    ? InterruptReason.Pause : InterruptReason.Control, lastActions);
            }
            ControlModeChanged?.Invoke(this);
        }

        public void SetActivityBounds(EnemyActivityBounds bounds) { runtime.ActivityBounds = bounds; }
        public void NotifyMovementResult(Vector2 position) { if (IsAlive) core.SetPosition(position); }
        public bool NotifyMovementCompleted(string requestId)
            => IsAlive && stateMachine.NotifyMovementCompleted(requestId);

        public void Pause()
        {
            if (IsAlive)
            {
                runtime.IsAttacking = false;
                decisionTree.Stop();
                stateMachine.RequestInterrupt(InterruptReason.Pause, lastActions);
            }
        }

        public void Resume()
        {
            if (IsAlive) { decisionTree.Reset(); stateMachine.Resume(); }
        }

        public void Dispose(IActorActionPort actions = null)
        {
            if (disposed) return;
            decisionTree?.Stop();
            stateMachine?.RequestInterrupt(InterruptReason.Despawn, actions ?? lastActions);
            Died = null;
            ControlModeChanged = null;
            disposed = true;
        }

        private void OnDestroy() { Dispose(); }
    }
}
