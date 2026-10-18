/* 感知→意图→行动请求的测试替身；替身只存在于 Tests 目录。 */
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Guards;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Status;
using UnityEngine;

namespace SlimeBattle.Tests
{
    public sealed class ActorAiEditModeTests
    {
        private readonly List<GameObject> actorObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var actorObject in actorObjects)
                if (actorObject != null) UnityEngine.Object.DestroyImmediate(actorObject);
            actorObjects.Clear();
        }

        private sealed class FixedDecision : DecisionNode
        {
            private readonly ActionIntent intent;
            public FixedDecision(ActionIntent intent) { this.intent = intent; }
            public override ActionIntent Evaluate(DecisionContext context) { return intent; }
        }

        private sealed class FakeWorld : IActorPerceptionSource, IActorActionPort,
            IActorStatusQuery
        {
            public readonly Dictionary<ActorId, ActorSnapshot> Actors = new Dictionary<ActorId, ActorSnapshot>();
            public readonly List<ActorEffectRequest> Effects = new List<ActorEffectRequest>();
            public readonly List<ActorMovementRequest> Moves = new List<ActorMovementRequest>();
            public int CancelCount;
            public Action<ActorEffectRequest> OnEffect;
            public PerceptionSnapshot Perception;
            public StatusEntrySnapshot RageStatus;
            public bool HasStatus(ActorId actorId, StatusId statusId) =>
                statusId.IsValid && RageStatus.StatusId == statusId;
            public bool HasStatus(ActorId actorId, StatusKind kind) =>
                kind == StatusKind.Rage && RageStatus.StatusId.IsValid;
            public bool TryGetStatus(ActorId actorId, StatusId statusId,
                out StatusEntrySnapshot status)
            {
                status = RageStatus;
                return HasStatus(actorId, statusId);
            }
            public bool TryGetStatusSnapshot(ActorId actorId, out StatusSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }
            public PerceptionSnapshot GetPerception(ActorId id) { return Perception; }
            public bool TryGetActorSnapshot(ActorId id, out ActorSnapshot snapshot)
                => Actors.TryGetValue(id, out snapshot);
            public string SubmitMovement(ActorMovementRequest request)
            { Moves.Add(request); return "movement-1"; }
            public void CancelMovement(string id) { CancelCount++; }
            public void ApplyEffect(ActorEffectRequest request)
            { Effects.Add(request); OnEffect?.Invoke(request); }
        }

        [Test]
        public void DecisionTreeProducesMoveWaitAttackWithoutMutatingActor()
        {
            var self = Snapshot("self", "slime", Vector2.zero);
            var tree = new ActorDecisionTree();
            var context = new DecisionContext(self, new PerceptionSnapshot(1, null));
            tree.SetRoot(new FixedDecision(ActionIntent.Move(Vector2.right)));
            Assert.AreEqual(ActionIntentType.Move, tree.Evaluate(context).Type);
            tree.SetRoot(new FixedDecision(ActionIntent.Wait()));
            Assert.AreEqual(ActionIntentType.Wait, tree.Evaluate(context).Type);
            tree.SetRoot(new FixedDecision(ActionIntent.AttackTarget(
                new AttackIntent(self.ActorId, new ActorId("target"), new ConfigId("attack")))));
            Assert.AreEqual(ActionIntentType.Attack, tree.Evaluate(context).Type);
            Assert.AreEqual(Vector2.zero, self.Position);
            Assert.AreEqual(100f, self.CurrentHealth);
            tree.Stop();
            Assert.IsFalse(tree.Evaluate(context).IsValid);
        }

        [Test]
        public void StateMachineAcceptsRejectsInterruptsAndCancelsMovement()
        {
            var self = Snapshot("self", "slime", Vector2.zero);
            var world = new FakeWorld();
            var state = new ActorStateMachine(2f, 10f, 1f);
            var context = new StateContext(.1f, self, world, world);
            Assert.AreEqual(StateMachineResult.Rejected,
                state.SubmitIntent(default(ActionIntent), context));
            Assert.AreEqual(StateMachineResult.Accepted,
                state.SubmitIntent(ActionIntent.Move(Vector2.right), context));
            Assert.AreEqual(StateId.Moving, state.CurrentState);
            Assert.IsFalse(state.NotifyMovementCompleted("stale-request"));
            Assert.IsTrue(state.NotifyMovementCompleted("movement-1"));
            Assert.AreEqual(StateId.Idle, state.CurrentState);
            Assert.AreEqual(StateMachineResult.Accepted,
                state.SubmitIntent(ActionIntent.Move(Vector2.zero), context));
            Assert.AreEqual(StateId.Idle, state.CurrentState);
            Assert.AreEqual(1, world.Moves.Count);
            Assert.AreEqual(StateMachineResult.Accepted,
                state.SubmitIntent(ActionIntent.Move(Vector2.right), context));
            state.RequestInterrupt(InterruptReason.Pause, world);
            Assert.AreEqual(1, world.CancelCount);
            Assert.IsFalse(state.NotifyMovementCompleted("movement-1"));
            Assert.AreEqual(StateMachineResult.Rejected,
                state.SubmitIntent(ActionIntent.Move(Vector2.up), context));
            state.Resume();
            Assert.AreEqual(StateMachineResult.Accepted,
                state.SubmitIntent(ActionIntent.Wait(), context));
            state.RequestInterrupt(InterruptReason.Death, world);
            Assert.AreEqual(StateId.Dead, state.CurrentState);
            Assert.AreEqual(StateMachineResult.Rejected,
                state.SubmitIntent(ActionIntent.Wait(), context));
        }

        [Test]
        public void AttackRechecksTargetAndSubmitsOnlyOneEffectRequest()
        {
            var self = Snapshot("self", "slime", Vector2.zero);
            var target = Snapshot("target", "enemy", Vector2.right);
            var world = new FakeWorld();
            world.Actors.Add(target.ActorId, target);
            var state = new ActorStateMachine(2f, 10f, 1f);
            var context = new StateContext(.1f, self, world, world);
            var attack = ActionIntent.AttackTarget(
                new AttackIntent(self.ActorId, target.ActorId, new ConfigId("attack")));
            Assert.AreEqual(StateMachineResult.Accepted, state.SubmitIntent(attack, context));
            state.Tick(context);
            state.Tick(context);
            Assert.AreEqual(1, world.Effects.Count);
            Assert.AreEqual(10f, world.Effects[0].BaseAmount);
            Assert.AreEqual(StateMachineResult.Rejected, state.SubmitIntent(attack, context));

            var second = new ActorStateMachine(2f, 10f, 1f);
            Assert.AreEqual(StateMachineResult.Accepted, second.SubmitIntent(attack, context));
            world.Actors.Remove(target.ActorId);
            second.Tick(context);
            Assert.AreEqual(1, world.Effects.Count);
        }

        [Test]
        public void SynchronousDeathDuringHitDoesNotRestoreIdleState()
        {
            var self = Snapshot("self", "slime", Vector2.zero);
            var target = Snapshot("target", "enemy", Vector2.right);
            var world = new FakeWorld();
            world.Actors.Add(target.ActorId, target);
            var state = new ActorStateMachine(2f, 10f, 1f, 0f);
            world.OnEffect = _ => state.RequestInterrupt(InterruptReason.Death, world);
            var context = new StateContext(.1f, self, world, world);
            state.SubmitIntent(ActionIntent.AttackTarget(
                new AttackIntent(self.ActorId, target.ActorId, new ConfigId("attack"))), context);
            state.Tick(context);
            Assert.AreEqual(StateId.Dead, state.CurrentState);
            Assert.IsTrue(state.IsStopped);
            Assert.AreEqual(1, world.Effects.Count);
        }

        [Test]
        public void SlimeActorWaitsMovesAttacksAndStopsAfterDeath()
        {
            var config = ScriptableObject.CreateInstance<SlimeConfig>();
            try
            {
                config.SetConfigId("slime");
                config.ConfigureStats(10, 100, 1f, 1f);
                var slime = NewSlimeActor();
                slime.Initialize(config, new ActorId("slime-1"), Vector2.zero, new FactionId("slime"));
                var world = new FakeWorld { Perception = new PerceptionSnapshot(1, null) };
                var context = new ActorContext(.2f, world, world);
                slime.Tick(context);
                Assert.AreEqual(0, world.Moves.Count);

                world.Perception = new PerceptionSnapshot(2, null, true, new Vector2(3f, 0f));
                slime.Tick(context);
                Assert.AreEqual(1, world.Moves.Count);

                var enemy = Snapshot("enemy-1", "enemy", Vector2.right);
                world.Actors[enemy.ActorId] = enemy;
                world.Perception = new PerceptionSnapshot(3,
                    new[] { new TargetObservation(enemy, true) });
                slime.Tick(context);
                Assert.AreEqual(1, world.CancelCount);
                Assert.AreEqual(1, world.Effects.Count);
                Assert.AreEqual(enemy.ActorId, world.Effects[0].TargetActorId);

                slime.ApplyResolvedEffect(ResolvedEffect.Damage(100f));
                slime.Tick(context);
                Assert.AreEqual(1, world.Effects.Count);
                slime.Dispose();
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void EnemyActorHonorsActivityBoundsAndAttacksNearestHostile()
        {
            var config = ScriptableObject.CreateInstance<EnemyConfig>();
            try
            {
                config.SetConfigId("enemy");
                config.ConfigureStats(10, 100, 1f, 1f);
                var enemy = NewEnemyActor();
                enemy.Initialize(config, new ActorId("enemy-1"), Vector2.zero,
                    new FactionId("enemy"), EnemyTargetPolicy.NearestHostile,
                    new EnemyActivityBounds(new Vector2(-2f, -2f), new Vector2(2f, 2f)));
                var near = Snapshot("near", "slime", Vector2.right);
                var outside = Snapshot("outside", "slime", new Vector2(4f, 0f));
                var world = new FakeWorld();
                world.Actors[near.ActorId] = near;
                world.Actors[outside.ActorId] = outside;
                world.Perception = new PerceptionSnapshot(1,
                    new[] { new TargetObservation(outside, true), new TargetObservation(near, true) });
                enemy.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(1, world.Effects.Count);
                Assert.AreEqual(near.ActorId, world.Effects[0].TargetActorId);
                var enemySnapshot = enemy.GetSnapshot();
                Assert.AreEqual(near.ActorId, enemySnapshot.TargetActorId);
                Assert.IsTrue(enemySnapshot.IsAttacking);
                enemy.Dispose();
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void SlimeNeedAndFearSelectFormalDecisionPaths()
        {
            var config = ScriptableObject.CreateInstance<SlimeConfig>();
            try
            {
                config.SetConfigId("slime-needs");
                config.ConfigureStats(10, 100, 1f, 1f);
                var slime = NewSlimeActor();
                slime.Initialize(config, new ActorId("need-slime"), Vector2.zero,
                    new FactionId("slime"));
                var enemy = Snapshot("enemy", "enemy", new Vector2(.5f, 0f));
                slime.ApplyResolvedEffect(new ResolvedEffect(EffectType.Status, 0f,
                    statusId: SlimeStatusIds.Hungry));
                var world = new FakeWorld { Perception = new PerceptionSnapshot(1,
                    new[] { new TargetObservation(enemy, true) }, guides:
                    new[] { new GuidanceObservation(GuidanceKind.Food, new Vector2(4f, 0f)) }) };
                world.Actors[enemy.ActorId] = enemy;
                slime.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(new Vector2(4f, 0f), world.Moves[0].Destination);
                Assert.AreEqual(0, world.Effects.Count);
                Assert.That(slime.LastDecisionReason, Does.Contain("饥饿"));
                Assert.AreEqual(SlimeBattle.Slimes.SlimeNeedState.Hungry,
                    slime.GetSnapshot().NeedState);

                slime.ApplyResolvedEffect(new ResolvedEffect(EffectType.Status, 0f,
                    statusId: SlimeStatusIds.Afraid));
                world.Perception = new PerceptionSnapshot(2,
                    new[] { new TargetObservation(enemy, true) }, guides:
                    new[] { new GuidanceObservation(GuidanceKind.Food, new Vector2(4f, 0f)) },
                    hasEscapeDestination: true, escapeDestination: new Vector2(-3f, 0f));
                slime.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(new Vector2(-3f, 0f), world.Moves[1].Destination);
                Assert.That(slime.LastDecisionReason, Does.Contain("害怕"));
                Assert.AreEqual(SlimeBattle.Slimes.SlimeNeedState.Afraid,
                    slime.GetSnapshot().NeedState);
                slime.Dispose(world);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void SlimeUsesNearestGenericGuideAfterCombatCheck()
        {
            var config = ScriptableObject.CreateInstance<SlimeConfig>();
            try
            {
                config.SetConfigId("slime-guide-priority");
                config.ConfigureStats(10, 100, 1f, 1f);
                var slime = NewSlimeActor();
                slime.Initialize(config, new ActorId("guide-slime"), Vector2.zero,
                    new FactionId("slime"));
                var guides = new[]
                {
                    new GuidanceObservation(GuidanceKind.Food, new Vector2(4f, 0f)),
                    new GuidanceObservation(GuidanceKind.Warmth, new Vector2(2f, 0f))
                };
                var world = new FakeWorld
                {
                    Perception = new PerceptionSnapshot(1, null, guides: guides)
                };
                slime.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(new Vector2(2f, 0f), world.Moves[0].Destination);
                Assert.That(slime.LastDecisionReason, Does.Contain("正常/暴躁"));

                Assert.IsTrue(slime.NotifyMovementCompleted("movement-1"));
                var enemy = Snapshot("guide-enemy", "enemy", new Vector2(3f, 0f));
                world.Actors[enemy.ActorId] = enemy;
                world.Perception = new PerceptionSnapshot(2,
                    new[] { new TargetObservation(enemy, true) }, guides: guides);
                slime.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(enemy.Position, world.Moves[1].Destination);
                Assert.That(slime.LastDecisionReason, Does.Contain("接近战斗"));
                slime.Dispose(world);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void LowHealthFleeCheckAndAgitationUseFormalPaths()
        {
            var config = ScriptableObject.CreateInstance<SlimeConfig>();
            try
            {
                config.SetConfigId("slime-flee");
                config.ConfigureStats(10, 100, 1f, 1f);
                var slime = NewSlimeActor();
                slime.Initialize(config, new ActorId("flee-slime"), Vector2.zero,
                    new FactionId("slime"));
                slime.ApplyResolvedEffect(ResolvedEffect.Damage(60f));
                var enemy = Snapshot("enemy", "enemy", new Vector2(2f, 0f));
                var world = new FakeWorld { Perception = new PerceptionSnapshot(1,
                    new[] { new TargetObservation(enemy, true) },
                    hasEscapeDestination: true, escapeDestination: new Vector2(-2f, 0f),
                    fleeCheckSucceeded: true) };
                slime.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(new Vector2(-2f, 0f), world.Moves[0].Destination);
                Assert.That(slime.LastDecisionReason, Does.Contain("逃离战斗"));

                world.Perception = new PerceptionSnapshot(2,
                    new[] { new TargetObservation(enemy, true) },
                    hasEscapeDestination: true, escapeDestination: new Vector2(-2f, 0f));
                slime.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(enemy.Position, world.Moves[1].Destination);

                slime.ApplyResolvedEffect(ResolvedEffect.Damage(15f));
                world.Perception = new PerceptionSnapshot(3,
                    new[] { new TargetObservation(enemy, true) },
                    hasEscapeDestination: true, escapeDestination: new Vector2(-2f, 0f),
                    fleeCheckSucceeded: true);
                slime.Tick(new ActorContext(.2f, world, world));
                Assert.That(slime.LastDecisionReason, Does.Contain("低于30%"));

                slime.ApplyResolvedEffect(new ResolvedEffect(EffectType.Status, 0f,
                    statusId: SlimeStatusIds.Agitated));
                world.RageStatus = new StatusEntrySnapshot(
                    new StatusId(SlimeStatusIds.Agitated), default, 5f, 0,
                    false, 1, 1f, 1f, 1f, 1f, false, default,
                    StatusKind.Rage, elapsedDurationSeconds: 2f,
                    humanAttackDelaySeconds: 1f);
                var human = new ActorSnapshot(new ActorId("human"), new FactionId("humans"),
                    ActorType.Guard, true, Vector2.right, 100f, 100f);
                world.Actors[human.ActorId] = human;
                world.Perception = new PerceptionSnapshot(4,
                    new[] { new TargetObservation(human, false, true, true) });
                slime.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(human.ActorId, world.Effects[0].TargetActorId);
                Assert.That(slime.LastDecisionReason, Does.Contain("暴躁"));
                slime.Dispose(world);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void EnemyPatrolStaysWithinBounds()
        {
            var config = ScriptableObject.CreateInstance<EnemyConfig>();
            try
            {
                config.SetConfigId("enemy-patrol");
                config.ConfigureStats(10, 100, 1f, 1f);
                var enemy = NewEnemyActor();
                enemy.Initialize(config, new ActorId("patrol-enemy"), Vector2.zero,
                    new FactionId("enemy"), EnemyTargetPolicy.NearestHostile,
                    new EnemyActivityBounds(new Vector2(-2f, -2f), new Vector2(2f, 2f)));
                var world = new FakeWorld { Perception = new PerceptionSnapshot(1, null,
                    guides: new[] { new GuidanceObservation(GuidanceKind.Wander, Vector2.right) }) };
                enemy.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(Vector2.right, world.Moves[0].Destination);
                Assert.That(enemy.LastDecisionReason, Does.Contain("游走"));
                world.Perception = new PerceptionSnapshot(2, null, true, new Vector2(3f, 0f));
                enemy.Tick(new ActorContext(.2f, world, world));
                Assert.AreEqual(1, world.Moves.Count);
                Assert.That(enemy.LastDecisionReason, Does.Contain("留在活动范围"));
                enemy.Dispose(world);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void GuardDecisionUsesWeaponGuardAndNearbyAttackPriority()
        {
            var config = ScriptableObject.CreateInstance<GuardConfig>();
            try
            {
                config.SetConfigId("guard-priority");
                config.ConfigureStats(10, 100, 1f, 1f);
                var guard = new ActorSnapshot(new ActorId("guard"),
                    new FactionId("slime"), ActorType.Guard, true, Vector2.zero,
                    100f, 100f);
                var nearby = EnemySnapshot("nearby", new Vector2(.25f, 0f));
                var guardThreat = EnemySnapshot("guard-threat", new Vector2(.5f, .5f));
                var weaponThreat = EnemySnapshot("weapon-threat", new Vector2(2f, 0f));
                var perception = new PerceptionSnapshot(1, new[]
                {
                    new TargetObservation(nearby, true, isNearWeapon: true,
                        isAttacking: true),
                    new TargetObservation(guardThreat, true, isAttackingGuard: true,
                        isAttacking: true),
                    new TargetObservation(weaponThreat, true, isAttackingWeapon: true,
                        isAttacking: true)
                }, hasDestination: true, destination: Vector2.zero);

                var intent = new GuardDecision(config).Evaluate(
                    new DecisionContext(guard, perception));

                Assert.AreEqual(ActionIntentType.Move, intent.Type);
                Assert.AreEqual(weaponThreat.Position, intent.Destination);

                var closeWeaponThreat = EnemySnapshot("weapon-close", new Vector2(.5f, 0f));
                var attackIntent = new GuardDecision(config).Evaluate(new DecisionContext(
                    guard, new PerceptionSnapshot(2, new[]
                    {
                        new TargetObservation(closeWeaponThreat, true,
                            isAttackingWeapon: true, isAttacking: true)
                    }, hasDestination: true, destination: Vector2.zero)));
                Assert.AreEqual(ActionIntentType.Attack, attackIntent.Type);
                Assert.AreEqual(closeWeaponThreat.ActorId, attackIntent.Attack.TargetId);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void GuardDecisionFallsBackToGuardAttackThenReturnsToEscortPoint()
        {
            var config = ScriptableObject.CreateInstance<GuardConfig>();
            try
            {
                config.SetConfigId("guard-fallback");
                config.ConfigureStats(10, 100, 1f, 1f);
                var guard = new ActorSnapshot(new ActorId("guard"),
                    new FactionId("slime"), ActorType.Guard, true, Vector2.zero,
                    100f, 100f);
                var guardThreat = EnemySnapshot("guard-threat", new Vector2(2f, 0f));
                var nearby = EnemySnapshot("nearby", new Vector2(.25f, 0f));
                var guardAttack = new PerceptionSnapshot(1, new[]
                {
                    new TargetObservation(nearby, true, isNearWeapon: true,
                        isAttacking: true),
                    new TargetObservation(guardThreat, true, isAttackingGuard: true,
                        isAttacking: true)
                }, hasDestination: true, destination: Vector2.zero);
                var moveIntent = new GuardDecision(config).Evaluate(
                    new DecisionContext(guard, guardAttack));
                Assert.AreEqual(ActionIntentType.Move, moveIntent.Type);
                Assert.AreEqual(guardThreat.Position, moveIntent.Destination);

                var escortIntent = new GuardDecision(config).Evaluate(new DecisionContext(
                    guard, new PerceptionSnapshot(2, null, hasDestination: true,
                        destination: new Vector2(2f, 0f))));
                Assert.AreEqual(ActionIntentType.Move, escortIntent.Type);
                Assert.AreEqual(new Vector2(2f, 0f), escortIntent.Destination);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private static ActorSnapshot Snapshot(string id, string faction, Vector2 position)
            => new ActorSnapshot(new ActorId(id), new FactionId(faction),
                ActorType.Slime, true, position, 100f, 100f);

        private static ActorSnapshot EnemySnapshot(string id, Vector2 position)
            => new ActorSnapshot(new ActorId(id), new FactionId("enemy"),
                ActorType.Enemy, true, position, 100f, 100f);

        private SlimeActor NewSlimeActor()
        {
            var actorObject = new GameObject("Test SlimeActor");
            actorObjects.Add(actorObject);
            actorObject.AddComponent<CircleCollider2D>();
            return actorObject.AddComponent<SlimeActor>();
        }

        private EnemyActor NewEnemyActor()
        {
            var actorObject = new GameObject("Test EnemyActor");
            actorObjects.Add(actorObject);
            actorObject.AddComponent<CircleCollider2D>();
            return actorObject.AddComponent<EnemyActor>();
        }
    }
}
