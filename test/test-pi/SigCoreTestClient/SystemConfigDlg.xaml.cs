using System.Windows;
using SigCoreCommon;

namespace SigCoreTestClient {
    public partial class SystemConfigDlg : Window {
        private readonly SystemConfigVM _vm;

        public SystemConfigDlg(HardwareManager.Config config) {
            InitializeComponent();

            _vm = new SystemConfigVM(config);
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
