using System.Collections.Generic;
using SlimeBattle.Mechanisms;
using UnityEngine;

namespace SlimeBattle.World
{
    [DisallowMultipleComponent]
    public sealed class PlacementAnchorAuthoring : MonoBehaviour
    {
        [SerializeField] private string anchorId;
        [SerializeField] private PlacementAnchorRole role;
        [SerializeField, Min(0f)] private float snapRadiusPixels = 80f;
        [SerializeField] private List<MechanismKind> acceptedMechanismKinds =
            new List<MechanismKind> { MechanismKind.Obstacle };
        [SerializeField] private bool allowsReplacement = true;
        [SerializeField] private string associatedRouteId;
        [SerializeField] private string associatedPointId;

        internal PlacementAnchorId AnchorId => new PlacementAnchorId(anchorId);

        internal bool TryGetSnapshot(out PlacementAnchorSnapshot snapshot, out string error)
        {
            snapshot = new PlacementAnchorSnapshot(AnchorId, role, transform.position,
                snapRadiusPixels, acceptedMechanismKinds, allowsReplacement,
                new MapRouteId(associatedRouteId), new MapPointId(associatedPointId));
            if (!snapshot.IsValid)
            {
                error = "Anchor ID, role, snap radius, accepted mechanism kinds or association is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
