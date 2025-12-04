using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace ControlPanel.Controls {
    public class ButtonCheckBox : CheckBox {
        static ButtonCheckBox() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonCheckBox), new FrameworkPropertyMetadata(typeof(ButtonCheckBox)));
        }
    }

    public class BooleanToOnOffConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool isChecked) {
                return isChecked ? "ON" : "OFF";
            }
            return "OFF";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is string str && str.Equals("ON", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class MouseOverColorConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool isChecked) {
                return isChecked ? Brushes.LightBlue : Brushes.LightGray;
            }
            return Brushes.LightGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
    public class MouseOverHighlightConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool isChecked) {
                return isChecked ? Brushes.Green : Brushes.DarkRed;
            }
            return Brushes.DarkRed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
