using SlimeBattle.Actors.Spawning;
using SlimeBattle.Core;
using SlimeBattle.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeBattle.Tests
{
    /// <summary>One-time editor authoring for the portable production-code integration scene.</summary>
    public static class FirstLevelCompositionSceneBuilder
    {
        private const string Folder = "Assets/Game/Tests/FirstLevelComposition/";

        [MenuItem("SlimeBattle/Build First Level Composition Test Scene")]
        public static void Build()
        {
            var guardConfig = Required<GuardConfig>(
                Folder + "CFG_Guard_FirstLevelComposition_TEST.asset");
            guardConfig.SetSpawnOffsets(new[] { new Vector2(0f, -1.2f) });
            EditorUtility.SetDirty(guardConfig);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var systems = new GameObject("SystemsRoot");
            var manager = new GameObject("LevelManager").AddComponent<LevelManager>();
            manager.transform.SetParent(systems.transform);
            var composition = systems.AddComponent<FirstLevelCompositionRoot>();
            var mapPrefab = Required<GameObject>(
                "Assets/Game/Tests/FirstLevelMap/PF_FirstLevelMapData_TEST.prefab");
            var mapRoot = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, scene);
            mapRoot.name = "MapRoot";
            var sceneryPrefab = Required<GameObject>(
                "Assets/Game/UI/Prefabs/PF_FirstLevelScenery.prefab");
            var scenery = (GameObject)PrefabUtility.InstantiatePrefab(sceneryPrefab, scene);
            scenery.name = "SceneryRoot";
            var entities = new GameObject("EntitiesRoot");
            var mechanisms = new GameObject("MechanismsRoot");
            var playerPrefab = Required<GameObject>(
                "Assets/Game/UI/Prefabs/PF_FirstLevelPlayer.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "PresentationRoot";

            var configs = new ConfigAsset[]
            {
                Required<LevelConfig>(Folder + "CFG_Level_FirstLevelComposition_TEST.asset"),
                Required<SlimeConfig>("Assets/Game/Configs/Level_test/CFG_Slime_MVP.asset"),
                Required<EnemyConfig>("Assets/Game/Configs/Level_test/CFG_Enemy_MVP.asset"),
                Required<ConfigAsset>("Assets/Game/Configs/Level_test/CFG_Objective_Test.asset"),
                Required<ConfigAsset>("Assets/Game/Configs/Level_test/CFG_EscortWeapon_Test.asset"),
                guardConfig,
                Required<MechanismConfig>("Assets/Game/Configs/FirstLevelMechanisms/CFG_Food.asset"),
                Required<MechanismConfig>("Assets/Game/Configs/FirstLevelMechanisms/CFG_Furnace.asset"),
                Required<MechanismConfig>("Assets/Game/Configs/FirstLevelMechanisms/CFG_Obstacle.asset"),
                Required<ConfigAsset>("Assets/Game/Configs/Status/CFG_Status_Hungry.asset"),
                Required<ConfigAsset>("Assets/Game/Configs/Status/CFG_Status_Agitated.asset"),
                Required<ConfigAsset>("Assets/Game/Configs/Status/CFG_Status_Afraid.asset"),
                Required<ConfigAsset>("Assets/Game/Configs/Status/CFG_Status_Cold.asset"),
                Required<ConfigAsset>("Assets/Game/Configs/Status/CFG_Status_Frozen.asset"),
                Required<ConfigAsset>("Assets/Game/Configs/Status/CFG_Status_Hot.asset"),
                Required<ConfigAsset>("Assets/Game/Configs/Status/CFG_Status_Burning.asset")
            };
            var managerFields = new SerializedObject(manager);
            var configField = managerFields.FindProperty("levelConfigs");
            configField.arraySize = configs.Length;
            for (var i = 0; i < configs.Length; i++)
                configField.GetArrayElementAtIndex(i).objectReferenceValue = configs[i];
            managerFields.FindProperty("slimePrefab").objectReferenceValue =
                Required<GameObject>("Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab")
                    .GetComponent<SlimeBattle.Actors.Slimes.SlimeActor>();
            managerFields.FindProperty("enemyPrefab").objectReferenceValue =
                Required<GameObject>("Assets/Game/Prefabs/Actors/PF_Enemy_Basic.prefab")
                    .GetComponent<SlimeBattle.Actors.Enemies.EnemyActor>();
            managerFields.FindProperty("guardPrefab").objectReferenceValue =
                Required<GameObject>("Assets/Game/Prefabs/Actors/PF_Guard.prefab")
                    .GetComponent<SlimeBattle.Actors.Guards.GuardActor>();
            managerFields.ApplyModifiedPropertiesWithoutUndo();

            var fields = new SerializedObject(composition);
            fields.FindProperty("levelManager").objectReferenceValue = manager;
            fields.FindProperty("slimeConfig").objectReferenceValue = configs[1];
            fields.FindProperty("enemyConfig").objectReferenceValue = configs[2];
            fields.FindProperty("mapRoot").objectReferenceValue = mapRoot.transform;
            fields.FindProperty("entitiesRoot").objectReferenceValue = entities.transform;
            fields.FindProperty("mechanismsRoot").objectReferenceValue = mechanisms.transform;
            fields.FindProperty("presentationRoot").objectReferenceValue = player.transform;
            fields.FindProperty("startCurrentSceneOnPlay").boolValue = true;
            fields.FindProperty("slimeWanderRadius").floatValue = 1.5f;
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, Folder + "FirstLevelComposition_TEST.unity");
            AssetDatabase.SaveAssets();
        }

        private static T Required<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new System.InvalidOperationException("Missing " + path);
            return asset;
        }
    }
}
