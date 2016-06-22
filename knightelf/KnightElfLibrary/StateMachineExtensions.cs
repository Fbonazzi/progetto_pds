using System.Windows.Input;
using Stateless;

namespace KnightElfLibrary
{
    public static class StateMachineExtensions
  {
    public static ICommand CreateCommand<TState, TTrigger>(this StateMachine<TState, TTrigger> stateMachine, TTrigger trigger)
    {
      return new RelayCommand
        (
          () => stateMachine.Fire(trigger),
          () => stateMachine.CanFire(trigger)
        );
    }
  }
}
