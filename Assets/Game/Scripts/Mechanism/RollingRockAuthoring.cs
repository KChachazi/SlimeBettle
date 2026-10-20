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
    /// <summary>One-shot, rightward rolling hazard. Each unit can be damaged once per roll.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class RollingRockAuthoring : MonoBehaviour
    {
        private enum MotionState { Idle, Delayed, Rolling, Finished }

        private readonly HashSet<ActorId> damagedActors = new HashSet<ActorId>();
        private LevelManager levelManager;
        private Rigidbody2D body;
        private MotionState state;
        private float damage;
        private float speed;
        private float delay;
        private float endX;
        private float delayElapsed;

        public void Initialize(LevelManager manager, float oneShotDamage,
            float speedWorldUnitsPerSecond, float delaySeconds, float rightEdge)
        {
            levelManager = manager;
            damage = Mathf.Max(0f, oneShotDamage);
            speed = Mathf.Max(0.01f, speedWorldUnitsPerSecond);
            delay = Mathf.Max(0f, delaySeconds);
            endX = Mathf.Max(transform.position.x, rightEdge);
            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public bool TryActivate()
        {
            if (state != MotionState.Idle || levelManager == null ||
                levelManager.GetLevelState() != LevelState.Running) return false;
            state = MotionState.Delayed;
            delayElapsed = 0f;
            return true;
        }

        private void FixedUpdate()
        {
            if (body == null || levelManager == null ||
                levelManager.GetLevelState() != LevelState.Running) return;

            if (state == MotionState.Delayed)
            {
                delayElapsed += Time.fixedDeltaTime;
                if (delayElapsed < delay) return;
                state = MotionState.Rolling;
            }

            if (state != MotionState.Rolling) return;
            var current = body.position;
            var next = Vector2.MoveTowards(current,
                new Vector2(endX, current.y), speed * Time.fixedDeltaTime);
            body.MovePosition(next);
            if (next.x >= endX) state = MotionState.Finished;
        }

        private void OnCollisionEnter2D(Collision2D collision) =>
            TryDamageActor(collision == null ? null : collision.otherCollider);

        private void OnCollisionStay2D(Collision2D collision) =>
            TryDamageActor(collision == null ? null : collision.otherCollider);

        private void TryDamageActor(Collider2D collider)
        {
            if (state != MotionState.Rolling || damage <= 0f || collider == null ||
                levelManager == null) return;
            var context = levelManager.GetCurrentContext();
            if (context == null || context.StatusManager == null) return;

            ActorId actorId;
            var slime = collider.GetComponentInParent<SlimeActor>();
            if (slime != null && slime.IsAlive) actorId = slime.ActorId;
            else
            {
                var enemy = collider.GetComponentInParent<EnemyActor>();
                if (enemy != null && enemy.IsAlive) actorId = enemy.ActorId;
                else
                {
                    var guard = collider.GetComponentInParent<GuardActor>();
                    if (guard == null || !guard.IsAlive) return;
                    actorId = guard.ActorId;
                }
            }

            if (!actorId.IsValid || !damagedActors.Add(actorId)) return;
            context.StatusManager.ApplyEffect(new EffectRequest(
                EffectRequestId.NewId(), default(ActorId), actorId,
                EffectType.Damage, damage,
                relationshipPolicy: EffectRelationshipPolicy.Any,
                requireLivingSource: false));
        }
    }
}
