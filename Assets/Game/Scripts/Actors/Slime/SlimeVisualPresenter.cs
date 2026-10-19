using System;
using System.Collections.Generic;
using SlimeBattle.Mechanisms;
using SlimeBattle.Slimes;
using UnityEngine;

namespace SlimeBattle.Actors.Slimes
{
    [Serializable]
    public sealed class SlimeAnimationStateOverride
    {
        [SerializeField] private SlimeNeedState state;
        [SerializeField] private AnimatorOverrideController controller;

        public SlimeNeedState State => state;
        public AnimatorOverrideController Controller => controller;
    }

    [DisallowMultipleComponent]
    public sealed class SlimeVisualPresenter : MonoBehaviour
    {
        private static readonly HashSet<string> ReportedMissingAnimations =
            new HashSet<string>();

        [SerializeField] private Animator animator;
        [SerializeField] private SlimeAnimationStateOverride[] stateOverrides =
            new SlimeAnimationStateOverride[0];
        [SerializeField, Min(0f)] private float movementThreshold = 0.001f;
        [SerializeField, Min(0f)] private float movementGraceSeconds = 0.12f;
        [SerializeField, Min(0f)] private float collisionAnimationCooldown = 0.35f;

        private SlimeActor actor;
        private RuntimeAnimatorController normalController;
        private SlimeNeedState activeNeedState = (SlimeNeedState)(-1);
        private Vector3 previousPosition;
        private float lastMovedAt = float.NegativeInfinity;
        private float nextCollisionAnimationAt;
        private int activeStateShortHash;
        private bool actionPlaying;
        private bool initialized;
        private bool stopped;

        private void Awake()
        {
            actor = GetComponent<SlimeActor>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            normalController = animator == null ? null : animator.runtimeAnimatorController;
            previousPosition = transform.position;
        }

        public void Initialize(SlimeActor owner)
        {
            actor = owner;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogError("SlimeVisualPresenter requires a child Animator with a controller.", this);
                enabled = false;
                return;
            }

            if (normalController == null) normalController = animator.runtimeAnimatorController;
            previousPosition = transform.position;
            lastMovedAt = float.NegativeInfinity;
            activeNeedState = (SlimeNeedState)(-1);
            initialized = true;
            UpdateAnimationSet();
        }

        public void PlayAttack() => PlayOneShot("Attack");
        public void PlayFood() => PlayOneShot("Eat");
        public void PlayFurnace() => PlayOneShot("Furnace");
        public void PlayHit() => PlayOneShot("Hit");

        public float PlayDeath()
        {
            if (!initialized || animator == null)
            {
                StopForDeath();
                return 0f;
            }

            UpdateAnimationSet();
            var deathClip = ResolveDeathClip();
            stopped = true;
            actionPlaying = false;
            activeStateShortHash = Animator.StringToHash("Death");
            animator.enabled = true;
            animator.Play(Animator.StringToHash("Base Layer.Death"), 0, 0f);
            return deathClip == null ? 0f : deathClip.length;
        }

        public void StopForDeath()
        {
            stopped = true;
            actionPlaying = false;
            if (animator != null) animator.enabled = false;
        }

        private void Update()
        {
            if (!initialized || stopped || actor == null || animator == null) return;
            if (!actor.IsAlive)
            {
                StopForDeath();
                return;
            }

            UpdateAnimationSet();
            UpdateMovement();
            if (actionPlaying)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash == activeStateShortHash && state.normalizedTime >= 1f)
                {
                    actionPlaying = false;
                    PlayLocomotion();
                }
                return;
            }

            PlayLocomotion();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!initialized || stopped || collision == null ||
                Time.time < nextCollisionAnimationAt) return;
            var other = collision.collider.GetComponentInParent<SlimeActor>();
            if (other == null || other == actor) return;
            nextCollisionAnimationAt = Time.time + collisionAnimationCooldown;
            PlayOneShot("Collision");
        }

        private void UpdateAnimationSet()
        {
            var needState = actor.GetSnapshot().NeedState;
            if (needState == activeNeedState) return;

            var controller = ResolveController(needState);
            if (animator.runtimeAnimatorController != controller)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                var normalizedTime = Mathf.Clamp01(state.normalizedTime);
                animator.runtimeAnimatorController = controller;
                animator.Play(StatePath(activeStateShortHash), 0, normalizedTime);
            }
            activeNeedState = needState;
        }

        private AnimationClip ResolveDeathClip()
        {
            AnimationClip baseDeathClip = null;
            if (normalController != null)
            {
                foreach (var clip in normalController.animationClips)
                {
                    if (clip != null && clip.name == "Slime_Normal_Death")
                    {
                        baseDeathClip = clip;
                        break;
                    }
                }
            }

            if (baseDeathClip == null) return null;
            var overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
            if (overrideController == null) return baseDeathClip;

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);
            foreach (var clipOverride in overrides)
            {
                if (clipOverride.Key == baseDeathClip)
                    return clipOverride.Value == null ? baseDeathClip : clipOverride.Value;
            }
            return baseDeathClip;
        }

        private RuntimeAnimatorController ResolveController(SlimeNeedState needState)
        {
            if (needState == SlimeNeedState.None) return normalController;
            if (stateOverrides != null)
            {
                foreach (var stateOverride in stateOverrides)
                {
                    if (stateOverride == null || stateOverride.State != needState) continue;
                    var controller = stateOverride.Controller;
                    if (controller != null && controller.runtimeAnimatorController == normalController)
                    {
                        ReportMissingActionOverrides(needState, controller);
                        return controller;
                    }
                    break;
                }
            }

            ReportMissing(needState.ToString(),
                "Slime status '" + needState + "' has no animation override; using Normal clips.");
            return normalController;
        }

        private void ReportMissingActionOverrides(SlimeNeedState state,
            AnimatorOverrideController controller)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            foreach (var clipOverride in overrides)
            {
                if (clipOverride.Key == null ||
                    (clipOverride.Value != null && clipOverride.Value != clipOverride.Key)) continue;
                ReportMissing(state + ":" + clipOverride.Key.name,
                    "Slime status '" + state + "' has no '" + clipOverride.Key.name +
                    "' animation; using the Normal clip.");
            }
        }

        private void UpdateMovement()
        {
            var position = transform.position;
            if ((position - previousPosition).sqrMagnitude >= movementThreshold * movementThreshold)
                lastMovedAt = Time.time;
            previousPosition = position;
        }

        private void PlayLocomotion()
        {
            var stateName = Time.time - lastMovedAt <= movementGraceSeconds ? "Move" : "Idle";
            if (actionPlaying || StateName(activeStateShortHash) == stateName) return;
            PlayState(stateName, false);
        }

        private void PlayOneShot(string stateName)
        {
            if (!initialized || stopped || !actor.IsAlive || animator == null) return;
            PlayState(stateName, true);
        }

        private void PlayState(string stateName, bool isAction)
        {
            activeStateShortHash = Animator.StringToHash(stateName);
            actionPlaying = isAction;
            animator.Play(Animator.StringToHash("Base Layer." + stateName), 0, 0f);
        }

        private static string StatePath(int stateHash)
        {
            if (stateHash == 0) return "Base Layer.Idle";
            if (stateHash == Animator.StringToHash("Move")) return "Base Layer.Move";
            if (stateHash == Animator.StringToHash("Attack")) return "Base Layer.Attack";
            if (stateHash == Animator.StringToHash("Eat")) return "Base Layer.Eat";
            if (stateHash == Animator.StringToHash("Furnace")) return "Base Layer.Furnace";
            if (stateHash == Animator.StringToHash("Hit")) return "Base Layer.Hit";
            if (stateHash == Animator.StringToHash("Collision")) return "Base Layer.Collision";
            if (stateHash == Animator.StringToHash("Death")) return "Base Layer.Death";
            return "Base Layer.Idle";
        }

        private static string StateName(int stateHash)
        {
            if (stateHash == Animator.StringToHash("Move")) return "Move";
            if (stateHash == Animator.StringToHash("Attack")) return "Attack";
            if (stateHash == Animator.StringToHash("Eat")) return "Eat";
            if (stateHash == Animator.StringToHash("Furnace")) return "Furnace";
            if (stateHash == Animator.StringToHash("Hit")) return "Hit";
            if (stateHash == Animator.StringToHash("Collision")) return "Collision";
            return "Idle";
        }

        private void ReportMissing(string key, string message)
        {
            if (ReportedMissingAnimations.Add(key)) Debug.LogWarning(message, this);
        }
    }
}
