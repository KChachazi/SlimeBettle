using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Data;
using SlimeBattle.Status;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    public readonly struct EscortWeaponSnapshot
    {
        public EscortWeaponSnapshot(MechanismId id, ConfigId configId,
            int health, int maxHealth, Vector2 position, MapRouteId routeId,
            int destinationWaypointIndex, FixedRouteMovementState transportState,
            ActorId combatTargetId, int carrierCount, float currentSpeed)
        {
            MechanismId = id;
            ConfigId = configId;
            Health = health;
            MaxHealth = maxHealth;
            Position = position;
            RouteId = routeId;
            DestinationWaypointIndex = destinationWaypointIndex;
            TransportState = transportState;
            CombatTargetId = combatTargetId;
            CarrierCount = carrierCount;
            CurrentSpeedWorldUnits = currentSpeed;
        }

        public MechanismId MechanismId { get; }
        public ConfigId ConfigId { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public Vector2 Position { get; }
        public MapRouteId RouteId { get; }
        public int DestinationWaypointIndex { get; }
        public FixedRouteMovementState TransportState { get; }
        public ActorId CombatTargetId { get; }
        public int CarrierCount { get; }
        public float CurrentSpeedWorldUnits { get; }
        public bool IsWaitingForCarriers => CarrierCount == 0 && !IsDestroyed && !HasReachedGoal;
        public bool IsDestroyed => Health <= 0;
        public bool HasReachedGoal => TransportState == FixedRouteMovementState.Completed;
    }

    internal sealed class EscortWeapon : IDisposable
    {
        private readonly MechanismId id;
        private readonly ActorId combatTargetId = ActorId.NewId();
        private readonly WeaponConfig config;
        private readonly Transform body;
        private readonly WeaponRouteMotor motor;
        private readonly FixedRouteMovementAdapter route;
        private readonly HashSet<EffectRequestId> acceptedDamageRequests =
            new HashSet<EffectRequestId>();
        private readonly HashSet<ActorId> carriers = new HashSet<ActorId>();
        private int health;
        private bool disposed;
        private bool destroyedPublished;
        private bool goalPublished;

        public EscortWeapon(MechanismId id, WeaponConfig config,
            MapRouteSnapshot fixedRoute, Transform body, MapManager map)
        {
            if (!id.IsValid || config == null || body == null || map == null ||
                !fixedRoute.IsValid)
                throw new ArgumentException("Weapon requires a valid config, route, body and map.");
            this.id = id;
            this.config = config;
            this.body = body;
            health = config.MaxHealth;
            motor = new WeaponRouteMotor(body, map, config);
            route = new FixedRouteMovementAdapter(fixedRoute, motor);
            if (!route.Start())
                throw new InvalidOperationException("Weapon is not at the route start.");
        }

        public event Action<EscortWeaponSnapshot, ActorId, EffectRequestId, int> Damaged;
        public event Action<EscortWeaponSnapshot> Destroyed;
        public event Action<EscortWeaponSnapshot> ReachedGoal;

        public IReadOnlyCollection<ActorId> CarrierIds => carriers;

        public bool AddCarrier(ActorId actorId)
        {
            if (disposed || !actorId.IsValid || health <= 0 || goalPublished ||
                carriers.Count >= 4 || !carriers.Add(actorId)) return false;
            motor.SetSpeedFactor(carriers.Count * 0.25f);
            return true;
        }

        public bool RemoveCarrier(ActorId actorId)
        {
            if (disposed || !carriers.Remove(actorId)) return false;
            motor.SetSpeedFactor(carriers.Count * 0.25f);
            return true;
        }

        public EscortWeaponSnapshot GetSnapshot() => new EscortWeaponSnapshot(id,
            config.ConfigId, health, config.MaxHealth, body.position,
            route.Route.RouteId, route.DestinationWaypointIndex, route.State,
            combatTargetId, carriers.Count,
            config.TransportSpeedWorldUnits * carriers.Count * 0.25f);

        public bool RequestDamage(ActorId sourceActorId, EffectRequestId requestId,
            int amount)
        {
            if (disposed || health <= 0 || goalPublished || !sourceActorId.IsValid ||
                !requestId.IsValid || amount <= 0 ||
                !acceptedDamageRequests.Add(requestId)) return false;
            var actualLoss = Mathf.Min(health, amount);
            health -= actualLoss;
            var snapshot = GetSnapshot();
            Damaged?.Invoke(snapshot, sourceActorId, requestId, actualLoss);
            if (health == 0 && !destroyedPublished)
            {
                destroyedPublished = true;
                route.Cancel(CancelReason.Interrupted);
                Destroyed?.Invoke(GetSnapshot());
            }
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (disposed || health <= 0 || goalPublished || deltaTime <= 0f) return;
            if (route.State == FixedRouteMovementState.Blocked)
                route.RetryBlockedSegment();
            motor.Tick(deltaTime);
            route.Tick();
            if (route.State == FixedRouteMovementState.Completed && !goalPublished)
            {
                goalPublished = true;
                ReachedGoal?.Invoke(GetSnapshot());
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            route.Cancel(CancelReason.Disposed);
            Damaged = null;
            Destroyed = null;
            ReachedGoal = null;
            acceptedDamageRequests.Clear();
            carriers.Clear();
            disposed = true;
        }
    }
}
