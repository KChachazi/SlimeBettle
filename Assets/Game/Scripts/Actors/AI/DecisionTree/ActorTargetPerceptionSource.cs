/* 正式 Actor 目标感知：只读 Manager 快照，按自身配置半径筛选存活的异阵营目标。 */
using System;
using System.Collections.Generic;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Slimes;
using SlimeBattle.Status;
using UnityEngine;

namespace SlimeBattle.Actors.AI.DecisionTree
{
    public sealed class ActorTargetPerceptionSource : IActorPerceptionSource
    {
        private sealed class FleeRoll
        {
            public ActorId TargetActorId;
            public int HealthBand;
            public bool Succeeded;
            public bool HasFled;
        }

        private readonly SlimeManager slimes;
        private readonly EnemyManager enemies;
        private readonly ConfigRegistry configs;
        private long sampleTick;
        private readonly System.Random random = new System.Random();
        private readonly Dictionary<ActorId, FleeRoll> fleeRolls =
            new Dictionary<ActorId, FleeRoll>();

        public ActorTargetPerceptionSource(SlimeManager slimes, EnemyManager enemies,
            ConfigRegistry configs)
        {
            this.slimes = slimes ?? throw new ArgumentNullException(nameof(slimes));
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public PerceptionSnapshot GetPerception(ActorId actorId)
        {
            var targets = new List<TargetObservation>();
            if (!TryGetActorSnapshot(actorId, out var self) || !self.IsAlive ||
                !TryGetConfigId(actorId, out var configId) ||
                !configs.TryGet(configId, out ActorConfig config))
            {
                fleeRolls.Remove(actorId);
                return new PerceptionSnapshot(++sampleTick, targets);
            }

            // 当前没有视线阻挡规则；距离是直线世界距离，活动范围仍由 EnemyDecision 检查。
            foreach (var slime in slimes.GetAliveSnapshots())
                AddIfDetected(self, new ActorSnapshot(slime.ActorId, slime.FactionId,
                    ActorType.Slime, slime.IsAlive, slime.Position, slime.CurrentHealth,
                    slime.MaxHealth, (ActorControlMode)slime.ControlMode, slime.StatusIds),
                    config.DetectionRange, targets);
            foreach (var enemy in enemies.GetAliveSnapshots())
                AddIfDetected(self, new ActorSnapshot(enemy.ActorId, enemy.FactionId,
                    ActorType.Enemy, enemy.IsAlive, enemy.Position, enemy.CurrentHealth,
                    enemy.MaxHealth, (ActorControlMode)enemy.ControlMode, enemy.StatusIds),
                    config.DetectionRange, targets);
            var hasEscapeDestination = TryGetFleeResult(self, targets,
                out var escapeDestination, out var fleeCheckSucceeded);
            return new PerceptionSnapshot(++sampleTick, targets,
                hasEscapeDestination: hasEscapeDestination,
                escapeDestination: escapeDestination,
                fleeCheckSucceeded: fleeCheckSucceeded);
        }

        public bool TryGetActorSnapshot(ActorId actorId, out ActorSnapshot snapshot)
        {
            if (slimes.TryGetSnapshot(actorId, out var slime))
            {
                snapshot = new ActorSnapshot(slime.ActorId, slime.FactionId, ActorType.Slime,
                    slime.IsAlive, slime.Position, slime.CurrentHealth, slime.MaxHealth,
                    (ActorControlMode)slime.ControlMode, slime.StatusIds);
                return true;
            }
            if (enemies.TryGetSnapshot(actorId, out var enemy))
            {
                snapshot = new ActorSnapshot(enemy.ActorId, enemy.FactionId, ActorType.Enemy,
                    enemy.IsAlive, enemy.Position, enemy.CurrentHealth, enemy.MaxHealth,
                    (ActorControlMode)enemy.ControlMode, enemy.StatusIds);
                return true;
            }
            snapshot = default(ActorSnapshot);
            return false;
        }

        private bool TryGetConfigId(ActorId actorId, out ConfigId configId)
        {
            if (slimes.TryGetSnapshot(actorId, out var slime))
            { configId = slime.ConfigId; return true; }
            if (enemies.TryGetSnapshot(actorId, out var enemy))
            { configId = enemy.ConfigId; return true; }
            configId = default(ConfigId);
            return false;
        }

        private static void AddIfDetected(ActorSnapshot self, ActorSnapshot target,
            float detectionRange, List<TargetObservation> targets)
        {
            if (target.ActorId == self.ActorId || !target.IsAlive ||
                target.FactionId == self.FactionId ||
                (target.Position - self.Position).sqrMagnitude > detectionRange * detectionRange)
                return;
            targets.Add(new TargetObservation(target, true));
        }

        private bool TryGetFleeResult(ActorSnapshot actor,
            IReadOnlyList<TargetObservation> targets, out Vector2 escapeDestination,
            out bool fleeCheckSucceeded)
        {
            escapeDestination = default(Vector2);
            fleeCheckSucceeded = false;
            if (actor.ActorType != ActorType.Slime || targets == null || targets.Count == 0 ||
                !configs.TryGetStatus(StatusKind.Fear, out var fearConfig))
            {
                fleeRolls.Remove(actor.ActorId);
                return false;
            }

            var nearestDistance = float.PositiveInfinity;
            var nearestTarget = default(ActorSnapshot);
            for (var i = 0; i < targets.Count; i++)
            {
                var observation = targets[i];
                if (!observation.IsVisible || !observation.IsHostile || !observation.Actor.IsAlive)
                    continue;
                var distance = (observation.Actor.Position - actor.Position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestTarget = observation.Actor;
            }
            if (!nearestTarget.ActorId.IsValid)
            {
                fleeRolls.Remove(actor.ActorId);
                return false;
            }

            var healthRatio = actor.MaxHealth <= 0f ? 1f : actor.CurrentHealth / actor.MaxHealth;
            var healthBand = healthRatio < fearConfig.CriticalFleeHealthThreshold ? 2
                : healthRatio < fearConfig.FleeHealthThreshold ? 1 : 0;
            if (healthBand == 0)
            {
                fleeRolls.Remove(actor.ActorId);
                return false;
            }

            if (!fleeRolls.TryGetValue(actor.ActorId, out var roll) ||
                roll.TargetActorId != nearestTarget.ActorId || roll.HealthBand != healthBand)
            {
                var chance = healthBand == 2
                    ? fearConfig.CriticalFleeChance : fearConfig.FleeChance;
                roll = new FleeRoll
                {
                    TargetActorId = nearestTarget.ActorId,
                    HealthBand = healthBand,
                    Succeeded = random.NextDouble() < chance
                };
                fleeRolls[actor.ActorId] = roll;
            }

            var away = actor.Position - nearestTarget.Position;
            if (away.sqrMagnitude <= 0.0001f) away = Vector2.up;
            escapeDestination = actor.Position + away.normalized * 3f;
            fleeCheckSucceeded = roll.Succeeded && !roll.HasFled;
            if (fleeCheckSucceeded) roll.HasFled = true;
            return true;
        }
    }
}
