using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ControlPanel.Dialogs {
    public partial class AInConfigDlg : Window {
        public AInConfigDlg(AInConfigPayload config) {
            InitializeComponent();

            AInConfigDlgVM vm = new AInConfigDlgVM(config);
            DataContext = vm;

            UpdateCalibrationVisibility(vm.CalibrationType);

            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AInConfigDlgVM.CalibrationType)) {
                    UpdateCalibrationVisibility(vm.CalibrationType);
                }
            };
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        public static Array Ranges { get; } = AInConfigPayload.Ranges;
        public static Array CalTypes { get; } = AInConfigPayload.CalTypes;
        public static Array DisplayFormats { get; } = AInConfigPayload.DisplayFormats;
        private void UpdateCalibrationVisibility(int calType) {
            MxBPanel.Visibility = Visibility.Collapsed;
            PolynomialPanel.Visibility = Visibility.Collapsed;
            MultiPointPanel.Visibility = Visibility.Collapsed;

            switch ((A_IN.CalType)calType) {
                case A_IN.CalType.mXplusB:
                    MxBPanel.Visibility = Visibility.Visible;
                    FormulaText.Text = "y = m·x + b";
                    break;

                case A_IN.CalType.Log10:
                    MxBPanel.Visibility = Visibility.Visible;
                    FormulaText.Text = "y = 10^(m·x + b)";
                    break;

                case A_IN.CalType.MultiPoint:
                    MultiPointPanel.Visibility = Visibility.Visible;
                    break;

                case A_IN.CalType.Polynomial:
                    PolynomialPanel.Visibility = Visibility.Visible;
                    break;
            }
        }
    }
}
