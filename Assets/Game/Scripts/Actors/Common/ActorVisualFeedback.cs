using UnityEngine;

namespace SlimeBattle.Actors
{
    /// <summary>Shared hit flash and death fade for every runtime actor visual.</summary>
    [DisallowMultipleComponent]
    public sealed class ActorVisualFeedback : MonoBehaviour
    {
        private const float HitFlashDuration = 0.12f;
        private const float DeathFadeDuration = 0.35f;

        private SpriteRenderer[] renderers = new SpriteRenderer[0];
        private Color[] baseColors = new Color[0];
        private float flashRemaining;
        private float fadeRemaining;
        private bool deathStarted;
        private bool initialized;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (initialized) return;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].color;
            initialized = true;
        }

        public void NotifyEffect(ResolvedEffect effect, EffectApplyResult result)
        {
            if (!initialized) Initialize();
            if (!effect.IsDamage || !result.Changed) return;

            flashRemaining = Mathf.Max(flashRemaining, HitFlashDuration);
            if (result.DidKill) BeginDeathFade();
        }

        private void Update()
        {
            if (!initialized || renderers.Length == 0) return;
            if (flashRemaining > 0f)
                flashRemaining = Mathf.Max(0f, flashRemaining - Time.deltaTime);
            if (deathStarted)
            {
                fadeRemaining = Mathf.Max(0f, fadeRemaining - Time.deltaTime);
                if (fadeRemaining <= 0f)
                {
                    CompleteDeathFade();
                    return;
                }
            }
            ApplyColors();
        }

        private void BeginDeathFade()
        {
            if (deathStarted) return;
            deathStarted = true;
            fadeRemaining = DeathFadeDuration;
            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.simulated = false;
        }

        private void ApplyColors()
        {
            var flashFactor = HitFlashDuration <= 0f
                ? 0f : Mathf.Clamp01(flashRemaining / HitFlashDuration);
            var fadeFactor = deathStarted
                ? Mathf.Clamp01(fadeRemaining / DeathFadeDuration) : 1f;
            for (var i = 0; i < renderers.Length; i++)
            {
                var color = Color.Lerp(baseColors[i], Color.white, flashFactor);
                color.a = baseColors[i].a * fadeFactor;
                renderers[i].color = color;
            }
        }

        private void CompleteDeathFade()
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                var color = baseColors[i];
                color.a = 0f;
                renderers[i].color = color;
                renderers[i].enabled = false;
            }
            enabled = false;
        }
    }
}
