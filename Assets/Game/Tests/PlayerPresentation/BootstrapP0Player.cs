using SlimeBattle.Core;
using UnityEngine;

namespace SlimeBattle.Tests.PlayerPresentation
{
    /// <summary>Test-only entry point. Does not modify the shared Bootstrap scene.</summary>
    public sealed class BootstrapP0Player : MonoBehaviour
    {
        private void Start()
        {
            var root = FindObjectOfType<GameRoot>();
            if (root == null)
            {
                Debug.LogError("Open Bootstrap_P0Player with its configured GameRoot and LevelManager.", this);
                return;
            }
            root.RequestEnterLevel(new LevelId("Assets/Game/Tests/PlayerPresentation/Test_PlayerPresentation.unity"));
        }
    }
}
