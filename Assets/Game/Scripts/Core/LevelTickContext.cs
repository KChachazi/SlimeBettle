/* LevelTickContext 为当前关卡的一次规则更新提供时间和 Actor 外部端口。 */
using SlimeBattle.Actors.AI.StateMachine;

namespace SlimeBattle.Core
{
    public readonly struct LevelTickContext
    {
        public LevelTickContext(float deltaTime, IActorPerceptionSource perception,
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
