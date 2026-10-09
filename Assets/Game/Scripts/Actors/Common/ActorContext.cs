/* ActorContext 只携带一次 Tick 的时间与当前关卡公开请求端口。 */
using SlimeBattle.Actors.AI.StateMachine;

namespace SlimeBattle.Actors
{
    public readonly struct ActorContext
    {
        public ActorContext(float deltaTime, IActorPerceptionSource perception,
            IActorActionPort actions)
        {
            DeltaTime = deltaTime;
            Perception = perception;
            Actions = actions;
        }
        public float DeltaTime { get; }
        public IActorPerceptionSource Perception { get; }
        public IActorActionPort Actions { get; }
    }
}
