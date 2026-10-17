using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Mechanisms;
using SlimeBattle.Slimes;

namespace SlimeBattle.Tests
{
    public sealed class LevelResultRulesEditModeTests
    {
        [Test]
        public void CaveNeedsSixAliveInsideAtEndOfFrameAndDecidesOnce()
        {
            using (var bus = new LevelEventBus())
            using (var rules = new LevelResultRules(bus))
            {
                var calls = 0;
                rules.ResultDecided += _ => calls++;
                bus.Publish(new WeaponDestroyedEvent(default(EscortWeaponSnapshot)));
                bus.Publish(new WeaponReachedGoalEvent(default(EscortWeaponSnapshot)));
                rules.EndFrame(.1f);
                Assert.IsFalse(rules.HasResult);
                bus.Publish(Occupancy(6, true));
                bus.Publish(Occupancy(5, false));
                rules.EndFrame(.1f);
                Assert.IsFalse(rules.HasResult);
                bus.Publish(Occupancy(6, true));
                rules.EndFrame(.1f);
                rules.EndFrame(.1f);
                Assert.AreEqual(LevelResult.Succeeded, rules.Result);
                Assert.AreEqual(1, calls);
            }
        }

        [Test]
        public void WaitingStartsOnFirstEntryAndExpiresAfterTenRunningSeconds()
        {
            using (var bus = new LevelEventBus())
            using (var rules = new LevelResultRules(bus))
            {
                rules.EndFrame(100f);
                Assert.IsFalse(rules.HasResult);
                bus.Publish(Occupancy(1, true));
                Assert.IsTrue(rules.IsWaiting);
                rules.EndFrame(4f);
                bus.Publish(Occupancy(0, false));
                rules.EndFrame(0f);
                Assert.AreEqual(6f, rules.RemainingSeconds);
                rules.EndFrame(5.9f);
                Assert.IsFalse(rules.HasResult);
                rules.EndFrame(.11f);
                Assert.AreEqual(LevelResult.Failed, rules.Result);
            }
        }

        [Test]
        public void DepletionFailsImmediatelyAndDisposeStopsListening()
        {
            using (var bus = new LevelEventBus())
            {
                var rules = new LevelResultRules(bus);
                bus.Publish(new SlimeDepletedEvent(new LevelId("level")));
                rules.EndFrame(.1f);
                Assert.AreEqual(LevelResult.Failed, rules.Result);
                rules.Dispose();
                bus.Publish(Occupancy(6, true));
                Assert.AreEqual(LevelResult.Failed, rules.Result);
            }
        }

        [Test]
        public void EscortSucceedsWhenWeaponReachesGoal()
        {
            using (var bus = new LevelEventBus())
            using (var rules = new LevelResultRules(bus, LevelObjectiveKind.EscortWeapon))
            {
                bus.Publish(new WeaponReachedGoalEvent(default(EscortWeaponSnapshot)));
                rules.EndFrame(.1f);
                Assert.AreEqual(LevelResult.Succeeded, rules.Result);
            }
        }

        [Test]
        public void EscortFailsWhenWeaponIsDestroyedOrSlimesDeplete()
        {
            using (var bus = new LevelEventBus())
            using (var rules = new LevelResultRules(bus, LevelObjectiveKind.EscortWeapon))
            {
                bus.Publish(new WeaponDestroyedEvent(default(EscortWeaponSnapshot)));
                rules.EndFrame(.1f);
                Assert.AreEqual(LevelResult.Failed, rules.Result);
            }

            using (var depletedBus = new LevelEventBus())
            using (var depletedRules = new LevelResultRules(
                depletedBus, LevelObjectiveKind.EscortWeapon))
            {
                depletedBus.Publish(new SlimeDepletedEvent(new LevelId("level")));
                depletedRules.EndFrame(.1f);
                Assert.AreEqual(LevelResult.Failed, depletedRules.Result);
            }
        }

        private static SlimeGoalOccupancyChangedEvent Occupancy(int count, bool inside) =>
            new SlimeGoalOccupancyChangedEvent(new LevelId("level"),
                new ActorId("slime"), count, inside);
    }
}
