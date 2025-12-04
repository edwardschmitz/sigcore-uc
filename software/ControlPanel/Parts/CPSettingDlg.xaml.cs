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

namespace ControlPanel.Parts {
    /// <summary>
    /// Interaction logic for CPSettingDlg.xaml
    /// </summary>
    public partial class CPSettingDlg : Window {
        CPSettingsVM vm = new CPSettingsVM();

        public CPSettingsVM VM { get => vm; }
        public CPSettingDlg() {
            InitializeComponent();
            
            DataContext = vm;
        }

        private void SaveSettings(object sender, RoutedEventArgs e) {
            // Handle save logic here...
            DialogResult = true;
        }

        private void CloseWindow(object sender, RoutedEventArgs e) {
            DialogResult = true;
            this.Close();
        }

    }
}
