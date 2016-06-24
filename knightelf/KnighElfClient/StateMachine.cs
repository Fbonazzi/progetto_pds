using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace KnightElfClient
{
    public enum SMStates
    {
        Disconnected,
        AddingServer,
        ServerSelected,
        EditingServer,
        Connected
    }
    public enum SMTriggers
    {
        Add, Remove,
        Save, Cancel,
        Select, Deselect,
        Edit, EndEdit,
        Connect, Disconnect, NetworkError
    }

    class StateMachine : Stateless.StateMachine<SMStates, SMTriggers>, INotifyPropertyChanged
    {   
        public StateMachine(    Func<bool> existsServer,
                                Func<bool> isConnectedServer,
                                Action launchAddDlgAction,
                                Action launchEditDlgAction,
                                Action disconnectAction,
                                Action removeServerAction) : base(SMStates.Disconnected)
        {
            Configure(SMStates.Disconnected)
                .Permit(SMTriggers.Add,SMStates.AddingServer)
                .PermitIf(SMTriggers.Select,SMStates.ServerSelected, existsServer)
                .OnEntryFrom(SMTriggers.Remove,removeServerAction); //non sono sicura che funzioni..

            Configure(SMStates.ServerSelected)
                .SubstateOf(SMStates.Disconnected)
                .PermitIf(SMTriggers.Remove,SMStates.Disconnected, existsServer)
                .Permit(SMTriggers.Deselect,SMStates.Disconnected)
                .Permit(SMTriggers.Edit,SMStates.EditingServer)
                .PermitIf(SMTriggers.Connect,SMStates.Connected,isConnectedServer);

            Configure(SMStates.AddingServer)
                .OnEntry(launchAddDlgAction)
                .Permit(SMTriggers.Save, SMStates.Disconnected)
                .Permit(SMTriggers.Cancel, SMStates.Disconnected);

            Configure(SMStates.EditingServer)
                .OnEntry(launchEditDlgAction)
                .Permit(SMTriggers.EndEdit,SMStates.ServerSelected);

            Configure(SMStates.Connected)
                .Permit(SMTriggers.Disconnect,SMStates.ServerSelected)  //o connected?
                .Permit(SMTriggers.NetworkError, SMStates.ServerSelected)
                .OnExit(disconnectAction);

            OnTransitioned(
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
