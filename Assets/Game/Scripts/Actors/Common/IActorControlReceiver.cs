/* StatusManager 已确认的控制模式由个体接收并同步中断其行动。 */
namespace SlimeBattle.Actors
{
    public interface IActorControlReceiver
    {
        void SetControlMode(ActorControlMode mode);
    }
}
