using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace KnightElfClient
{
    public enum SMStates
    {
        Start,
        AddingServer,
        ServerSelected,
        EditingServer,
        RemovingServer,
        WorkingRemote
    }
    public enum SMTriggers
    {
        Add, Edit,
        Remove, Removed,
        Save, Cancel,
        Select, Deselect,
        Connect, Disconnect,
        Run, Pause
    }

    class StateMachine : Stateless.StateMachine<SMStates, SMTriggers>, INotifyPropertyChanged
    {   
        public StateMachine(    Func<bool> existsServer,
                                Func<bool> isEditableServer,
                                Func<bool> isConnectedServer,
                                Func<bool> isReadyServer,
                                Action launchAddDlgAction,
                                Action launchEditDlgAction,
                                Action removeServerAction,
                                Action disconnectAction,
                                Action connectAction) : base(SMStates.Start)
        {

            Configure(SMStates.Start)
                .Permit(SMTriggers.Add,SMStates.AddingServer)
                .PermitIf(SMTriggers.Select,SMStates.ServerSelected, existsServer);

            Configure(SMStates.ServerSelected)
                .SubstateOf(SMStates.Start)
                .Permit(SMTriggers.Deselect, SMStates.Start)
                .PermitIf(SMTriggers.Remove,SMStates.RemovingServer, existsServer)
                .PermitIf(SMTriggers.Edit, SMStates.EditingServer, isEditableServer)
                .PermitIf(SMTriggers.Connect, SMStates.EditingServer, isReadyServer)
                .PermitIf(SMTriggers.Disconnect, SMStates.EditingServer, isConnectedServer)
                .PermitIf(SMTriggers.Run, SMStates.WorkingRemote, isConnectedServer);

            Configure(SMStates.AddingServer)
                .OnEntry(removeServerAction)
                .Permit(SMTriggers.Save, SMStates.ServerSelected)
                .Permit(SMTriggers.Cancel, SMStates.Start);

            Configure(SMStates.RemovingServer)
                .OnEntry(removeServerAction)
                .Ignore(SMTriggers.Deselect)
                .Permit(SMTriggers.Removed, SMStates.Start);

            Configure(SMStates.EditingServer)
                .OnEntry(launchEditDlgAction)
                .Permit(SMTriggers.Save, SMStates.ServerSelected)
                .Permit(SMTriggers.Cancel, SMStates.ServerSelected);

            Configure(SMStates.WorkingRemote)
                .Permit(SMTriggers.Pause, SMStates.ServerSelected);

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
