/* 单只史莱姆专属的决策输入；生命、状态和位置只保存在 ActorCore。 */
using SlimeBattle.Actors;
using SlimeBattle.Actors.SlimeGroups;
using UnityEngine;

namespace SlimeBattle.Actors.Slimes
{
    public sealed class SlimeRuntimeData
    {
        public ActorId TargetId { get; internal set; }
        public GroupId GroupId { get; internal set; }
        public Vector2 GroupTarget { get; internal set; }
        public Vector2 WanderCenter { get; internal set; }
        public bool HasWanderCenter { get; internal set; }
    }
}
