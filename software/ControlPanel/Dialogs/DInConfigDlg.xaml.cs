using System;
using System.Collections.Generic;
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
    /// Interaction logic for DInConfigDlg.xaml
    /// </summary>
    public partial class DInConfigDlg : Window {
        public DInConfigDlg() {
            InitializeComponent();
        }
        private void Ok_Click(object sender, RoutedEventArgs e) {
            // Zero debounce is valid: it disables debouncing.
            if (!DialogInputValidation.Validate(this,
                DialogInputValidation.NumericRule.Whole("DebounceMs", "Debounce interval (ms)", 0)) ||
                !DialogInputValidation.CommitBindings(this))
                return;

            DialogResult = true;
            Close();
        }
    }
}
