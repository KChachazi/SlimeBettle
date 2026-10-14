using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Guards
{
    public readonly struct GuardSnapshot
    {
        public GuardSnapshot(ActorSnapshot actor, ConfigId configId, StateId actionState)
        {
            ActorId = actor.ActorId;
            FactionId = actor.FactionId;
            Position = actor.Position;
            IsAlive = actor.IsAlive;
            CurrentHealth = actor.CurrentHealth;
            MaxHealth = actor.MaxHealth;
            StatusIds = actor.StatusIds;
            ConfigId = configId;
            ActionState = actionState;
        }

        public ActorId ActorId { get; }
        public FactionId FactionId { get; }
        public Vector2 Position { get; }
        public bool IsAlive { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public IReadOnlyList<string> StatusIds { get; }
        public ConfigId ConfigId { get; }
        public StateId ActionState { get; }
    }
}
