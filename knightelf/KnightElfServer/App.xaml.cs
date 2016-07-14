using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
//using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool _isExit;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            MainWindow = new MainWindow();
            MainWindow.Closing += MainWindow_Closing;
            ((KnightElfServer.MainWindow)MainWindow).SubscribeToSMStateChanges(new PropertyChangedEventHandler(ChangeIcon));

            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.DoubleClick += (s, args) => ShowMainWindow();
            _notifyIcon.Icon = Icon.FromHandle(KnightElfServer.Properties.Resources.knight_red_transparent.GetHicon());
            _notifyIcon.Visible = true;

            CreateContextMenu();
        }

        private void CreateContextMenu()
        {
            _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Show KnightElf").Click += (s, e) => ShowMainWindow();
            _notifyIcon.ContextMenuStrip.Items.Add("Exit").Click += (s, e) => ExitApplication();
        }

        private void ExitApplication()
        {
            _isExit = true;
            MainWindow.Close();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        private void ShowMainWindow()
        {
            if (MainWindow.IsVisible)
            {
                if (MainWindow.WindowState == WindowState.Minimized)
                {
                    MainWindow.WindowState = WindowState.Normal;
                }
                MainWindow.Activate();
            }
            else
            {
                MainWindow.Show();
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isExit)
            {
                e.Cancel = true;
                MainWindow.Hide(); // A hidden window can be shown again, a closed one not
            }
        }

        private void ChangeIcon(object sender, PropertyChangedEventArgs e)
        {
            StateMachine sm = sender as StateMachine;
            switch (sm.State)
            {
                case SMStates.Running:
                    _notifyIcon.Icon = Icon.FromHandle(KnightElfServer.Properties.Resources.knight_green_transparent.GetHicon());
                    break;
                case SMStates.Paused:
                    _notifyIcon.Icon = Icon.FromHandle(KnightElfServer.Properties.Resources.knight_yellow_transparent.GetHicon());
                    break;
                default:
                    _notifyIcon.Icon = Icon.FromHandle(KnightElfServer.Properties.Resources.knight_red_transparent.GetHicon());
                    break;
            }
        }
    }
}
