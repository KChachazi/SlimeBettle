/* 行动意图是决策输出，不表示移动或命中已经发生。 */
using SlimeBattle.Actors;
using UnityEngine;

namespace SlimeBattle.Actors.AI.DecisionTree
{
    public enum ActionIntentType { None, Wait, Move, Attack }

    public readonly struct ActionIntent
    {
        private ActionIntent(ActionIntentType type, Vector2 destination, AttackIntent attack, string reason)
        {
            Type = type;
            Destination = destination;
            Attack = attack;
            Reason = reason ?? string.Empty;
        }

        public ActionIntentType Type { get; }
        public Vector2 Destination { get; }
        public AttackIntent Attack { get; }
        // 供调试和验收展示决策分支；执行端仍只消费 Type 与目标。
        public string Reason { get; }
        public bool IsValid => Type != ActionIntentType.None;

        public static ActionIntent Wait(string reason = "") => new ActionIntent(ActionIntentType.Wait, default(Vector2), default(AttackIntent), reason);
        public static ActionIntent Move(Vector2 destination, string reason = "") => new ActionIntent(ActionIntentType.Move, destination, default(AttackIntent), reason);
        public static ActionIntent AttackTarget(AttackIntent attack, string reason = "") => attack.IsValid
            ? new ActionIntent(ActionIntentType.Attack, default(Vector2), attack, reason)
            : default(ActionIntent);
    }
}
