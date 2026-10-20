/* 一次生成独立 Play 场景和同名 LevelConfig；只创建缺失资产，不覆盖手工改过的场景。 */
using System;
using System.Collections.Generic;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Status;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeBattle.Tests.AIBranches
{
    public static class AIBranchSceneBuilder
    {
        private const string Folder = "Assets/Game/Tests/AIBranches";
        private const string ConfigFolder = Folder + "/Configs";
        private const string SceneFolder = Folder + "/Scenes";

        [MenuItem("SlimeBattle/Tests/Generate AI Branch Scenes")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(ConfigFolder);
            EnsureFolder(SceneFolder);
            var slimeConfig = AssetDatabase.LoadAssetAtPath<SlimeConfig>(
                "Assets/Game/Configs/Actors/CFG_Slime_MVP.asset");
            var enemyConfig = AssetDatabase.LoadAssetAtPath<EnemyConfig>(
                "Assets/Game/Configs/Actors/CFG_Enemy_MVP.asset");
            var objective = AssetDatabase.LoadAssetAtPath<ObjectiveConfig>(
                "Assets/Game/Configs/Levels/LevelTest/CFG_Objective_Test.asset");
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponConfig>(
                "Assets/Game/Configs/Levels/LevelTest/CFG_EscortWeapon_Test.asset");
            var slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab")?.GetComponent<SlimeActor>();
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Actors/PF_Enemy_Basic.prefab")?.GetComponent<EnemyActor>();
            if (slimeConfig == null || enemyConfig == null || objective == null || weapon == null ||
                slimePrefab == null || enemyPrefab == null)
                throw new InvalidOperationException("AI branch scene dependencies are missing.");

            var cases = (AIBranchScenario[])Enum.GetValues(typeof(AIBranchScenario));
            var levelConfigs = new List<LevelConfig>();
            foreach (var item in cases)
            {
                var sceneName = "Test_AI_" + item;
                var configPath = ConfigFolder + "/CFG_" + sceneName + ".asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(configPath);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelConfig>();
                    level.SetConfigId(sceneName);
                    level.SetObjectiveConfigId(objective.ConfigId);
                    level.SetMaxAliveSlimes(30);
                    AssetDatabase.CreateAsset(level, configPath);
                }
                levelConfigs.Add(level);
            }
            var registryAssets = new List<ConfigAsset>(levelConfigs)
                { slimeConfig, enemyConfig, objective, weapon };
            // 这些中性测试资产仅提供 AI 前置状态；状态结算仍由正式 StatusManager 执行。
            registryAssets.Add(EnsureStatusConfig("Hungry"));
            registryAssets.Add(EnsureStatusConfig("Cold"));
            registryAssets.Add(EnsureStatusConfig("Afraid"));
            registryAssets.Add(EnsureStatusConfig("Agitated"));
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var item in cases)
            {
                var path = SceneFolder + "/Test_AI_" + item + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    CreateScene(path, item, registryAssets, slimeConfig, enemyConfig,
                        slimePrefab, enemyPrefab);
                if (!buildScenes.Exists(scene => scene.path == path))
                    buildScenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AIBranch] 已准备 " + cases.Length +
                " 个独立场景；在 Assets/Game/Tests/AIBranches/Scenes 任选一个打开后 Play。");
        }

        private static void CreateScene(string path, AIBranchScenario item,
            List<ConfigAsset> configs, SlimeConfig slimeConfig, EnemyConfig enemyConfig,
            SlimeActor slimePrefab, EnemyActor enemyPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GameRoot");
            var gameRoot = root.AddComponent<GameRoot>();
            var managerObject = new GameObject("LevelManager");
            managerObject.transform.SetParent(root.transform);
            var manager = managerObject.AddComponent<LevelManager>();
            var rootFields = new SerializedObject(gameRoot);
            rootFields.FindProperty("levelManager").objectReferenceValue = manager;
            rootFields.ApplyModifiedPropertiesWithoutUndo();
            var managerFields = new SerializedObject(manager);
            var configArray = managerFields.FindProperty("levelConfigs");
            configArray.arraySize = configs.Count;
            for (var i = 0; i < configs.Count; i++)
                configArray.GetArrayElementAtIndex(i).objectReferenceValue = configs[i];
            managerFields.FindProperty("slimePrefab").objectReferenceValue = slimePrefab;
            managerFields.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            managerFields.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(.08f, .1f, .14f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();

            var fixtureObject = new GameObject("AI Branch Fixture");
            var fixture = fixtureObject.AddComponent<AIBranchScene>();
            var slimeRoot = new GameObject("SlimeRoot").transform;
            var enemyRoot = new GameObject("EnemyRoot").transform;
            slimeRoot.SetParent(fixtureObject.transform);
            enemyRoot.SetParent(fixtureObject.transform);
            var fixtureFields = new SerializedObject(fixture);
            fixtureFields.FindProperty("scenario").enumValueIndex = (int)item;
            fixtureFields.FindProperty("slimeConfig").objectReferenceValue = slimeConfig;
            fixtureFields.FindProperty("enemyConfig").objectReferenceValue = enemyConfig;
            fixtureFields.FindProperty("slimeRoot").objectReferenceValue = slimeRoot;
            fixtureFields.FindProperty("enemyRoot").objectReferenceValue = enemyRoot;
            fixtureFields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        private static StatusConfig EnsureStatusConfig(string statusId)
        {
            var path = ConfigFolder + "/CFG_Test_AI_Status_" + statusId + ".asset";
            var config = AssetDatabase.LoadAssetAtPath<StatusConfig>(path);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<StatusConfig>();
            config.SetConfigId("test_ai_status_" + statusId.ToLowerInvariant());
            var serialized = new SerializedObject(config);
            serialized.FindProperty("statusId").stringValue = statusId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(config, path);
            return config;
        }
    }
}
