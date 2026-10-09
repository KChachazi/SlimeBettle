/* 感知快照只保存一次采样所得的事实；列表会在构造时复制。 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBattle.Actors.AI.DecisionTree
{
    public readonly struct TargetObservation
    {
        public TargetObservation(ActorSnapshot actor, bool isHostile, bool isVisible = true,
            bool isHuman = false)
        {
            Actor = actor;
            IsHostile = isHostile;
            IsVisible = isVisible;
            IsHuman = isHuman;
        }

        public ActorSnapshot Actor { get; }
        public bool IsHostile { get; }
        public bool IsVisible { get; }
        public bool IsHuman { get; }
    }

    public enum GuidanceKind { Food, Warmth, Wander }

    public readonly struct GuidanceObservation
    {
        public GuidanceObservation(GuidanceKind kind, Vector2 position)
        {
            Kind = kind;
            Position = position;
        }
        public GuidanceKind Kind { get; }
        public Vector2 Position { get; }
    }

    public readonly struct PerceptionSnapshot
    {
        public PerceptionSnapshot(long sampleTick, IReadOnlyList<TargetObservation> targets,
            bool hasDestination = false, Vector2 destination = default(Vector2),
            IReadOnlyList<GuidanceObservation> guides = null,
            bool hasEscapeDestination = false, Vector2 escapeDestination = default(Vector2),
            bool fleeCheckSucceeded = false)
        {
            SampleTick = sampleTick;
            var copy = new List<TargetObservation>();
            if (targets != null)
            {
                for (var i = 0; i < targets.Count; i++) copy.Add(targets[i]);
            }
            Targets = copy.AsReadOnly();
            var guideCopy = new List<GuidanceObservation>();
            if (guides != null)
            {
                for (var i = 0; i < guides.Count; i++) guideCopy.Add(guides[i]);
            }
            Guides = guideCopy.AsReadOnly();
            HasDestination = hasDestination;
            Destination = destination;
            HasEscapeDestination = hasEscapeDestination;
            EscapeDestination = escapeDestination;
            FleeCheckSucceeded = fleeCheckSucceeded;
        }

        public long SampleTick { get; }
        public IReadOnlyList<TargetObservation> Targets { get; }
        public IReadOnlyList<GuidanceObservation> Guides { get; }
        public bool HasDestination { get; }
        public Vector2 Destination { get; }
        public bool HasEscapeDestination { get; }
        public Vector2 EscapeDestination { get; }
        // 概率结算结果由上层输入；决策树只判断是否走逃避分支。
        public bool FleeCheckSucceeded { get; }
    }
}
