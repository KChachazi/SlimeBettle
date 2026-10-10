/* 决策树读取的状态 ID；添加/移除状态仍由结算端负责。 */
namespace SlimeBattle.Actors.Slimes
{
    public static class SlimeStatusIds
    {
        public const string Hungry = "Hungry";
        public const string Cold = "Cold";
        public const string Afraid = "Afraid";
        public const string Agitated = "Agitated";
        public const string Hot = "Hot";
        public const string Frozen = "Frozen";
        public const string Burning = "Burning";

        internal static bool Contains(System.Collections.Generic.IReadOnlyList<string> statusIds, string id)
        {
            if (statusIds == null) return false;
            for (var i = 0; i < statusIds.Count; i++)
                if (statusIds[i] == id) return true;
            return false;
        }
    }
}
