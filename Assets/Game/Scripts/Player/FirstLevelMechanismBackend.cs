using System.Collections.Generic;
using SlimeBattle.Core;
using SlimeBattle.Player.Placement;
using UnityEngine;

namespace SlimeBattle.Player
{
    /// <summary>Drag this component into the presentation backend slot, never a plain C# system.</summary>
    public sealed class FirstLevelMechanismBackend : MonoBehaviour, IPlacementBackend,
        IAnchorReplacementBackend
    {
        [SerializeField] private LevelManager levelManager;
        private LevelContext boundContext;
        private MechanismPlacementBackend backend;

        public void Initialize(LevelManager manager)
        {
            levelManager = manager;
            boundContext = null;
            backend = null;
        }

        private MechanismPlacementBackend Current
        {
            get
            {
                if (levelManager == null) { levelManager = FindObjectOfType<LevelManager>(); }
                var context = levelManager != null ? levelManager.GetCurrentContext() : null;
                if (backend == null || context != boundContext)
                {
                    boundContext = context;
                    backend = new MechanismPlacementBackend(context?.MechanismSystem);
                }
                return backend;
            }
        }
        public bool IsAvailable => isActiveAndEnabled && Current.IsAvailable;
        public bool SupportsAnchorReplacement => IsAvailable && Current.SupportsAnchorReplacement;
        public PlacementResult RequestPlacement(PlacementRequest request) => IsAvailable
            ? Current.RequestPlacement(request) : new PlacementResult(false, default, "机关后端未接入或已禁用");
        public PlacementBudgetSnapshot GetBudgetSnapshot() => isActiveAndEnabled ? Current.GetBudgetSnapshot() : default;
        public IReadOnlyList<PlacementObjectSnapshot> ReadPlacedObjects() => Current.ReadPlacedObjects();
        private void OnDisable() { boundContext = null; backend = null; }
    }
}
