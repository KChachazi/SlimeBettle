/* 决策树只读取 Actor 和感知快照并返回一个行动意图。 */
using System;
using SlimeBattle.Status;

namespace SlimeBattle.Actors.AI.DecisionTree
{
    public readonly struct DecisionContext
    {
        public DecisionContext(ActorSnapshot actor, PerceptionSnapshot perception,
            IActorStatusQuery statusQuery = null)
        {
            Actor = actor;
            Perception = perception;
            StatusQuery = statusQuery;
        }

        public ActorSnapshot Actor { get; }
        public PerceptionSnapshot Perception { get; }
        public IActorStatusQuery StatusQuery { get; }
    }

    public abstract class DecisionNode
    {
        public abstract ActionIntent Evaluate(DecisionContext context);
    }

    public sealed class ActorDecisionTree
    {
        private DecisionNode root;
        private bool stopped;

        public bool IsStopped => stopped;

        public void SetRoot(DecisionNode node) { root = node; }

        public ActionIntent Evaluate(DecisionContext context)
        {
            return stopped || !context.Actor.IsAlive || root == null
                ? default(ActionIntent)
                : root.Evaluate(context);
        }

        public void Reset() { stopped = false; }
        public void Stop() { stopped = true; }
    }
}
