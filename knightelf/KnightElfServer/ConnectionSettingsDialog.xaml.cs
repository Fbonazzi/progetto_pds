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
using System.Windows.Shapes;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for ConnectionSettingsDialog.xaml
    /// </summary>
    public partial class ConnectionSettingsDialog : Window
    {
        public ConnectionSettingsDialog()
        {
            InitializeComponent();

            //TODO: Populate the ListBox with feasible IPs

        }

        private void btnDialogSave_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}