using SigCoreCommon;
using System.Windows;
using static SigCoreCommon.RELAY_OUT;

namespace SigCoreTestClient {
    public partial class RelayConfigDlg : Window {
        private RelayConfigVM _vm;

        public RelayConfigDlg(RelayConfig config) {
            InitializeComponent();

            _vm = new RelayConfigVM(config);
            DataContext = _vm;

            SaveBtn.Click += (s, e) => {
                _vm.ApplyChanges();
                DialogResult = true;
                Close();
            };

            CloseBtn.Click += (s, e) => Close();
        }
    }
}
