namespace SlimeBattle.World
{
    public readonly struct MapReadyEvent
    {
        public MapReadyEvent(int areaCount, int pointCount, int staticBlockerCount)
        {
            AreaCount = areaCount;
            PointCount = pointCount;
            StaticBlockerCount = staticBlockerCount;
        }

        public int AreaCount { get; }
        public int PointCount { get; }
        public int StaticBlockerCount { get; }
    }
}
