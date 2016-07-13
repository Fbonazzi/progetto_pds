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
    [ValueConversion(typeof(State), typeof(Viewbox))]
    public class StateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch ((State)value)
            {
                case State.Authenticated:
                    return Application.Current.FindResource("StatusAuthentication") as Viewbox;
                case State.Closed:
                    return Application.Current.FindResource("StatusOffline") as Viewbox;
                case State.Connected:
                    return Application.Current.FindResource("StatusHourglass") as Viewbox;
                case State.Crashed:
                    return Application.Current.FindResource("StatusOffline") as Viewbox;
                case State.New:
                    return Application.Current.FindResource("StatusOffline") as Viewbox;
                case State.Running:
                    return Application.Current.FindResource("StatusConnected") as Viewbox;
                case State.Suspended:
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
}
