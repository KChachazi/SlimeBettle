using SlimeBattle.Actors;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Slimes;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    /// <summary>Scene and prefab adapter for mechanism identity, collider contacts, and visuals.</summary>
    [DisallowMultipleComponent]
    public sealed class MechanismAuthoring : MonoBehaviour
    {
        [SerializeField] private string mechanismId;
        [SerializeField] private string configId;
        [SerializeField] private Collider2D interactionCollider;
        [SerializeField] private Collider2D solidCollider;

        private MechanismSystem system;

        public MechanismId MechanismId => new MechanismId(mechanismId);
        public string ConfigId => configId ?? string.Empty;
        internal bool IsBound => system != null;
        internal Collider2D InteractionCollider => interactionCollider != null
            ? interactionCollider : GetComponent<Collider2D>();
        internal Collider2D SolidCollider => solidCollider != null
            ? solidCollider : GetComponent<Collider2D>();

        internal void Bind(MechanismSystem mechanismSystem)
        {
            system = mechanismSystem;
        }

        internal void Unbind(MechanismSystem mechanismSystem)
        {
            if (system == mechanismSystem) system = null;
        }

        internal void AssignMechanismId(MechanismId value)
        {
            mechanismId = value.Value;
        }

        internal void SetInteractionEnabled(bool enabled)
        {
            var collider = InteractionCollider;
            if (collider != null) collider.enabled = enabled;
        }

        internal void SetMechanismVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            NotifyContact(other, MechanismContactKind.Entered);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            NotifyContact(other, MechanismContactKind.Stayed);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            NotifyContact(other, MechanismContactKind.Exited);
        }

        private void NotifyContact(Collider2D other, MechanismContactKind contactKind)
        {
            if (system == null || other == null || !TryGetActor(other,
                    out var actorId, out var actorType, out var actorPosition)) return;

            system.NotifyContact(new MechanismTriggerContext(MechanismId,
                actorId, actorType, contactKind, actorPosition, other.GetInstanceID()));
        }

        private static bool TryGetActor(Collider2D collider, out ActorId actorId,
            out ActorType actorType, out Vector2 actorPosition)
        {
            var slime = collider.GetComponentInParent<SlimeActor>();
            if (slime != null && slime.IsAlive)
            {
                actorId = slime.ActorId;
                actorType = ActorType.Slime;
                actorPosition = slime.transform.position;
                return actorId.IsValid;
            }

            var enemy = collider.GetComponentInParent<EnemyActor>();
            if (enemy != null && enemy.IsAlive)
            {
                actorId = enemy.ActorId;
                actorType = ActorType.Enemy;
                actorPosition = enemy.transform.position;
                return actorId.IsValid;
            }

            actorId = default(ActorId);
            actorType = ActorType.Unknown;
            actorPosition = default(Vector2);
            return false;
        }

        private void OnDestroy()
        {
            if (system != null) system.NotifyAuthoringDestroyed(this);
        }
    }
}
