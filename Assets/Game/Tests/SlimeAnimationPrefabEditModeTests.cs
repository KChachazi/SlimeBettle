using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SlimeBattle.Tests
{
    public sealed class SlimeAnimationPrefabEditModeTests
    {
        [Test]
        public void GeneratedSlimeAnimationAssetsHaveCompleteSerializedFrameData()
        {
            var controller = ReadAsset("Assets/Game/Animations/Slime/AC_Slime_Normal.controller");
            var atlas = ReadAsset("Assets/Game/Animations/Slime/Atlases/SA_Slime_Normal.spriteatlas");
            var prefab = ReadAsset("Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab");
            var states = new[]
            {
                "Idle", "Move", "Attack", "Eat", "Furnace", "Hit", "Collision"
            };

            Assert.AreEqual(7,
                Regex.Matches(controller, "^--- !u!1102 ", RegexOptions.Multiline).Count);
            foreach (var state in states)
            {
                var clipPath = "Assets/Game/Animations/Slime/Clips/Slime_Normal_" + state + ".anim";
                var clip = ReadAsset(clipPath);
                var guid = ReadAsset(clipPath + ".meta").Split('\n')[1].Trim().Substring(6);
                StringAssert.Contains("m_Name: " + state, controller);
                StringAssert.Contains(guid, controller);
                Assert.AreEqual(120,
                    Regex.Matches(clip, "^    - time:", RegexOptions.Multiline).Count);
                StringAssert.Contains("m_SampleRate: 24", clip);
                StringAssert.Contains("m_StopTime: 5", clip);
                var shouldLoop = state == "Idle" || state == "Move";
                StringAssert.Contains("m_LoopTime: " + (shouldLoop ? "1" : "0"), clip);
            }

            Assert.AreEqual(7, Regex.Matches(atlas,
                "^    - \\{fileID: 102900000, guid:", RegexOptions.Multiline).Count);
            Assert.AreEqual(840, Regex.Matches(atlas,
                "^    - [0-9a-f]{32}: 21300000$", RegexOptions.Multiline).Count);
            StringAssert.Contains("m_TextureCompression: 1", atlas);
            StringAssert.Contains("m_Overridden: 1", atlas);
            StringAssert.Contains("gameplayPixelsPerUnit: 64", prefab);
            StringAssert.Contains("m_LocalScale: {x: 0.45, y: 0.45, z: 0.45}", prefab);
            StringAssert.Contains("m_Color: {r: 1, g: 1, b: 1, a: 1}", prefab);
            StringAssert.DoesNotContain("m_Name: Death", controller);
        }

        private static string ReadAsset(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath));
        }
    }
}
