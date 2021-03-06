﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
            _notifyIcon.Icon = _notifyIcon.Icon = KnightElfServer.Properties.Resources.knight_red_transparent1;

            _notifyIcon.Text = "KnightElf";
            _notifyIcon.BalloonTipTitle = "KnightElf";
            _notifyIcon.BalloonTipText = "KnightElf is now in the tray area, right-click on the icon to show the menu.";
            _notifyIcon.Visible = true;

            CreateContextMenu();

            _notifyIcon.ShowBalloonTip(50);
        }

        private void CreateContextMenu()
        {
            _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Show KnightElf").Click += (s, e) => ShowMainWindow();
            System.Windows.Forms.ToolStripSeparator separator = new System.Windows.Forms.ToolStripSeparator();
            separator.Size = new System.Drawing.Size(173, 6);
            _notifyIcon.ContextMenuStrip.Items.Add(separator);
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
                _notifyIcon.ShowBalloonTip(50);
            }
        }

        private void ChangeIcon(object sender, PropertyChangedEventArgs e)
        {
            StateMachine sm = sender as StateMachine;
            switch (sm.State)
            {
                case SMStates.Running:
                    _notifyIcon.Icon = KnightElfServer.Properties.Resources.knight_green_transparent1;
                    break;
                case SMStates.Paused:
                    _notifyIcon.Icon = KnightElfServer.Properties.Resources.knight_yellow_transparent1;
                    break;
                default:
                    _notifyIcon.Icon = KnightElfServer.Properties.Resources.knight_red_transparent1;
                    break;
            }
        }
    }
}
