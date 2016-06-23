using System.Windows.Input;
using Stateless;

namespace KnightElfLibrary
{
    public static class StateMachineExtensions
  {
        /// <summary>
        /// Creates command corresponding to a state machine's trigger.
        /// </summary>
        /// <remarks>
        /// The Execute function will be based on the <see cref="StateMachine.Fire"/> method
        /// provided by the <see cref="Stateless" /> package; OnEntry and OnExit actions are
        /// called depending on state machine's configuration.
        /// 
        /// The CanExecute function will be based on the <see cref="StateMachine.CanFire(TTrigger)"/> method
        /// and implements the state machine logic.
        /// </remarks>
        /// <param name="trigger">The trigger to implement.</param>
        /// <returns>The command corresponding to the given trigger.</returns>
        public static ICommand CreateCommand<TState, TTrigger>(this StateMachine<TState, TTrigger> stateMachine, TTrigger trigger)
    {
      return new RelayCommand
        (
          () => stateMachine.Fire(trigger),     //action to execute, based on machine configured OnEntry, OnExit Actions
          () => stateMachine.CanFire(trigger)   //Execution conditions, based on the actual state
        );
    }
  }
}
