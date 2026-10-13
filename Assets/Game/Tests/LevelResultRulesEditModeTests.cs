using NUnit.Framework;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Slimes;

namespace SlimeBattle.Tests
{
    public sealed class LevelResultRulesEditModeTests
    {
        [Test]
        public void SameFrameSuccessWinsAndResultIsEmittedOnce()
        {
            using (var bus = new LevelEventBus())
            using (var rules = new LevelResultRules(bus))
            {
                var calls = 0;
                rules.ResultDecided += _ => calls++;
                bus.Publish(new WeaponDestroyedEvent(default(EscortWeaponSnapshot)));
                bus.Publish(new SlimeDepletedEvent(new LevelId("level")));
                bus.Publish(new WeaponReachedGoalEvent(default(EscortWeaponSnapshot)));
                rules.EndFrame();
                rules.EndFrame();
                Assert.AreEqual(LevelResult.Succeeded, rules.Result);
                Assert.AreEqual(1, calls);
            }
        }

        [Test]
        public void DestroyedWeaponFailsAfterFrameAndDisposeStopsListening()
        {
            using (var bus = new LevelEventBus())
            {
                var rules = new LevelResultRules(bus);
                bus.Publish(new WeaponDestroyedEvent(default(EscortWeaponSnapshot)));
                Assert.IsFalse(rules.HasResult);
                rules.EndFrame();
                Assert.AreEqual(LevelResult.Failed, rules.Result);
                rules.Dispose();
                bus.Publish(new WeaponReachedGoalEvent(default(EscortWeaponSnapshot)));
                Assert.AreEqual(LevelResult.Failed, rules.Result);
            }
        }
    }
}
