using System.Windows;

namespace SigCoreTestClient {
    public partial class PIDConfigDlg : Window {
        private PIDConfigVM _vm;

        public PIDConfigDlg(SigCoreCommon.PID_LOOP.Config config) {
            InitializeComponent();
            _vm = new PIDConfigVM(config);
            DataContext = _vm;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e) {
            _vm.ApplyChanges();
            DialogResult = true;
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
