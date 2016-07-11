using KnightElfLibrary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace KnightElfServer
{
    [ValueConversion(typeof(SMStates), typeof(Viewbox))]
    public class StateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch ((SMStates)value)
            {
                case SMStates.Connected:
                    return Application.Current.FindResource("StatusConnected") as Viewbox;
                case SMStates.EditingConnection:
                    return Application.Current.FindResource("StatusEditing") as Viewbox;
                case SMStates.Ready:
                    return Application.Current.FindResource("StatusOffline") as Viewbox;
                case SMStates.SettingConnection:
                    return Application.Current.FindResource("StatusEditing") as Viewbox;
                case SMStates.Start:
                    return Application.Current.FindResource("StatusOffline") as Viewbox;
                case SMStates.WaitClientConnect:
                    return Application.Current.FindResource("StatusOffline") as Viewbox;
                case SMStates.Paused:
                    return Application.Current.FindResource("StatusPaused") as Viewbox;
                default:
                    return DependencyProperty.UnsetValue;
            }

            // or
            //return Application.Current.FindResource(Enum.GetName(typeof(SMStates), value) + "Icon");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }


        class SMDisconnectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            ViewModel vm = parameter as ViewModel;
            if ((SMStates)values[0] == SMStates.Connected)
                return values[1];
            else return values[2];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
