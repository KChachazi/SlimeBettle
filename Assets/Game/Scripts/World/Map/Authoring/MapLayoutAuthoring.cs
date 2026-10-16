using UnityEngine;

namespace SlimeBattle.World
{
    /// <summary>Scene binding that exposes an offline map definition to MapManager.</summary>
    [DisallowMultipleComponent]
    public sealed class MapLayoutAuthoring : MonoBehaviour
    {
        [SerializeField] private FirstLevelMapDefinition definition;

        internal FirstLevelMapDefinition Definition => definition;

        internal bool TryGetSnapshot(out MapLayoutSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (definition == null)
            {
                error = "Map layout definition is not assigned.";
                return false;
            }
            return definition.TryBuildSnapshot(out snapshot, out error);
        }
    }
}
