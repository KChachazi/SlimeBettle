using SlimeBattle.Player.Placement;

namespace SlimeBattle.UI
{
    /// <summary>Presentation wording; backend rejection reasons remain visible verbatim.</summary>
    public static class FirstLevelUiText
    {
        public static string PlacementHint(PlacementSystem placement)
        {
            if (placement.IsRemoving && placement.Hint == "Outside map bounds.") return "超出地图边界，请在地图内选择机关";
            if (placement.IsRemoving && placement.Hint == "Map bounds are not configured.") return "地图边界未接入，暂时无法拆除";
            return Hint(placement.Hint);
        }

        public static string Hint(string message)
        {
            const string limitPrefix = "First-level limit should be 20 (configured: ";
            if (!string.IsNullOrEmpty(message) && message.StartsWith(limitPrefix, System.StringComparison.Ordinal) && message.EndsWith(").", System.StringComparison.Ordinal))
                return "首关配置上限应为 20（当前：" + message.Substring(limitPrefix.Length, message.Length-limitPrefix.Length-2) + "），请由配置负责人核对";
            switch (message)
            {
                case "Guide at least 6 slimes into the goal cave.": return "引导至少 6 只史莱姆进入终点洞穴";
                case "Escort the weapon to the goal.": return "护送武器抵达终点";
                case "Placement module is not connected.": return "放置模块未接入";
                case "Level complete": return "关卡已完成";
                case "Level failed": return "本次引导结束";
                case "Choose Food, Furnace or Obstacle.": return "选择右侧机关，再点击地图放置";
                case "Click/tap a map cell to place it.": return "点击地图中的空格放置 · 取消可退出";
                case "Free cell. Click/tap to place.": return "此格可以放置 · 点击即放下";
                case "This cell is occupied.": return "此格已有机关，请选择其他空格";
                case "Outside map bounds.": return "超出地图边界，请在地图内放置";
                case "Map bounds are not configured.": return "地图边界未接入，暂时无法放置";
                case "Mechanism creation API is not connected.": return "机关创建接口未接入，暂时无法放置";
                case "Placed Food.": return "食物已放下";
                case "Placed Furnace.": return "熔炉已放下";
                case "Placed Obstacle.": return "障碍已放下";
                case "Waiting for objective data": return "等待关卡数据接入";
                case "Placement is only available while the level is running.": return "关卡运行中才可放置";
                default: return message ?? string.Empty;
            }
        }
    }
}
