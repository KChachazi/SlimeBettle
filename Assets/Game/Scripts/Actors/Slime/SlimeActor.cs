/* 正式史莱姆预制体组件：个体持有生命与 AI，由 SlimeManager 实例化和驱动。 */
using System;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors.SlimeGroups;
using SlimeBattle.Data;
using SlimeBattle.Slimes;
using UnityEngine;

namespace SlimeBattle.Actors.Slimes
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SlimeActor : MonoBehaviour, IEffectReceiver, IActorControlReceiver
    {
        private ActorCore core;
        private SlimeConfig config;
        private SlimeRuntimeData runtime;
        private ActorDecisionTree decisionTree;
        private ActorStateMachine stateMachine;
        private IActorActionPort lastActions;
        private bool disposed;

        public event Action<SlimeActor, ResolvedEffect> Died;
        public event Action<SlimeActor> ControlModeChanged;
        public ActorId ActorId => core == null ? default(ActorId) : core.GetActorId();
        public bool IsAlive => !disposed && core != null && core.IsAlive();
        public string LastDecisionReason { get; private set; } = string.Empty;
        public ActorId GetActorId() => ActorId;
        public ActorType GetActorType() => ActorType.Slime;
        public FactionId GetFactionId() => core == null ? default(FactionId) : core.GetFactionId();
        bool IEffectReceiver.IsAlive() => IsAlive;
        ActorSnapshot IEffectReceiver.GetSnapshot() => core == null
            ? default(ActorSnapshot) : core.GetSnapshot();

        public void Initialize(SlimeConfig slimeConfig, ActorId actorId, Vector2 position,
            FactionId factionId)
        {
            if (core != null || slimeConfig == null || !actorId.IsValid)
                throw new ArgumentException("Slime initialization requires config and fresh ActorId.");
            transform.position = position;
            config = slimeConfig;
            core = new ActorCore(actorId, factionId, ActorType.Slime,
                config.ConfigId, position, config.MaxHealth);
            runtime = new SlimeRuntimeData();
            decisionTree = new ActorDecisionTree();
            decisionTree.SetRoot(new BasicSlimeDecision(runtime, config.AttackRange, config.ConfigId));
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
            var stateContext = new StateContext(context.DeltaTime, snapshot,
                context.Perception, context.Actions);
            if (intent.IsValid) stateMachine.SubmitIntent(intent, stateContext);
            stateMachine.Tick(stateContext);
        }

        public SlimeSnapshot GetSnapshot()
        {
            var snapshot = core.GetSnapshot();
            // NeedState 只是单值展示，不复制保存状态；完整并存状态看 StatusIds。
            var need = GetPrimaryNeed(snapshot);
            return new SlimeSnapshot(snapshot, config.ConfigId,
                snapshot.CurrentHealth, snapshot.MaxHealth,
                (SlimeControlMode)snapshot.ControlMode, need);
        }

        private static SlimeNeedState GetPrimaryNeed(ActorSnapshot snapshot)
        {
            var statuses = snapshot.StatusIds;
            if (SlimeStatusIds.Contains(statuses, SlimeStatusIds.Afraid)) return SlimeNeedState.Afraid;
            if (SlimeStatusIds.Contains(statuses, SlimeStatusIds.Hungry)) return SlimeNeedState.Hungry;
            if (SlimeStatusIds.Contains(statuses, SlimeStatusIds.Cold)) return SlimeNeedState.Cold;
            if (SlimeStatusIds.Contains(statuses, SlimeStatusIds.Agitated)) return SlimeNeedState.Agitated;
            if (SlimeStatusIds.Contains(statuses, SlimeStatusIds.Hot)) return SlimeNeedState.Hot;
            return SlimeNeedState.None;
        }

        public EffectApplyResult ApplyResolvedEffect(ResolvedEffect effect)
        {
            if (disposed || core == null) return EffectApplyResult.Rejected("Slime is unavailable.");
            var result = core.ApplyResolvedEffect(effect);
            if (result.DidKill)
            {
                decisionTree.Stop();
                stateMachine.RequestInterrupt(InterruptReason.Death, lastActions);
                Died?.Invoke(this, effect);
            }
            return result;
        }

        public bool RemoveResolvedStatus(string statusId)
        {
            return IsAlive && core.RemoveResolvedStatus(statusId);
        }

        public void SetControlMode(ActorControlMode mode)
        {
            SetControlMode((SlimeControlMode)mode, lastActions);
        }

        public void SetControlMode(SlimeControlMode mode, IActorActionPort actions = null)
        {
            if (!IsAlive || mode == SlimeControlMode.Dead) return;
            if (core.RuntimeData.ControlMode == (ActorControlMode)mode) return;
            core.SetControlMode((ActorControlMode)mode);
            if (mode == SlimeControlMode.Autonomous)
            {
                decisionTree.Reset();
                stateMachine.Resume();
            }
            else
            {
                decisionTree.Stop();
                stateMachine.RequestInterrupt(mode == SlimeControlMode.Paused
                    ? InterruptReason.Pause : InterruptReason.Control, actions);
            }
            ControlModeChanged?.Invoke(this);
        }

        public void NotifyGroupContext(SlimeGroupSnapshot snapshot)
        {
            runtime.GroupId = snapshot.GroupId;
            runtime.GroupTarget = snapshot.Target;
        }

        public void ClearGroupContext()
        {
            runtime.GroupId = default(GroupId);
            runtime.GroupTarget = default(Vector2);
        }

        public void NotifyMovementResult(Vector2 position) { if (IsAlive) core.SetPosition(position); }
        public bool NotifyMovementCompleted(string requestId)
            => IsAlive && stateMachine.NotifyMovementCompleted(requestId);

        public void Pause()
        {
            if (IsAlive) { decisionTree.Stop(); stateMachine.RequestInterrupt(InterruptReason.Pause, lastActions); }
        }

        public void Resume()
        {
            if (IsAlive && core.RuntimeData.ControlMode == ActorControlMode.Autonomous)
            { decisionTree.Reset(); stateMachine.Resume(); }
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
