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
    [ValueConversion(typeof(SMStates), typeof(DataTemplate))]
    public class StateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch ((SMStates)value)
            {
                case SMStates.Connected:
                    return Application.Current.FindResource("StatusConnected") as DataTemplate;
                case SMStates.EditingConnection:
                    return Application.Current.FindResource("StatusEditing") as DataTemplate;
                case SMStates.Ready:
                    return Application.Current.FindResource("StatusOffline") as DataTemplate;
                case SMStates.SettingConnection:
                    return Application.Current.FindResource("StatusEditing") as DataTemplate;
                case SMStates.Start:
                    return Application.Current.FindResource("StatusOffline") as DataTemplate;
                case SMStates.WaitClientConnect:
                    return Application.Current.FindResource("StatusOffline") as DataTemplate;
                case SMStates.Paused:
                    return Application.Current.FindResource("StatusPaused") as DataTemplate;
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
}
