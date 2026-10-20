using SlimeBattle.Actors.Slimes;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    /// <summary>Starts a rolling rock when a live Slime enters its authored map region.</summary>
    [DisallowMultipleComponent]
    public sealed class RollingRockTriggerAuthoring : MonoBehaviour
    {
        private RollingRockAuthoring rock;

        public void Initialize(RollingRockAuthoring rollingRock) => rock = rollingRock;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (rock == null || other == null) return;
            var slime = other.GetComponentInParent<SlimeActor>();
            if (slime != null && slime.IsAlive) rock.TryActivate();
        }
    }
}
