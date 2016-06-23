using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
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
using System.IO;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TextBoxWriter tbwLogger;

        
        public MainWindow()
        {
            InitializeComponent();

            // Set console output to logger TextBox
            tbwLogger = new TextBoxWriter(tbLogger);
            Console.SetOut(tbwLogger);

            // bind the Date to the UI
            DataContext = new ViewModel();
        }
    }
}
