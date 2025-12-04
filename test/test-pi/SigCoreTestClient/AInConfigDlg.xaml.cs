using System.Windows;

namespace SigCoreTestClient {
    public partial class AInConfigDlg : Window {
        private AInConfigVM _vm;

        public AInConfigDlg(SigCoreCommon.A_IN.AInConfig config) {
            InitializeComponent();
            _vm = new AInConfigVM(config);
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
