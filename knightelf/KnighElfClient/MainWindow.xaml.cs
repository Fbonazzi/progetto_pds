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
using KnightElfLibrary;

namespace KnightElfClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            // Connect console output with logger TextBox
            Console.SetOut(new TextBoxWriter(tbLogger));

            _viewModel = new ViewModel();
            DataContext = _viewModel;

            //Bind Connection list with ListBox
            lbServers.ItemsSource = _viewModel.Servers;
        }
    }
}
