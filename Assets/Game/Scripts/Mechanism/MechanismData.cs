using SlimeBattle.Actors;
using SlimeBattle.Data;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    public readonly struct MechanismTriggerContext
    {
        public MechanismTriggerContext(MechanismId mechanismId, ActorId actorId,
            ActorType actorType, MechanismContactKind contactKind,
            Vector2 actorPosition, int contactColliderId = 0)
        {
            MechanismId = mechanismId;
            ActorId = actorId;
            ActorType = actorType;
            ContactKind = contactKind;
            ActorPosition = actorPosition;
            ContactColliderId = contactColliderId;
        }

        public MechanismId MechanismId { get; }
        public ActorId ActorId { get; }
        public ActorType ActorType { get; }
        public MechanismContactKind ContactKind { get; }
        public Vector2 ActorPosition { get; }
        public int ContactColliderId { get; }
        public bool IsValid => MechanismId.IsValid && ActorId.IsValid &&
            System.Enum.IsDefined(typeof(ActorType), ActorType) &&
            System.Enum.IsDefined(typeof(MechanismContactKind), ContactKind);
    }

    public readonly struct MechanismActivationRequest
    {
        public MechanismActivationRequest(ActorId actorId,
            MechanismActivationKind activationKind = MechanismActivationKind.Manual,
            Vector2 position = default(Vector2))
        {
            ActorId = actorId;
            ActivationKind = activationKind;
            Position = position;
        }

        public ActorId ActorId { get; }
        public MechanismActivationKind ActivationKind { get; }
        public Vector2 Position { get; }
        public bool IsValid => ActorId.IsValid &&
            System.Enum.IsDefined(typeof(MechanismActivationKind), ActivationKind);
    }

    public readonly struct MechanismActivationResult
    {
        public MechanismActivationResult(MechanismId mechanismId, ActorId actorId,
            bool isAccepted, bool consumed, string reason)
        {
            MechanismId = mechanismId;
            ActorId = actorId;
            IsAccepted = isAccepted;
            Consumed = consumed;
            Reason = reason ?? string.Empty;
        }

        public MechanismId MechanismId { get; }
        public ActorId ActorId { get; }
        public bool IsAccepted { get; }
        public bool Consumed { get; }
        public string Reason { get; }
    }

    public readonly struct MechanismSnapshot
    {
        public MechanismSnapshot(MechanismId mechanismId, ConfigId configId,
            MechanismKind kind, Vector2 position, MechanismState state,
            bool isPlayerPlaced, int activationLimit, int remainingActivations,
            float cooldownRemainingSeconds, GuideSourceType guideSourceType,
            float attractionRadiusPixels, PlacementAnchorId anchorId = default,
            float rotationDegrees = 0f)
        {
            MechanismId = mechanismId;
            ConfigId = configId;
            Kind = kind;
            Position = position;
            State = state;
            IsPlayerPlaced = isPlayerPlaced;
            ActivationLimit = activationLimit;
            RemainingActivations = remainingActivations;
            CooldownRemainingSeconds = cooldownRemainingSeconds;
            GuideSourceType = guideSourceType;
            AttractionRadiusPixels = attractionRadiusPixels;
            AnchorId = anchorId;
            RotationDegrees = rotationDegrees;
        }

        public MechanismId MechanismId { get; }
        public ConfigId ConfigId { get; }
        public MechanismKind Kind { get; }
        public Vector2 Position { get; }
        public MechanismState State { get; }
        public bool IsPlayerPlaced { get; }
        public int ActivationLimit { get; }
        public int RemainingActivations { get; }
        public float CooldownRemainingSeconds { get; }
        public GuideSourceType GuideSourceType { get; }
        public float AttractionRadiusPixels { get; }
        public PlacementAnchorId AnchorId { get; }
        public float RotationDegrees { get; }
        public bool HasUnlimitedActivations => ActivationLimit == 0;
        public bool IsAvailable => State == MechanismState.Available;
    }

    public readonly struct GuideSourceSnapshot
    {
        public GuideSourceSnapshot(GuideSourceType type, MechanismId mechanismId,
            MapAreaId areaId, ConfigId configId, Vector2 position,
            float attractionRadiusPixels, bool isAvailable, bool isMapAreaSource)
        {
            Type = type;
            MechanismId = mechanismId;
            AreaId = areaId;
            ConfigId = configId;
            Position = position;
            AttractionRadiusPixels = attractionRadiusPixels;
            IsAvailable = isAvailable;
            IsMapAreaSource = isMapAreaSource;
        }

        public GuideSourceType Type { get; }
        public MechanismId MechanismId { get; }
        public MapAreaId AreaId { get; }
        public ConfigId ConfigId { get; }
        public Vector2 Position { get; }
        public float AttractionRadiusPixels { get; }
        public bool IsAvailable { get; }
        public bool IsMapAreaSource { get; }
    }

    public readonly struct MechanismPlacementRequest
    {
        public MechanismPlacementRequest(MechanismAuthoring authoring, Vector2 position)
        {
            Authoring = authoring;
            Position = position;
        }

        public MechanismAuthoring Authoring { get; }
        public Vector2 Position { get; }
        public bool IsValid => Authoring != null && IsFinite(Position.x) && IsFinite(Position.y);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct MechanismPlacementResult
    {
        public MechanismPlacementResult(bool succeeded, MechanismId mechanismId, string reason)
        {
            Succeeded = succeeded;
            MechanismId = mechanismId;
            Reason = reason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public MechanismId MechanismId { get; }
        public string Reason { get; }
    }

    public readonly struct PlacementCooldown
    {
        public PlacementCooldown(ConfigId configId, float remainingSeconds)
        {
            ConfigId = configId;
            RemainingSeconds = remainingSeconds;
        }

        public ConfigId ConfigId { get; }
        public float RemainingSeconds { get; }
        public bool IsReady => RemainingSeconds <= 0f;
    }
}
