using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using KnightElfLibrary;

namespace KnightElfClient
{
    [ValueConversion(typeof(State), typeof(DataTemplate))]
    public class StateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch ((State)value)
            {
                case State.Authenticated:
                    return Application.Current.FindResource("StatusAuthentication") as DataTemplate;
                case State.Closed:
                    return Application.Current.FindResource("StatusOffline") as DataTemplate;
                case State.Connected:
                    return Application.Current.FindResource("StatusHourglass") as DataTemplate;
                case State.Crashed:
                    return Application.Current.FindResource("StatusOffline") as DataTemplate;
                case State.New:
                    return Application.Current.FindResource("StatusOffline") as DataTemplate;
                case State.Running:
                    return Application.Current.FindResource("StatusConnected") as DataTemplate;
                case State.Suspended:
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

    [ValueConversion(typeof(SMStates), typeof(String))]
    public class StateToCursorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch ((SMStates)value)
            {
                case SMStates.WorkingRemote:
                    return "None";
                default:
                    return "Arrow";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
