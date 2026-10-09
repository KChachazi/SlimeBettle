namespace SlimeBattle.World
{
    public readonly struct OccupancyQuery
    {
        public OccupancyQuery(float radius = 0f, bool requireWithinBounds = true,
            bool checkStaticBlockers = true)
        {
            Radius = radius;
            RequireWithinBounds = requireWithinBounds;
            CheckStaticBlockers = checkStaticBlockers;
        }

        public float Radius { get; }
        public bool RequireWithinBounds { get; }
        public bool CheckStaticBlockers { get; }

        public bool IsValid => !float.IsNaN(Radius) && !float.IsInfinity(Radius) && Radius >= 0f;
    }
}
