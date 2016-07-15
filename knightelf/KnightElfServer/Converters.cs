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
using System.Windows.Media.Imaging;

namespace KnightElfServer
{
    [ValueConversion(typeof(SMStates), typeof(DataTemplate))]
    public class StateToIconTConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch ((SMStates)value)
            {
                case SMStates.Running:
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
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    [ValueConversion(typeof(SMStates), typeof(Viewbox))]
    public class StateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch ((SMStates)value)
            {
                case SMStates.Running:
                    return Application.Current.FindResource("StatusIConnected") as Viewbox;
                case SMStates.EditingConnection:
                    return Application.Current.FindResource("StatusIEditing") as Viewbox;
                case SMStates.Ready:
                    return Application.Current.FindResource("StatusIOffline") as Viewbox;
                case SMStates.SettingConnection:
                    return Application.Current.FindResource("StatusIEditing") as Viewbox;
                case SMStates.Start:
                    return Application.Current.FindResource("StatusIOffline") as Viewbox;
                case SMStates.WaitClientConnect:
                    return Application.Current.FindResource("StatusIOffline") as Viewbox;
                case SMStates.Paused:
                    return Application.Current.FindResource("StatusIPaused") as Viewbox;
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

    [ValueConversion(typeof(SMStates), typeof(BitmapImage))]
    public class StateToAppIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch ((SMStates)value)
            {
                case SMStates.Running:
                    return Application.Current.FindResource("KnightGreen") as BitmapImage;
                case SMStates.EditingConnection:
                    return Application.Current.FindResource("KnightRed") as BitmapImage;
                case SMStates.Ready:
                    return Application.Current.FindResource("KnightRed") as BitmapImage;
                case SMStates.SettingConnection:
                    return Application.Current.FindResource("KnightRed") as BitmapImage;
                case SMStates.Start:
                    return Application.Current.FindResource("KnightRed") as BitmapImage;
                case SMStates.WaitClientConnect:
                    return Application.Current.FindResource("KnightRed") as BitmapImage;
                case SMStates.Paused:
                    return Application.Current.FindResource("KnightYellow") as BitmapImage;
                default:
                    return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
