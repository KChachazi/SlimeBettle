/* Level_test 的最小感知适配：无敌对目标，只向决策树提供有界随机漫游点。 */
using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Enemies;
using SlimeBattle.Slimes;
using SlimeBattle.World.Movement;
using UnityEngine;

namespace SlimeBattle.Actors.Spawning
{
    internal sealed class RandomWalkPerceptionSource : IActorPerceptionSource
    {
        private const float ArrivalTolerance = 0.25f;
        private const float MinimumDestinationDistance = 0.75f;

        private readonly SlimeManager slimeManager;
        private readonly EnemyManager enemyManager;
        private readonly MovementSystem movementSystem;
        private readonly EnemyActivityBounds defaultWanderBounds;
        private readonly System.Random random = new System.Random();
        private readonly Dictionary<ActorId, Vector2> destinations =
            new Dictionary<ActorId, Vector2>();
        private long sampleTick;

        public RandomWalkPerceptionSource(SlimeManager slimeManager,
            EnemyManager enemyManager, MovementSystem movementSystem,
            EnemyActivityBounds defaultWanderBounds)
        {
            this.slimeManager = slimeManager ?? throw new ArgumentNullException(nameof(slimeManager));
            this.enemyManager = enemyManager ?? throw new ArgumentNullException(nameof(enemyManager));
            this.movementSystem = movementSystem ?? throw new ArgumentNullException(nameof(movementSystem));
            this.defaultWanderBounds = defaultWanderBounds;
        }

        public PerceptionSnapshot GetPerception(ActorId actorId)
        {
            if (!TryGetActorSnapshot(actorId, out var actor) || !actor.IsAlive)
            {
                destinations.Remove(actorId);
                return new PerceptionSnapshot(++sampleTick, Array.Empty<TargetObservation>());
            }

            var bounds = GetWanderBounds(actorId, actor.ActorType);
            var shouldChooseDestination = !destinations.TryGetValue(actorId, out var destination);
            if (!shouldChooseDestination)
                shouldChooseDestination = ShouldChooseNewDestination(actorId, actor.Position, destination);
            if (shouldChooseDestination)
            {
                destination = ChooseDestination(bounds, actor.Position);
                destinations[actorId] = destination;
            }

            var guides = new[] { new GuidanceObservation(GuidanceKind.Wander, destination) };
            return new PerceptionSnapshot(++sampleTick, Array.Empty<TargetObservation>(),
                guides: guides);
        }

        public bool TryGetActorSnapshot(ActorId actorId, out ActorSnapshot snapshot)
        {
            if (slimeManager.TryGetSnapshot(actorId, out var slime))
            {
                snapshot = new ActorSnapshot(slime.ActorId, slime.FactionId, ActorType.Slime,
                    slime.IsAlive, slime.Position, slime.CurrentHealth, slime.MaxHealth,
                    (ActorControlMode)slime.ControlMode, slime.StatusIds);
                return true;
            }
            if (enemyManager.TryGetSnapshot(actorId, out var enemy))
            {
                snapshot = new ActorSnapshot(enemy.ActorId, enemy.FactionId, ActorType.Enemy,
                    enemy.IsAlive, enemy.Position, enemy.CurrentHealth, enemy.MaxHealth,
                    (ActorControlMode)enemy.ControlMode, enemy.StatusIds);
                return true;
            }

            snapshot = default(ActorSnapshot);
            return false;
        }

        private EnemyActivityBounds GetWanderBounds(ActorId actorId, ActorType actorType)
        {
            if (actorType == ActorType.Enemy &&
                enemyManager.TryGetSnapshot(actorId, out var enemy) && enemy.ActivityBounds.IsDefined)
                return enemy.ActivityBounds;
            return defaultWanderBounds;
        }

        private bool ShouldChooseNewDestination(ActorId actorId, Vector2 position,
            Vector2 destination)
        {
            if (Vector2.Distance(position, destination) <= ArrivalTolerance) return true;
            if (!movementSystem.TryGetMovementState(actorId, out var state)) return false;
            return state.Result == MovementResult.ReachedDestination ||
                state.Result == MovementResult.Blocked ||
                state.Result == MovementResult.Cancelled;
        }

        private Vector2 ChooseDestination(EnemyActivityBounds bounds, Vector2 currentPosition)
        {
            var minimum = bounds.Minimum;
            var maximum = bounds.Maximum;
            var candidate = currentPosition;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                candidate = new Vector2(
                    minimum.x + (float)random.NextDouble() * (maximum.x - minimum.x),
                    minimum.y + (float)random.NextDouble() * (maximum.y - minimum.y));
                if ((candidate - currentPosition).sqrMagnitude >=
                    MinimumDestinationDistance * MinimumDestinationDistance)
                    break;
            }
            return candidate;
        }
    }
}
