/* 主菜单按钮的场景入口：只转发请求，不管理关卡内部数据。 */
using UnityEngine;

namespace SlimeBattle.Core
{
    public sealed class MainMenuLevelEntry : MonoBehaviour
    {
        public void StartLevelTest()
        {
            StartLevel("Level_test");
        }

        public void StartFirstLevel()
        {
            StartLevel("FirstLevel");
        }

        public void StartCastleLevel()
        {
            StartLevel("Castle");
        }

        private void StartLevel(string levelName)
        {
            var root = FindObjectOfType<GameRoot>();
            if (root == null)
            {
                Debug.LogError("MainMenu: GameRoot is missing.", this);
                return;
            }

            root.RequestEnterLevel(new LevelId(levelName));
        }
    }
}
