using System;
using System.Collections.Generic;
using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct StatusSnapshot
    {
        private readonly IReadOnlyList<StatusEntrySnapshot> statuses;

        public StatusSnapshot(ActorId actorId, ActorControlMode controlMode,
            IEnumerable<StatusEntrySnapshot> statuses)
        {
            ActorId = actorId;
            ControlMode = controlMode;
            this.statuses = Array.AsReadOnly(statuses == null
                ? Array.Empty<StatusEntrySnapshot>()
                : new List<StatusEntrySnapshot>(statuses).ToArray());
        }

        public ActorId ActorId { get; }
        public ActorControlMode ControlMode { get; }
        public IReadOnlyList<StatusEntrySnapshot> Statuses => statuses ?? Array.Empty<StatusEntrySnapshot>();
    }
}
