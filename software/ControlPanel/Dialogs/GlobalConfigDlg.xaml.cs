using SigCoreCommon;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ControlPanel.Dialogs {
    public partial class GlobalConfigDlg : Window {
        public GlobalConfigDlg() {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        public static Array SampleRates { get; } = Enum.GetValues(typeof(HardwareManager.Config.SamplesPerSecond));
    }

    public class BoolToCollapsedConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool b && b) {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
