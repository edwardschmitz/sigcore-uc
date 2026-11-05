using System.Windows;

namespace ControlPanel.Dialogs {
    public partial class ReconnectionDlg : Window {
        public ReconnectionDlg() {
            InitializeComponent();
        }

        private void CloseControlPanel_Click(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }
    }
}
