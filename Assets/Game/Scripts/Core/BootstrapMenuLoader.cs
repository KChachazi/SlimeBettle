/* Bootstrap 只负责进入主菜单；关卡仍由 GameRoot/LevelManager 加载。 */
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Core
{
    public sealed class BootstrapMenuLoader : MonoBehaviour
    {
        private void Start()
        {
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        }
    }
}
