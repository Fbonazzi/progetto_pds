﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ConnectionSettingsDialog cSettingsDlg;

        public MainWindow()
        {
            InitializeComponent();
            cSettingsDlg = new ConnectionSettingsDialog();
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            if(cSettingsDlg.ShowDialog() == true)
            {
                //TODO: show IP?
                btnConnect.IsEnabled = true;
                labelIPaddr.Content = cSettingsDlg.IPaddr;
            }
        }
    }
}
