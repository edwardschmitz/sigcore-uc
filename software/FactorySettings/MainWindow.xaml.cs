using System.Windows;

namespace FactorySettings {
    public partial class MainWindow : Window {

        private readonly MainWindowVM _vm;

        public MainWindow() {
            InitializeComponent();

            _vm = new MainWindowVM();
            DataContext = _vm;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
