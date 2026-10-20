namespace SlimeBattle.World
{
    public readonly struct OccupancyQuery
    {
        public OccupancyQuery(float radius = 0f, bool requireWithinBounds = true,
            bool checkStaticBlockers = true, float roadTolerance = 0f,
            bool ignoreRoadArea = false)
        {
            Radius = radius;
            RequireWithinBounds = requireWithinBounds;
            CheckStaticBlockers = checkStaticBlockers;
            RoadTolerance = roadTolerance;
            IgnoreRoadArea = ignoreRoadArea;
        }

        public float Radius { get; }
        public bool RequireWithinBounds { get; }
        public bool CheckStaticBlockers { get; }
        /// <summary>Small allowance for measurement seams between adjoining road polygons.</summary>
        public float RoadTolerance { get; }
        /// <summary>Whether Road-area membership is skipped for this query.</summary>
        public bool IgnoreRoadArea { get; }

        public bool IsValid => IsFiniteNonNegative(Radius) && IsFiniteNonNegative(RoadTolerance);

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
