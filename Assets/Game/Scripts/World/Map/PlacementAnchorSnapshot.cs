using System;
using System.Collections.Generic;
using System.Linq;
using SlimeBattle.Mechanisms;
using UnityEngine;

namespace SlimeBattle.World
{
    public readonly struct PlacementAnchorSnapshot
    {
        private readonly IReadOnlyList<MechanismKind> acceptedMechanismKinds;

        public PlacementAnchorSnapshot(PlacementAnchorId anchorId,
            PlacementAnchorRole role, Vector2 position, float snapRadiusPixels,
            IEnumerable<MechanismKind> acceptedKinds, bool allowsReplacement,
            MapRouteId associatedRouteId, MapPointId associatedPointId)
        {
            AnchorId = anchorId;
            Role = role;
            Position = position;
            SnapRadiusPixels = snapRadiusPixels;
            var kinds = acceptedKinds == null
                ? Array.Empty<MechanismKind>()
                : acceptedKinds.ToArray();
            acceptedMechanismKinds = Array.AsReadOnly(kinds);
            AllowsReplacement = allowsReplacement;
            AssociatedRouteId = associatedRouteId;
            AssociatedPointId = associatedPointId;
        }

        public PlacementAnchorId AnchorId { get; }
        public PlacementAnchorRole Role { get; }
        public Vector2 Position { get; }
        public float SnapRadiusPixels { get; }
        public IReadOnlyList<MechanismKind> AcceptedMechanismKinds =>
            acceptedMechanismKinds ?? Array.Empty<MechanismKind>();
        public bool AllowsReplacement { get; }
        public MapRouteId AssociatedRouteId { get; }
        public MapPointId AssociatedPointId { get; }

        public bool Accepts(MechanismKind kind) =>
            AcceptedMechanismKinds.Contains(kind);

        public bool IsValid
        {
            get
            {
                if (!AnchorId.IsValid || !Enum.IsDefined(typeof(PlacementAnchorRole), Role) ||
                    !IsFinite(Position) || !IsFinite(SnapRadiusPixels) || SnapRadiusPixels < 0f ||
                    AcceptedMechanismKinds.Count == 0)
                    return false;

                if (Role == PlacementAnchorRole.RouteBlock && !AssociatedRouteId.IsValid)
                    return false;
                if (Role != PlacementAnchorRole.RouteBlock && !AssociatedPointId.IsValid)
                    return false;

                var seen = new HashSet<MechanismKind>();
                foreach (var kind in AcceptedMechanismKinds)
                    if (!Enum.IsDefined(typeof(MechanismKind), kind) ||
                        kind == MechanismKind.None || !seen.Add(kind)) return false;
                return true;
            }
        }

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
