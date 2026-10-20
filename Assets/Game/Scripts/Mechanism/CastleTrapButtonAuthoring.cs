using SlimeBattle.Core;
using SlimeBattle.Actors.Slimes;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    /// <summary>Deactivates the explicitly linked castle spike trap group when a Slime enters.</summary>
    [DisallowMultipleComponent]
    public sealed class CastleTrapButtonAuthoring : MonoBehaviour
    {
        [SerializeField] private string buttonId;
        [SerializeField] private GameObject[] trapsToDisable;
        private LevelManager levelManager;
        private Collider2D sensor;
        private bool activated;

        private void OnEnable()
        {
            levelManager = FindObjectOfType<LevelManager>();
            sensor = GetComponent<Collider2D>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.GetComponentInParent<SlimeActor>() == null) return;
            Activate();
        }

        private void Update()
        {
            if (activated || sensor == null || levelManager == null ||
                levelManager.GetLevelState() != LevelState.Running) return;

            // Carried Slimes have their colliders disabled, but their visible body
            // still presses the plate when the escort weapon reaches its boundary.
            foreach (var slime in FindObjectsOfType<SlimeActor>())
            {
                if (slime == null || !slime.IsAlive || !slime.IsCarrying) continue;
                var sprite = slime.GetComponent<SpriteRenderer>();
                if (sprite != null && sensor.bounds.Intersects(sprite.bounds))
                {
                    Activate();
                    return;
                }
            }
        }

        private void Activate()
        {
            if (activated) return;
            var disabledCount = 0;
            if (trapsToDisable != null)
                foreach (var trap in trapsToDisable)
                {
                    if (trap == null || !trap.activeSelf) continue;
                    trap.SetActive(false);
                    disabledCount++;
                }

            if (disabledCount == 0) return;
            activated = true;
            Debug.Log($"Castle button '{buttonId}' disabled {disabledCount} spike trap(s).", this);
        }
    }
}
