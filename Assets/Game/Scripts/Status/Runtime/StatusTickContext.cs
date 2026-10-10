namespace SlimeBattle.Status
{
    public readonly struct StatusTickContext
    {
        public StatusTickContext(float deltaTime)
        {
            DeltaTime = deltaTime;
        }

        public float DeltaTime { get; }
    }
}
