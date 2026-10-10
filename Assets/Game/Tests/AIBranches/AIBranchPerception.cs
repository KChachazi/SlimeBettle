/* 测试夹具只补食物、热源、逃跑判定和暂缺的 Guard；敌我目标仍由正式感知计算。 */
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using UnityEngine;

namespace SlimeBattle.Tests.AIBranches
{
    internal sealed class AIBranchPerception : IActorPerceptionSource
    {
        private readonly ActorTargetPerceptionSource targets;
        private readonly AIBranchScenario scenario;
        private readonly ActorId slimeId;
        private readonly ActorId humanId = ActorId.NewId();
        private readonly Vector2 humanPosition = new Vector2(.65f, 0f);
        private long sampleTick;

        public AIBranchPerception(ActorTargetPerceptionSource targets,
            AIBranchScenario scenario, ActorId slimeId)
        {
            this.targets = targets;
            this.scenario = scenario;
            this.slimeId = slimeId;
        }

        public Vector2 HumanPosition => humanPosition;
        public PerceptionSnapshot GetPerception(ActorId actorId)
        {
            var formal = targets.GetPerception(actorId);
            var observations = new List<TargetObservation>(formal.Targets);
            var guides = new List<GuidanceObservation>();
            var isSlime = actorId == slimeId;
            if (isSlime && scenario == AIBranchScenario.SlimeAgitatedHuman)
                observations.Add(new TargetObservation(HumanSnapshot(), false, true, true));
            if (isSlime && scenario == AIBranchScenario.SlimeHungry)
                guides.Add(new GuidanceObservation(GuidanceKind.Food, new Vector2(-3f, 1f)));
            if (isSlime && scenario == AIBranchScenario.SlimeCold)
                guides.Add(new GuidanceObservation(GuidanceKind.Warmth, new Vector2(-3f, -1f)));
            if (isSlime && scenario == AIBranchScenario.SlimeAfraid)
                guides.Add(new GuidanceObservation(GuidanceKind.Food, new Vector2(3f, 1f)));
            if (!isSlime && scenario == AIBranchScenario.EnemyWander)
                guides.Add(new GuidanceObservation(GuidanceKind.Wander, new Vector2(2f, 1f)));

            var escape = isSlime && (scenario == AIBranchScenario.SlimeAfraid ||
                scenario == AIBranchScenario.SlimeFleeBelow50 ||
                scenario == AIBranchScenario.SlimeFleeBelow30 ||
                scenario == AIBranchScenario.SlimeFightAfterFailedFlee);
            var fleeSucceeded = isSlime && (scenario == AIBranchScenario.SlimeFleeBelow50 ||
                scenario == AIBranchScenario.SlimeFleeBelow30);
            return new PerceptionSnapshot(++sampleTick, observations, guides: guides,
                hasEscapeDestination: escape, escapeDestination: new Vector2(-3f, 0f),
                fleeCheckSucceeded: fleeSucceeded);
        }

        public bool TryGetActorSnapshot(ActorId actorId, out ActorSnapshot snapshot)
        {
            if (actorId == humanId)
            {
                snapshot = HumanSnapshot();
                return true;
            }
            return targets.TryGetActorSnapshot(actorId, out snapshot);
        }

        private ActorSnapshot HumanSnapshot()
            => new ActorSnapshot(humanId, new FactionId("Human"), ActorType.Guard,
                true, humanPosition);
    }
}
