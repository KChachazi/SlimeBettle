namespace SlimeBattle.World
{
    public readonly struct OccupancyQuery
    {
        public OccupancyQuery(float radius = 0f, bool requireWithinBounds = true,
            bool checkStaticBlockers = true, float roadTolerance = 0f)
        {
            Radius = radius;
            RequireWithinBounds = requireWithinBounds;
            CheckStaticBlockers = checkStaticBlockers;
            RoadTolerance = roadTolerance;
        }

        public float Radius { get; }
        public bool RequireWithinBounds { get; }
        public bool CheckStaticBlockers { get; }
        /// <summary>Small allowance for measurement seams between adjoining road polygons.</summary>
        public float RoadTolerance { get; }

        public bool IsValid => IsFiniteNonNegative(Radius) && IsFiniteNonNegative(RoadTolerance);

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
