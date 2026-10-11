using SigCoreCommon;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FactorySettings {
    public partial class SelectSigCoreDlg : Window {
        public string SelectedHost { get; private set; } = "";

        public SelectSigCoreDlg(IReadOnlyList<SigCoreDevice> devices) {
            InitializeComponent();
            DeviceList.ItemsSource = devices;

            if (devices.Count == 1) {
                DeviceList.SelectedIndex = 0;
            }

            HostTextBox.Focus();
        }

        private void DeviceList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e) {

            if (DeviceList.SelectedItem is SigCoreDevice device &&
                !string.IsNullOrWhiteSpace(device.Address)) {

                HostTextBox.Text = device.Address;
            }
        }

        private void DeviceList_MouseDoubleClick(
            object sender,
            MouseButtonEventArgs e) {

            if (DeviceList.SelectedItem is SigCoreDevice)
                AcceptSelection();
        }

        private void Connect_Click(object sender, RoutedEventArgs e) {
            AcceptSelection();
        }

        private void AcceptSelection() {
            string host = HostTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(host)) {
                MessageBox.Show(
                    this,
                    "Select a discovered SigCore UC or enter a hostname/IP address.",
                    "Factory Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            SelectedHost = host;
            DialogResult = true;
        }
    }
}
