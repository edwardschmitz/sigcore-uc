using SigCoreCommon;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ControlPanel {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        SigCoreSystem system = new SigCoreSystem();
        MainWindowVM vm;

        public MainWindow() {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            vm = new MainWindowVM(system);
            DataContext = vm;

            Microsoft.Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        }

        private void SystemEvents_PowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e) {
            if (e.Mode == Microsoft.Win32.PowerModes.Resume) {
                vm.HandleSystemResume();
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            //Cursor = Cursors.Wait;

            //try {
            //    bool result = await system.ConnectAsync(selected.IPAddress);
            //    if (result) {
            //        vm.InitializeComponents();
            //        system.Subscribe();
            //    } else {
            //        MessageBox.Show($"Connect Failed {selected.IPAddress}", "System Connection Error");
            //    }

            //} catch (Exception ex) {
            //    MessageBox.Show($"Initialization failed:\n{ex.Message}", "Error",
            //                    MessageBoxButton.OK, MessageBoxImage.Error);
            //} finally {
            //    Cursor = Cursors.Arrow;
            //    IsEnabled = true;

            //    Binding visBinding = new Binding("WindowVis");
            //    DimOverlay.SetBinding(Border.VisibilityProperty, visBinding);
            //}
        }
        public void SetDim(bool isDimmed) {
            DimOverlay.Visibility = isDimmed ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}