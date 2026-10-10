using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SlimeBattle.Tests.PlayerPresentation.Editor
{
    public static class P0PlayerTestSetup
    {
        private const string BootstrapPath = "Assets/Game/Tests/PlayerPresentation/Bootstrap_P0Player.unity";
        private const string TestPath = "Assets/Game/Tests/PlayerPresentation/Test_PlayerPresentation.unity";

        [MenuItem("SlimeBattle/Tests/Open P0 Player Bootstrap")]
        public static void OpenBootstrap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            // Opt-in test registration preserves the team's existing scene order.
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in new[] { BootstrapPath, TestPath })
            {
                var existing = scenes.FindIndex(scene => scene.path == path);
                if (existing >= 0) { scenes[existing] = new EditorBuildSettingsScene(path, true); }
                else { scenes.Add(new EditorBuildSettingsScene(path, true)); }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.OpenScene(BootstrapPath);
        }
    }
}
