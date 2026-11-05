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
        private static bool TryIpv4(string input, out uint bits) {
            bits = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;
            string[] segments = input.Trim().Split('.');
            if (segments.Length != 4) return false;
            foreach (string segment in segments) {
                if (segment.Length == 0 || segment.Length > 3 ||
                    !byte.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out byte octet) ||
                    segment != octet.ToString(CultureInfo.InvariantCulture))
                    return false;
                bits = (bits << 8) | octet;
            }
            return true;
        }

        private bool ValidateAddress(string property, string label, bool required = true, bool mask = false) {
            TextBox field = DialogInputValidation.FindTextBox(this, property);
            if (field == null)
                return DialogInputValidation.Reject(this, null, "Missing " + label + " input field.");
            if (!required && string.IsNullOrWhiteSpace(field.Text)) return true;
            if (!TryIpv4(field.Text, out uint bits))
                return DialogInputValidation.Reject(this, field, label + " must be a dotted IPv4 address (for example, 192.168.1.10).");

            // Do not send leading/trailing spaces even when the typed address is otherwise valid.
            field.Text = field.Text.Trim();
            if (mask) {
                // A valid IPv4 netmask has contiguous 1-bits followed by 0-bits.
                uint inverted = ~bits;
                if (bits == 0 || (inverted & (inverted + 1)) != 0)
                    return DialogInputValidation.Reject(this, field, label + " must be a contiguous IPv4 subnet mask.");
            }
            return true;
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            // DHCP can leave unused static fields containing old values; validate only the active interface settings.
            if (EthDhcpCheck.IsChecked != true &&
                (!ValidateAddress("EthIpAddress", "Ethernet IP address") ||
                 !ValidateAddress("EthSubnetMask", "Ethernet subnet mask", mask: true) ||
                 !ValidateAddress("EthGateway", "Ethernet gateway", required: false) ||
                 !ValidateAddress("EthDns", "Ethernet DNS", required: false)))
                return;

            if (WifiDhcpCheck.IsChecked != true &&
                (!ValidateAddress("WifiIpAddress", "Wi-Fi IP address") ||
                 !ValidateAddress("WifiSubnetMask", "Wi-Fi subnet mask", mask: true) ||
                 !ValidateAddress("WifiGateway", "Wi-Fi gateway", required: false) ||
                 !ValidateAddress("WifiDns", "Wi-Fi DNS", required: false)))
                return;

            if (!DialogInputValidation.CommitBindings(this)) return;
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
