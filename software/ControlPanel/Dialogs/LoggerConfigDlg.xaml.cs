using System.Windows;

namespace ControlPanel.Dialogs {
    public partial class LoggerConfigDlg : Window {
        public LoggerConfigDlg() {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            if (!DialogInputValidation.Validate(this,
                DialogInputValidation.NumericRule.Real("IntervalSec", "Logging interval (seconds)", minimum: 0.001, maximum: 2147483),
                DialogInputValidation.NumericRule.Whole("Port", "Database port", 1, 65535),
                DialogInputValidation.NumericRule.Whole("ConnectionTimeoutSec", "Connection timeout", 1),
                DialogInputValidation.NumericRule.Whole("CommandTimeoutSec", "Command timeout", 1)) ||
                !DialogInputValidation.CommitBindings(this))
                return;

            DialogResult = true;
            Close();
        }
    }
}
