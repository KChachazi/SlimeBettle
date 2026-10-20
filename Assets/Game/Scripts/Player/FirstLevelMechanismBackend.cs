using System.Collections.Generic;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Placement;
using UnityEngine;

namespace SlimeBattle.Player
{
    /// <summary>Drag this component into the presentation backend slot, never a plain C# system.</summary>
    public sealed class FirstLevelMechanismBackend : MonoBehaviour, IPlacementBackend,
        IPlacementPreviewInfoProvider, IAnchorReplacementBackend, IPlacementRemovalBackend
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
        public bool SupportsRemoval => isActiveAndEnabled && Current.SupportsRemoval;
        public bool TryGetAttractionRadiusWorldUnits(PlacementItemId item,
            out float radius)
        {
            radius = 0f;
            return isActiveAndEnabled &&
                Current.TryGetAttractionRadiusWorldUnits(item, out radius);
        }
        public PlacementResult RequestRemoval(MechanismId mechanismId) => SupportsRemoval
            ? Current.RequestRemoval(mechanismId) : new PlacementResult(false, mechanismId, "机关后端未接入或已禁用");
        public PlacementResult RequestPlacement(PlacementRequest request) => IsAvailable
            ? Current.RequestPlacement(request) : new PlacementResult(false, default, "机关后端未接入或已禁用");
        public PlacementBudgetSnapshot GetBudgetSnapshot() => isActiveAndEnabled ? Current.GetBudgetSnapshot() : default;
        public IReadOnlyList<PlacementObjectSnapshot> ReadPlacedObjects() => Current.ReadPlacedObjects();
        private void OnDisable() { boundContext = null; backend = null; }
    }
}
