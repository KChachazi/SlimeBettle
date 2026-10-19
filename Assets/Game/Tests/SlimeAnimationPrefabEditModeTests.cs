using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.Actors.Slimes;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace SlimeBattle.Tests
{
    public sealed class SlimeAnimationPrefabEditModeTests
    {
        private const string AnimationRoot = "Assets/Game/Animations/Slime";
        private const string PrefabPath = "Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab";

        [Test]
        public void NormalControllerAndStateClipsHaveCompleteFrameData()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                AnimationRoot + "/AC_Slime_Normal.controller");
            Assert.IsNotNull(controller);
            Assert.AreEqual(8, controller.layers[0].stateMachine.states.Length);

            var actions = new[] { "Idle", "Move", "Attack", "Eat", "Furnace", "Hit", "Collision" };
            foreach (var action in actions)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    AnimationRoot + "/Clips/Slime_Normal_" + action + ".anim");
                AssertClip(clip, action == "Idle" || action == "Move");
            }

            var normalDeath = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationRoot + "/Clips/State_Default/Slime_Normal_Death.anim");
            AssertClip(normalDeath, false);
            Assert.IsTrue(HasStateMotion(controller, "Death", normalDeath));
        }

        [Test]
        public void AvailableStatusActionsAndDeathsAreMappedAndMissingActionsFallBack()
        {
            AssertStatusOverrides("Afraid", new[]
            {
                "Idle", "Move", "Attack", "Eat", "Furnace", "Hit", "Collision", "Death"
            });
            AssertStatusOverrides("Hungry", new[]
            {
                "Idle", "Move", "Attack", "Eat", "Furnace", "Hit", "Collision", "Death"
            });
            AssertStatusOverrides("Agitated", new[]
            {
                "Move", "Attack", "Eat", "Furnace", "Hit", "Collision"
            });
            AssertNormalFallback("Agitated", "Idle");
            AssertNormalFallback("Agitated", "Death");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            var presenter = prefab.GetComponent<SlimeVisualPresenter>();
            Assert.IsNotNull(presenter);
            var serializedPresenter = new SerializedObject(presenter);
            var stateOverrides = serializedPresenter.FindProperty("stateOverrides");
            Assert.AreEqual(3, stateOverrides.arraySize);
            AssertPrefabOverride(stateOverrides, 0, 4, "AC_Slime_Afraid");
            AssertPrefabOverride(stateOverrides, 1, 1, "AC_Slime_Hungry");
            AssertPrefabOverride(stateOverrides, 2, 5, "AC_Slime_Agitated");
        }

        [Test]
        public void AnimationAtlasesContainTheirFramesAndAreIncludedInBuild()
        {
            AssertAtlas("SA_Slime_Normal", 840);
            AssertAtlas("SA_Slime_Normal_Death", 120);
            AssertAtlas("SA_Slime_Hungry", 960);
            AssertAtlas("SA_Slime_Afraid", 960);
            AssertAtlas("SA_Slime_Agitated", 720);
        }

        [Test]
        public void PrefabKeepsGameplayScaleAndOriginalSpriteColor()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            var slime = prefab.GetComponent<SlimeActor>();
            Assert.IsNotNull(slime);
            Assert.AreEqual(64f, slime.GameplayPixelsPerUnit);

            var visual = prefab.transform.Find("Visual");
            Assert.IsNotNull(visual);
            Assert.AreEqual(new Vector3(.45f, .45f, .45f), visual.localScale);
            Assert.AreEqual(Color.white, visual.GetComponent<SpriteRenderer>().color);
        }

        private static void AssertStatusOverrides(string status, string[] actions)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                AnimationRoot + "/AC_Slime_" + status + ".overrideController");
            Assert.IsNotNull(controller);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);

            foreach (var action in actions)
            {
                var clipPath = AnimationRoot + "/Clips/State_" + status + "/Slime_" +
                    status + "_" + action + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                AssertClip(clip, action == "Idle" || action == "Move");
                Assert.IsTrue(HasOverride(overrides, "Slime_Normal_" + action, clip),
                    status + " should override " + action + ".");
            }
        }

        private static void AssertNormalFallback(string status, string action)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                AnimationRoot + "/AC_Slime_" + status + ".overrideController");
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            for (var i = 0; i < overrides.Count; i++)
            {
                if (overrides[i].Key == null || overrides[i].Key.name != "Slime_Normal_" + action)
                    continue;
                Assert.IsTrue(overrides[i].Value == null || overrides[i].Value == overrides[i].Key,
                    status + " " + action + " should use its normal fallback.");
                return;
            }
            Assert.Fail("No normal clip slot was found for " + status + " " + action + ".");
        }

        private static bool HasOverride(
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides,
            string baseClipName, AnimationClip replacement)
        {
            for (var i = 0; i < overrides.Count; i++)
                if (overrides[i].Key != null && overrides[i].Key.name == baseClipName &&
                    overrides[i].Value == replacement) return true;
            return false;
        }

        private static void AssertClip(AnimationClip clip, bool shouldLoop)
        {
            Assert.IsNotNull(clip);
            Assert.AreEqual(24f, clip.frameRate);
            Assert.AreEqual(5f, clip.length, .01f);
            Assert.AreEqual(shouldLoop, AnimationUtility.GetAnimationClipSettings(clip).loopTime);
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.AreEqual(1, bindings.Length);
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            Assert.AreEqual(120, frames.Length);
            foreach (var frame in frames) Assert.IsNotNull(frame.value);
        }

        private static bool HasStateMotion(AnimatorController controller,
            string name, AnimationClip clip)
        {
            foreach (var state in controller.layers[0].stateMachine.states)
                if (state.state.name == name && state.state.motion == clip) return true;
            return false;
        }

        private static void AssertPrefabOverride(SerializedProperty overrides, int index,
            int stateValue, string controllerName)
        {
            var element = overrides.GetArrayElementAtIndex(index);
            Assert.AreEqual(stateValue, element.FindPropertyRelative("state").intValue);
            var controller = element.FindPropertyRelative("controller").objectReferenceValue;
            Assert.IsNotNull(controller);
            Assert.AreEqual(controllerName, controller.name);
        }

        private static void AssertAtlas(string name, int expectedSpriteCount)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(
                AnimationRoot + "/Atlases/" + name + ".spriteatlas");
            Assert.IsNotNull(atlas);
            Assert.AreEqual(expectedSpriteCount, atlas.spriteCount, name);
            Assert.IsTrue(SpriteAtlasExtensions.IsIncludeInBuild(atlas), name);
        }
    }
}
