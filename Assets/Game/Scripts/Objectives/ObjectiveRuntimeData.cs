/* ObjectiveRuntimeData 保存一次目标实例的结果状态和关联武器。 */
namespace SlimeBattle.Objectives
{
    public sealed class ObjectiveRuntimeData
    {
        internal ObjectiveRuntimeData(
            ObjectiveId objectiveId,
            WeaponRuntimeData weapon,
            bool hasSuccessCondition)
        {
            ObjectiveId = objectiveId;
            Weapon = weapon;
            HasSuccessCondition = hasSuccessCondition;
            Phase = ObjectivePhase.Pending;
            ResultReason = ObjectiveResultReason.None;
        }

        public ObjectiveId ObjectiveId { get; }
        public WeaponRuntimeData Weapon { get; }
        public bool HasSuccessCondition { get; }
        public ObjectivePhase Phase { get; internal set; }
        public ObjectiveResultReason ResultReason { get; internal set; }
        public bool IsSettled => Phase != ObjectivePhase.Pending;

        public ObjectiveSnapshot GetSnapshot()
        {
            return new ObjectiveSnapshot(
                ObjectiveId,
                Phase,
                IsSettled,
                ResultReason,
                Weapon.WeaponId);
        }
    }
}
