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

namespace KnightElfClient
{
    #region Validation Rules
    public class StringToIPValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            IPAddress ip;
            if (value!=null && IPAddress.TryParse(value.ToString(), out ip))
                return new ValidationResult(true, null);
            else return new ValidationResult(false, "Please enter a valid IP address.");
        }
    }

    public class PortValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            UInt16 port;
            if (UInt16.TryParse(value.ToString(), out port))
            {
                return new ValidationResult(true, null);
            }
            return new ValidationResult(false, "Please enter a valid IP address.");
        }
    }
    #endregion

    class StringIPConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value != null)
                return value.ToString();
            else
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IPAddress ip;
            if (IPAddress.TryParse(value as string, out ip))
                return ip;
            else
                return DependencyProperty.UnsetValue;
        }
    }

    public class HasErrorToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.LongLength > 0)
            {
                foreach (var value in values)
                {
                    // if at least one is true (HasError) set return to false (Not Enabled)
                    if (value is bool && (bool)value)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
