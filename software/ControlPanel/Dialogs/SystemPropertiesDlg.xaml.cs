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
    /// Interaction logic for SystemPropertiesDlg.xaml
    /// </summary>
    public partial class SystemPropertiesDlg : Window {
        public SystemPropertiesDlg() {
            InitializeComponent();
        }
        private void Ok_Click(object sender, RoutedEventArgs e) {
            // The Core/hardware must define any upper PWM frequency limit; reject nonpositive input here.
            if (!DialogInputValidation.Validate(this,
                DialogInputValidation.NumericRule.Whole("PWMFrequency", "PWM frequency (Hz)", 1)) ||
                !DialogInputValidation.CommitBindings(this))
                return;

            DialogResult = true;
            Close();
        }

    }
}
