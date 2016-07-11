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
        WaitClientConnect, Connected, Paused
    }
    public enum SMTriggers
    {
        SetConnection, SaveConnection, CancelConnection,
        EditConnection, EndEditConnection,
        Connect, ClientConnected, IntWaitClient, Disconnect,
        Run, Pause
    }

    class StateMachine : Stateless.StateMachine<SMStates, SMTriggers>, INotifyPropertyChanged
    {
        public StateMachine(Action setConnectionAction,
                                Action connectAction,
                                Action disconnectAction,
                                Action intWaitAction) : base(SMStates.Start)
        {
            Configure(SMStates.Start)
                //.OnEntryFrom(SMTriggers.IntWaitClient, intWaitAction)
                .Permit(SMTriggers.SetConnection, SMStates.SettingConnection);

            Configure(SMStates.SettingConnection)
                .OnEntry(setConnectionAction)
                .Permit(SMTriggers.SaveConnection, SMStates.Ready)
                .Permit(SMTriggers.CancelConnection, SMStates.Start);

            Configure(SMStates.EditingConnection)
                .SubstateOf(SMStates.SettingConnection)
                .Permit(SMTriggers.CancelConnection, SMStates.Ready);

            Configure(SMStates.Ready)
                .OnEntryFrom(SMTriggers.IntWaitClient, intWaitAction)
                .Permit(SMTriggers.SetConnection, SMStates.EditingConnection)
                .Permit(SMTriggers.Connect, SMStates.WaitClientConnect);

            Configure(SMStates.WaitClientConnect)
                .OnEntry(connectAction)
                .Permit(SMTriggers.ClientConnected, SMStates.Connected)
                .Permit(SMTriggers.IntWaitClient, SMStates.Ready);

            Configure(SMStates.Connected)
                .Permit(SMTriggers.Disconnect, SMStates.Ready)
                .OnExit(disconnectAction);

            Configure(SMStates.Paused)
                .SubstateOf(SMStates.Connected)
                .Permit(SMTriggers.Connect, SMStates.Connected)
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
