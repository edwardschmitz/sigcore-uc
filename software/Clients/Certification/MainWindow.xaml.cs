using SigCoreCommon;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using Zeroconf;

namespace Certification {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private SigCoreSystem _system;
        private MainWindowVM _vm;

        public MainWindow() {
            InitializeComponent();
            _vm = new MainWindowVM();
            DataContext = _vm;
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            _system = new SigCoreSystem();
            _vm.System = _system;
            Discover();
        }
        private async Task OpenHosts(string host, string name) {
            await _system!.ConnectAsync(host, name);
            _vm.System = _system;
        }
        private void Discover() {
            SigCoreDiscovery discovery = new SigCoreDiscovery();

            discovery.DeviceDiscovered += async (IZeroconfHost host) => {
                // Stop further discovery immediately
                discovery.Stop();

                Console.WriteLine($"Found device: {host.DisplayName} @ {host.IPAddress}");

                try {
                    await OpenHosts(host.DisplayName, host.IPAddress);
                } catch (Exception ex) {
                    Console.WriteLine($"Connection to {host.IPAddress} failed: {ex.Message}");
                }

                foreach (var service in host.Services) {
                    Console.WriteLine($" - {service.Key}, Port: {service.Value.Port}");
                }
            };

            discovery.Start();
        }
    }
}