using System.Windows;
using SigCoreCommon;

namespace SigCoreTestClient {
    public partial class DInConfigDlg : Window {
        private DInConfigVM _vm;

        public DInConfigDlg(D_IN.DInConfig config) {
            InitializeComponent();
            _vm = new DInConfigVM(config);
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
