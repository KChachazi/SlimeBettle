using System;
using UnityEngine;

namespace SlimeBattle.Actors.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyVisualPresenter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float movementThreshold = 0.001f;
        [SerializeField, Min(0f)] private float movementGraceSeconds = 0.12f;

        private EnemyActor actor;
        private Vector3 previousPosition;
        private float lastMovedAt = float.NegativeInfinity;
        private int activeStateHash;
        private bool actionPlaying;
        private bool initialized;
        private bool stopped;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            previousPosition = transform.position;
        }

        public void Initialize(EnemyActor owner)
        {
            actor = owner;
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogError("EnemyVisualPresenter requires an Animator with a controller.", this);
                enabled = false;
                return;
            }

            previousPosition = transform.position;
            lastMovedAt = float.NegativeInfinity;
            activeStateHash = Animator.StringToHash("Idle");
            initialized = true;
            PlayState("Idle", false);
        }

        public void PlayAttack() => PlayState("Attack", true);
        public void PlayHit() => PlayState("Hit", true);

        public float PlayDeath()
        {
            if (!initialized || animator == null)
            {
                StopForDeath();
                return 0f;
            }

            var deathClip = FindClip("Death");
            stopped = true;
            actionPlaying = false;
            PlayState("Death", true);
            animator.Update(0f);
            var stateSpeed = Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).speed);
            if (stateSpeed <= 0f || float.IsNaN(stateSpeed) || float.IsInfinity(stateSpeed))
                stateSpeed = 1f;
            return deathClip == null ? 0f : deathClip.length / stateSpeed;
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

            UpdateMovement();
            if (actionPlaying)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash == activeStateHash && state.normalizedTime >= 1f)
                {
                    actionPlaying = false;
                    PlayLocomotion();
                }
                return;
            }

            PlayLocomotion();
        }

        private void UpdateMovement()
        {
            var position = transform.position;
            if ((position - previousPosition).magnitude >= movementThreshold)
                lastMovedAt = Time.time;
            previousPosition = position;
        }

        private void PlayLocomotion()
        {
            var stateName = Time.time - lastMovedAt <= movementGraceSeconds ? "Walk" : "Idle";
            if (actionPlaying || activeStateHash == Animator.StringToHash(stateName)) return;
            PlayState(stateName, false);
        }

        private void PlayState(string stateName, bool isAction)
        {
            if (!initialized || animator == null || (!isAction && stopped)) return;
            activeStateHash = Animator.StringToHash(stateName);
            actionPlaying = isAction;
            animator.enabled = true;
            animator.Play(Animator.StringToHash("Base Layer." + stateName), 0, 0f);
        }

        private AnimationClip FindClip(string suffix)
        {
            var controller = animator == null ? null : animator.runtimeAnimatorController;
            if (controller == null) return null;
            foreach (var clip in controller.animationClips)
                if (clip != null && clip.name.EndsWith("_" + suffix,
                    StringComparison.Ordinal)) return clip;
            return null;
        }
    }
}
