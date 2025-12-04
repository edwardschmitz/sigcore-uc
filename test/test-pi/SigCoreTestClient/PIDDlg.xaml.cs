using SigCoreCommon;
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

namespace SigCoreTestClient {
    /// <summary>
    /// Interaction logic for PIDDlg.xaml
    /// </summary>
    public partial class PIDDlg : Window {
        private readonly Certification.PIDDlgVM _vm;

        public PIDDlg(SigCoreSystem system, uint channel) {
            InitializeComponent();
            _vm = new Certification.PIDDlgVM(system, channel);
            DataContext = _vm;
            Loaded += async (_, __) => await _vm.LoadAsync();
        }

        private async void SendValues_Click(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
