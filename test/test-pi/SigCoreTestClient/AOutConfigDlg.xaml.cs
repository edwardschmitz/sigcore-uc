using System;
using System.Windows;

namespace SigCoreTestClient {
    public partial class AOutConfigDlg : Window {
        private AOutConfigVM _vm;

        public AOutConfigDlg(SigCoreCommon.A_OUT.AnalogOutChannelConfig config) {
            InitializeComponent();

            _vm = new AOutConfigVM(config);
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
