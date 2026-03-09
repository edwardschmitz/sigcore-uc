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
            // Collapse everything first
            MxBPanel.Visibility = Visibility.Collapsed;
            PolynomialPanel.Visibility = Visibility.Collapsed;
            MultiPointPanel.Visibility = Visibility.Collapsed;

            // These values must match the payload enum values
            // 0 = None
            // 1 = mXplusB
            // 2 = MultiPoint
            // 3 = Polynomial

            switch (calType) {
                case 1: // mXplusB
                    MxBPanel.Visibility = Visibility.Visible;
                    break;

                case 2: // MultiPoint
                    MultiPointPanel.Visibility = Visibility.Visible;
                    break;

                case 3: // Polynomial
                    PolynomialPanel.Visibility = Visibility.Visible;
                    break;
            }
        }
    }
}
