using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Data;
using SlimeBattle.Slimes;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SlimeBattle.Tests.MapMovement
{
    public sealed class SlimeDeathAnimationPlayModeTests
    {
        [UnityTest]
        public IEnumerator DeathAnimationFinishesBeforeTheExistingFade()
        {
            GameObject prefab = null;
            SlimeConfig config = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab");
            config = AssetDatabase.LoadAssetAtPath<SlimeConfig>(
                "Assets/Game/Configs/Level_test/CFG_Slime_MVP.asset");
#else
            Assert.Ignore("This visual integration test loads the project prefab through the Editor.");
            yield break;
#endif
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(config);
            var statuses = new string[]
            {
                null, SlimeStatusIds.Hungry, SlimeStatusIds.Afraid, SlimeStatusIds.Agitated
            };
            var expectedClips = new string[]
            {
                "Slime_Normal_Death", "Slime_Hungry_Death", "Slime_Afraid_Death",
                "Slime_Normal_Death"
            };
            var actors = new List<SlimeActor>();
            try
            {
                for (var i = 0; i < statuses.Length; i++)
                {
                    var instance = Object.Instantiate(prefab);
                    var actor = instance.GetComponent<SlimeActor>();
                    actor.Initialize(config, new ActorId("death-animation-test-" + i),
                        new Vector2(i * 2f, 0f), new FactionId("slime"));
                    if (!string.IsNullOrEmpty(statuses[i]))
                        actor.ApplyResolvedEffect(new ResolvedEffect(EffectType.Status, 0f,
                            statusId: statuses[i]));
                    Assert.IsTrue(actor.ApplyResolvedEffect(ResolvedEffect.Damage(10000f)).DidKill);
                    actors.Add(actor);
                }

                yield return null;
                yield return null;
                for (var i = 0; i < actors.Count; i++)
                {
                    var animator = actors[i].GetComponentInChildren<Animator>();
                    Assert.IsNotNull(animator);
                    var clips = animator.GetCurrentAnimatorClipInfo(0);
                    Assert.IsNotEmpty(clips);
                    Assert.AreEqual(expectedClips[i], clips[0].clip.name,
                        statuses[i] ?? "Normal");
                }

                yield return new WaitForSeconds(4.75f);
                foreach (var actor in actors)
                {
                    var sprite = actor.GetComponentInChildren<SpriteRenderer>();
                    Assert.AreEqual(1f, sprite.color.a, .02f,
                        "Death presentation should stay visible until its clip ends.");
                }

                yield return new WaitForSeconds(.5f);
                foreach (var actor in actors)
                {
                    var sprite = actor.GetComponentInChildren<SpriteRenderer>();
                    Assert.Less(sprite.color.a, .99f,
                        "The existing fade should start after the death sequence.");
                    Assert.Greater(sprite.color.a, 0f);
                }

                yield return new WaitForSeconds(.2f);
                foreach (var actor in actors)
                {
                    var sprite = actor.GetComponentInChildren<SpriteRenderer>();
                    var animator = actor.GetComponentInChildren<Animator>();
                    Assert.IsFalse(sprite.enabled, "The sprite should be hidden after the fade.");
                    Assert.IsFalse(animator.enabled,
                        "The Animator should stop after the death animation and fade finish.");
                }
            }
            finally
            {
                foreach (var actor in actors)
                    if (actor != null) Object.Destroy(actor.gameObject);
            }
            yield return null;
        }
    }
}
