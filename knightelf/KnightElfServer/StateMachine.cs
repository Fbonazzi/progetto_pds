using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace KnightElfServer
{
    public enum SMStates
    {
        Start, Ready,
        SettingConnection, EditingConnection,
        WaitClientConnect, Running, Paused
    }
    public enum SMTriggers
    {
        SetConnection, SaveConnection, CancelConnection,
        EditConnection, EndEditConnection,
        Connect, IntWaitClient, Disconnect,
        Run, Pause
    }

    public class StateMachine : Stateless.StateMachine<SMStates, SMTriggers>, INotifyPropertyChanged
    {
        public StateMachine(Action setConnectionAction,
                                Action connectAction,
                                Action intWaitAction) : base(SMStates.Start)
        {
            Configure(SMStates.Start)
                .Permit(SMTriggers.SetConnection, SMStates.SettingConnection);

            Configure(SMStates.SettingConnection)
                .OnEntry(setConnectionAction)
                .Permit(SMTriggers.SaveConnection, SMStates.Ready)
                .Permit(SMTriggers.CancelConnection, SMStates.Start);

            Configure(SMStates.EditingConnection)
                .SubstateOf(SMStates.SettingConnection)
                .Permit(SMTriggers.CancelConnection, SMStates.Ready);

            Configure(SMStates.Ready)
                .Permit(SMTriggers.SetConnection, SMStates.EditingConnection)
                .Permit(SMTriggers.Connect, SMStates.WaitClientConnect);

            Configure(SMStates.WaitClientConnect)
                .OnEntryFrom(SMTriggers.IntWaitClient, intWaitAction)
                .OnEntryFrom(SMTriggers.Connect, connectAction)
                .Permit(SMTriggers.Run, SMStates.Running)
                .Permit(SMTriggers.Disconnect, SMStates.Ready)
                .PermitReentry(SMTriggers.IntWaitClient);

            Configure(SMStates.Running)
                .Permit(SMTriggers.Disconnect, SMStates.Ready)
                .Permit(SMTriggers.Pause, SMStates.Paused);

            Configure(SMStates.Paused)
                .SubstateOf(SMStates.Running)
                .Permit(SMTriggers.Run, SMStates.Running)
                .Permit(SMTriggers.Disconnect, SMStates.Ready);

            OnTransitioned (
                  (t) =>
                  {
                      OnPropertyChanged("State");
                      CommandManager.InvalidateRequerySuggested();
                      //used to debug commands and UI components
                      Debug.WriteLine("State Machine transitioned from {0} -> {1} [{2}]",
                                        t.Source, t.Destination, t.Trigger);
                  }
            );
        }

        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }



        #endregion
    }
}
