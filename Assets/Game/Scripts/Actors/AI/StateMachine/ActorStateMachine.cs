/* 单个 Actor 的行动状态机：消费意图、执行一次动作、处理暂停与打断。 */
using SlimeBattle.Actors.AI.DecisionTree;

namespace SlimeBattle.Actors.AI.StateMachine
{
    public enum StateId { Idle, Moving, Attacking, Controlled, Dead }
    public enum InterruptReason { Pause, Control, Knockback, Death, Despawn, Reset }
    public enum StateMachineResult { Accepted, Rejected, Interrupted }

    public readonly struct StateContext
    {
        public StateContext(float deltaTime, ActorSnapshot actor,
            IActorPerceptionSource perception, IActorActionPort actions)
        {
            DeltaTime = deltaTime;
            Actor = actor;
            Perception = perception;
            Actions = actions;
        }
        public float DeltaTime { get; }
        public ActorSnapshot Actor { get; }
        public IActorPerceptionSource Perception { get; }
        public IActorActionPort Actions { get; }
    }

    public sealed class ActorStateMachine
    {
        private readonly AttackAction attackAction = new AttackAction();
        private readonly float attackRange;
        private readonly float attackPower;
        private readonly float attackInterval;
        private readonly float attackWindup;
        private ActionIntent currentIntent;
        private string movementRequestId;
        private float cooldown;
        private float attackElapsed;
        private bool attackPending;
        private bool paused;
        private bool stopped;

        public ActorStateMachine(float attackRange, float attackPower, float attackInterval,
            float attackWindup = 0.15f)
        {
            this.attackRange = attackRange;
            this.attackPower = attackPower;
            this.attackInterval = attackInterval;
            this.attackWindup = attackWindup;
        }

        public StateId GetCurrentStateId() { return CurrentState; }
        public StateId CurrentState { get; private set; } = StateId.Idle;
        public bool IsStopped => stopped;
        public bool IsPaused => paused;

        public StateMachineResult SubmitIntent(ActionIntent intent, StateContext context)
        {
            if (stopped || paused || !context.Actor.IsAlive || !intent.IsValid ||
                context.Actor.ControlMode != ActorControlMode.Autonomous ||
                (intent.Type == ActionIntentType.Move && context.Actions == null) ||
                (intent.Type == ActionIntentType.Attack &&
                    (context.Actions == null || context.Perception == null)) ||
                (intent.Type == ActionIntentType.Attack && (attackPending || cooldown > 0f)))
            {
                return StateMachineResult.Rejected;
            }

            // 已在目的地时不再提交一笔没有实际位移的请求。
            if (intent.Type == ActionIntentType.Move &&
                UnityEngine.Vector2.Distance(context.Actor.Position, intent.Destination) <= .05f)
            {
                CancelMovement(context.Actions);
                currentIntent = default(ActionIntent);
                CurrentState = StateId.Idle;
                return StateMachineResult.Accepted;
            }

            if (CurrentState == StateId.Moving && intent.Type == ActionIntentType.Move &&
                currentIntent.Destination == intent.Destination)
            {
                return StateMachineResult.Accepted;
            }

            CancelMovement(context.Actions);
            currentIntent = intent;
            switch (intent.Type)
            {
                case ActionIntentType.Move:
                    movementRequestId = context.Actions.SubmitMovement(
                        new ActorMovementRequest(context.Actor.ActorId, intent.Destination));
                    CurrentState = StateId.Moving;
                    break;
                case ActionIntentType.Attack:
                    attackPending = true;
                    attackElapsed = 0f;
                    CurrentState = StateId.Attacking;
                    break;
                default:
                    CurrentState = StateId.Idle;
                    break;
            }
            return StateMachineResult.Accepted;
        }

        public bool NotifyMovementCompleted(string requestId)
        {
            // 只接受当前请求；迟到的旧关卡或被打断请求不能结束新动作。
            if (stopped || paused || CurrentState != StateId.Moving ||
                string.IsNullOrEmpty(requestId) || requestId != movementRequestId)
                return false;
            movementRequestId = null;
            currentIntent = default(ActionIntent);
            CurrentState = StateId.Idle;
            return true;
        }

        public void Tick(StateContext context)
        {
            if (stopped || paused) return;
            if (!context.Actor.IsAlive)
            {
                RequestInterrupt(InterruptReason.Death, context.Actions);
                return;
            }
            if (context.DeltaTime > 0f) cooldown = UnityEngine.Mathf.Max(0f, cooldown - context.DeltaTime);
            if (!attackPending) return;
            attackElapsed += UnityEngine.Mathf.Max(0f, context.DeltaTime);
            if (attackElapsed < attackWindup) return;

            // 命中与提交发生在进入 Attacking 之后的 Tick；目标可能已失效。
            attackPending = false;
            attackElapsed = 0f;
            attackAction.ExecuteHit(currentIntent.Attack, context.Actor,
                attackRange, attackPower, context.Perception, context.Actions);
            // ApplyEffect 可能同步触发死亡或关卡销毁，不能覆盖中断后的 Dead 状态。
            if (stopped || paused) return;
            cooldown = attackInterval;
            CurrentState = StateId.Idle;
            currentIntent = default(ActionIntent);
        }

        public StateMachineResult RequestInterrupt(InterruptReason reason, IActorActionPort actions)
        {
            if (stopped) return StateMachineResult.Rejected;
            CancelMovement(actions);
            attackPending = false;
            attackElapsed = 0f;
            currentIntent = default(ActionIntent);
            paused = reason == InterruptReason.Pause;
            CurrentState = reason == InterruptReason.Death ? StateId.Dead
                : reason == InterruptReason.Control ? StateId.Controlled : StateId.Idle;
            if (reason == InterruptReason.Death || reason == InterruptReason.Despawn) stopped = true;
            return StateMachineResult.Interrupted;
        }

        public void Resume()
        {
            if (!stopped && paused) { paused = false; CurrentState = StateId.Idle; }
        }

        public void Reset(IActorActionPort actions)
        {
            if (stopped) return;
            RequestInterrupt(InterruptReason.Reset, actions);
            cooldown = 0f;
        }

        private void CancelMovement(IActorActionPort actions)
        {
            if (!string.IsNullOrEmpty(movementRequestId)) actions?.CancelMovement(movementRequestId);
            movementRequestId = null;
        }
    }
}
