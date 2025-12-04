using SigCoreCommon;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ControlPanel.Dialogs {
    public partial class AInConfigDlg : Window {
        public AInConfigDlg() {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        public static Array Ranges { get; } = Enum.GetValues(typeof(SigCoreCommon.A_IN.Range));
        public static Array CalTypes { get; } = Enum.GetValues(typeof(SigCoreCommon.A_IN.CalType));
        public static Array DisplayFormats { get; } = Enum.GetValues(typeof(SigCoreCommon.A_IN.DisplayFormat));
    }

    public class CalTypeToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
