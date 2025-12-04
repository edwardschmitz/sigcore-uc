using System.Windows;

namespace ControlPanel.Dialogs {
    public partial class LoggerConfigDlg : Window {
        public LoggerConfigDlg() {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }
    }
}
