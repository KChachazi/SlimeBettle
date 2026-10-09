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
            if (root == null) { root = new GameObject("GameRoot").AddComponent<GameRoot>(); }
            root.RequestEnterLevel(new LevelId("Assets/Game/Tests/PlayerPresentation/Test_PlayerPresentation.unity"));
        }
    }
}
