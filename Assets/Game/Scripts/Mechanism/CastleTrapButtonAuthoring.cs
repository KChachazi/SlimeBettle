using System.Collections.Generic;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Guards;
using SlimeBattle.Actors.Slimes;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    /// <summary>Retracts its linked spikes while a living unit presses the button.</summary>
    [DisallowMultipleComponent]
    public sealed class CastleTrapButtonAuthoring : MonoBehaviour
    {
        [SerializeField] private string buttonId;
        [SerializeField] private GameObject[] trapsToControl;

        private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();
        private readonly List<Collider2D> staleOccupants = new List<Collider2D>();
        private Collider2D sensor;

        private void OnEnable()
        {
            sensor = GetComponent<Collider2D>();
            occupants.Clear();
            SetTrapsExtended(true);
        }

        private void OnDisable()
        {
            occupants.Clear();
            SetTrapsExtended(true);
        }

        private void OnTriggerEnter2D(Collider2D other) => TrackOccupant(other);
        private void OnTriggerStay2D(Collider2D other) => TrackOccupant(other);

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null) occupants.Remove(other);
            RefreshButtonState();
        }

        private void Update() => RefreshButtonState();

        private void TrackOccupant(Collider2D other)
        {
            if (other == null || !IsLivingUnit(other)) return;
            occupants.Add(other);
            SetTrapsExtended(false);
        }

        private void RefreshButtonState()
        {
            var isPressed = HasLivingColliderOccupant() || HasCarriedSlimePressing();
            SetTrapsExtended(!isPressed);
        }

        private bool HasLivingColliderOccupant()
        {
            staleOccupants.Clear();
            var hasOccupant = false;
            foreach (var occupant in occupants)
            {
                if (occupant == null || !occupant.enabled ||
                    !occupant.gameObject.activeInHierarchy || !IsLivingUnit(occupant))
                {
                    staleOccupants.Add(occupant);
                    continue;
                }

                hasOccupant = true;
            }

            foreach (var staleOccupant in staleOccupants)
                occupants.Remove(staleOccupant);
            return hasOccupant;
        }

        private bool HasCarriedSlimePressing()
        {
            if (sensor == null) return false;

            // Carried Slimes have disabled colliders; their visible body still presses
            // the plate when the escort weapon overlaps it.
            foreach (var slime in FindObjectsOfType<SlimeActor>())
            {
                if (slime == null || !slime.IsAlive || !slime.IsCarrying) continue;
                var sprite = slime.GetComponentInChildren<SpriteRenderer>();
                if (sprite != null && sensor.bounds.Intersects(sprite.bounds)) return true;
            }

            return false;
        }

        private static bool IsLivingUnit(Collider2D collider)
        {
            var slime = collider.GetComponentInParent<SlimeActor>();
            if (slime != null && slime.IsAlive) return true;

            var enemy = collider.GetComponentInParent<EnemyActor>();
            if (enemy != null && enemy.IsAlive) return true;

            var guard = collider.GetComponentInParent<GuardActor>();
            return guard != null && guard.IsAlive;
        }

        private void SetTrapsExtended(bool extended)
        {
            if (trapsToControl == null) return;

            foreach (var trap in trapsToControl)
            {
                if (trap != null && trap.activeSelf != extended)
                    trap.SetActive(extended);
            }
        }
    }
}
