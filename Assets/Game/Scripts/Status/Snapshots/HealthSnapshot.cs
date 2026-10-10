using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct HealthSnapshot
    {
        public HealthSnapshot(ActorId actorId, float currentHealth, float maxHealth, bool isAlive)
        {
            ActorId = actorId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            IsAlive = isAlive;
        }

        public ActorId ActorId { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public bool IsAlive { get; }

        public static HealthSnapshot FromActor(ActorSnapshot actor) =>
            new HealthSnapshot(actor.ActorId, actor.CurrentHealth, actor.MaxHealth, actor.IsAlive);
    }
}
