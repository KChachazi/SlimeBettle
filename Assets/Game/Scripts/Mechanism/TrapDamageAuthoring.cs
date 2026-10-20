using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Guards;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Core;
using SlimeBattle.Status;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    /// <summary>Applies a steady max-health fraction to actors that remain on this trap.</summary>
    [DisallowMultipleComponent]
    public sealed class TrapDamageAuthoring : MonoBehaviour
    {
        private const float DamageFractionPerSecond = 0.05f;
        private const float DamageIntervalSeconds = 1f;

        private sealed class Contact
        {
            public readonly HashSet<int> SolidColliderIds = new HashSet<int>();
            public readonly HashSet<int> SensorColliderIds = new HashSet<int>();
            public float ElapsedSeconds;
        }

        private readonly Dictionary<ActorId, Contact> contacts =
            new Dictionary<ActorId, Contact>();
        private LevelManager levelManager;
        private LevelContext contactContext;

        private void OnEnable()
        {
            FindLevelManager();
        }

        private void Start() => FindLevelManager();

        private void OnCollisionEnter2D(Collision2D collision) =>
            AddContact(collision == null ? null : collision.otherCollider, false);

        private void OnCollisionStay2D(Collision2D collision) =>
            AddContact(collision == null ? null : collision.otherCollider, false);

        private void OnCollisionExit2D(Collision2D collision) =>
            RemoveContact(collision == null ? null : collision.otherCollider, false);

        private void OnTriggerEnter2D(Collider2D other) => AddContact(other, true);
        private void OnTriggerStay2D(Collider2D other) => AddContact(other, true);
        private void OnTriggerExit2D(Collider2D other) => RemoveContact(other, true);

        private void Update()
        {
            var context = levelManager == null ? null : levelManager.GetCurrentContext();
            if (context != contactContext)
            {
                contacts.Clear();
                contactContext = context;
            }
            if (context == null || levelManager.GetLevelState() != LevelState.Running ||
                contacts.Count == 0 || Time.deltaTime <= 0f) return;

            var deltaTime = Time.deltaTime;
            var actorIds = new List<ActorId>(contacts.Keys);
            foreach (var actorId in actorIds)
            {
                if (!contacts.TryGetValue(actorId, out var contact)) continue;
                if (!context.StatusManager.TryGetHealthSnapshot(actorId, out var health) ||
                    health.MaxHealth <= 0f)
                {
                    contacts.Remove(actorId);
                    continue;
                }

                contact.ElapsedSeconds += deltaTime;
                while (contact.ElapsedSeconds >= DamageIntervalSeconds)
                {
                    contact.ElapsedSeconds -= DamageIntervalSeconds;
                    context.StatusManager.ApplyEffect(new EffectRequest(
                        EffectRequestId.NewId(), default(ActorId), actorId,
                        EffectType.Damage, health.MaxHealth * DamageFractionPerSecond,
                        relationshipPolicy: EffectRelationshipPolicy.Any,
                        requireLivingSource: false));
                }
            }
        }

        private void AddContact(Collider2D other, bool isSensor)
        {
            if (other == null || !TryGetActorId(other, out var actorId)) return;
            if (!contacts.TryGetValue(actorId, out var contact))
            {
                contact = new Contact();
                contacts.Add(actorId, contact);
            }
            if (isSensor) contact.SensorColliderIds.Add(other.GetInstanceID());
            else contact.SolidColliderIds.Add(other.GetInstanceID());
        }

        private void RemoveContact(Collider2D other, bool isSensor)
        {
            if (other == null || !TryGetActorId(other, out var actorId) ||
                !contacts.TryGetValue(actorId, out var contact)) return;
            if (isSensor) contact.SensorColliderIds.Remove(other.GetInstanceID());
            else contact.SolidColliderIds.Remove(other.GetInstanceID());
            if (contact.SolidColliderIds.Count == 0 &&
                contact.SensorColliderIds.Count == 0) contacts.Remove(actorId);
        }

        private static bool TryGetActorId(Collider2D collider, out ActorId actorId)
        {
            var slime = collider.GetComponentInParent<SlimeActor>();
            if (slime != null && slime.IsAlive)
            {
                actorId = slime.ActorId;
                return actorId.IsValid;
            }
            var enemy = collider.GetComponentInParent<EnemyActor>();
            if (enemy != null && enemy.IsAlive)
            {
                actorId = enemy.ActorId;
                return actorId.IsValid;
            }
            var guard = collider.GetComponentInParent<GuardActor>();
            if (guard != null && guard.IsAlive)
            {
                actorId = guard.ActorId;
                return actorId.IsValid;
            }

            actorId = default(ActorId);
            return false;
        }

        private void OnDisable() => contacts.Clear();

        private void FindLevelManager()
        {
            if (levelManager == null) levelManager = FindObjectOfType<LevelManager>();
        }
    }
}
