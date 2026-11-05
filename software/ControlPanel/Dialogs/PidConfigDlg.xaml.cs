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

namespace ControlPanel.Dialogs
{
    /// <summary>
    /// Interaction logic for PidConfigDlg.xaml
    /// </summary>
    public partial class PidConfigDlg : Window
    {
        public PidConfigDlg()
        {
            InitializeComponent();
        }
        private void Ok_Click(object sender, RoutedEventArgs e) {
            if (!DialogInputValidation.Validate(this,
                DialogInputValidation.NumericRule.Real("Kp", "Kp"),
                DialogInputValidation.NumericRule.Real("Ki", "Ki"),
                DialogInputValidation.NumericRule.Real("Kd", "Kd"),
                DialogInputValidation.NumericRule.Real("OutputMin", "Output minimum"),
                DialogInputValidation.NumericRule.Real("OutputMax", "Output maximum"),
                DialogInputValidation.NumericRule.Real("PVMin", "PV minimum"),
                DialogInputValidation.NumericRule.Real("PVMax", "PV maximum"),
                DialogInputValidation.NumericRule.Real("IntegralLimit", "Integral limit", minimum: 0)) ||
                !DialogInputValidation.CommitBindings(this))
                return;

            var config = (DataContext as PidConfigDlgVM)?.Config;
            if (config == null) {
                DialogInputValidation.Reject(this, null, "PID configuration is unavailable.");
                return;
            }
            if (config.OutputMin >= config.OutputMax) {
                DialogInputValidation.Reject(this, DialogInputValidation.FindTextBox(this, "OutputMin"),
                    "Output minimum must be less than output maximum.");
                return;
            }
            if (config.PVMin >= config.PVMax) {
                DialogInputValidation.Reject(this, DialogInputValidation.FindTextBox(this, "PVMin"),
                    "PV minimum must be less than PV maximum.");
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
