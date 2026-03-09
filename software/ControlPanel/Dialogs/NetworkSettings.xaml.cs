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
using System.Windows.Shapes;

namespace ControlPanel.Dialogs {
    /// <summary>
    /// Interaction logic for NetworkSettings.xaml
    /// </summary>
    public partial class NetworkSettings : Window {
        public NetworkSettings() {
            InitializeComponent();
        }
        private void EthDhcpChanged(object sender, RoutedEventArgs e) {
            UpdatePanels();
        }

        private void WifiDhcpChanged(object sender, RoutedEventArgs e) {
            UpdatePanels();
        }

        private void UpdatePanels() {
            EthStaticPanel.Visibility =
                EthDhcpCheck.IsChecked == true
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            WifiStaticPanel.Visibility =
                WifiDhcpCheck.IsChecked == true
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }
        private void Ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

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
