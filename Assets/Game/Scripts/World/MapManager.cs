using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.World
{
    /// <summary>
    /// Binds static, solid colliders authored in the current scene. This first slice is used by
    /// MovementSystem to distinguish map geometry from dynamic actors and trigger volumes.
    /// </summary>
    public sealed class MapManager : IDisposable
    {
        private readonly HashSet<Collider2D> movementBlockers = new HashSet<Collider2D>();
        private Scene scene;
        private bool disposed;

        internal MapManager(Scene scene)
        {
            Bind(scene);
        }

        private bool IsBound => !disposed && scene.IsValid() && scene.isLoaded;

        internal bool IsMovementBlocker(Collider2D collider)
        {
            return IsBound && collider != null && collider.enabled &&
                collider.gameObject.activeInHierarchy && movementBlockers.Contains(collider) &&
                IsSolidStaticCollider(collider);
        }

        public void Dispose()
        {
            if (disposed) return;
            movementBlockers.Clear();
            scene = default(Scene);
            disposed = true;
        }

        private void Bind(Scene activeScene)
        {
            if (!activeScene.IsValid() || !activeScene.isLoaded) return;

            scene = activeScene;
            foreach (var root in scene.GetRootGameObjects())
            {
                var colliders = root.GetComponentsInChildren<Collider2D>(true);
                foreach (var collider in colliders)
                {
                    if (IsSolidStaticCollider(collider)) movementBlockers.Add(collider);
                }
            }
        }

        private bool IsSolidStaticCollider(Collider2D collider)
        {
            if (collider == null || collider.isTrigger) return false;
            if (collider.gameObject.scene.handle != scene.handle) return false;
            var body = collider.attachedRigidbody;
            return body == null || body.bodyType == RigidbodyType2D.Static;
        }
    }
}
