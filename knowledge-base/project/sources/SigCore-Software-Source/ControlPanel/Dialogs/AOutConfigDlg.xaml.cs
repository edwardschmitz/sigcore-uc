using SigCoreCommon;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ControlPanel.Dialogs {
    public partial class AOutConfigDlg : Window {
        public AOutConfigDlg() {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        // Expose the OutputMode enum for XAML binding
        public static Array Modes { get; } = Enum.GetValues(typeof(A_OUT.OutputMode));
        public static Array DisplayFormats { get; } = Enum.GetValues(typeof(SigCoreCommon.A_OUT.DisplayFormat));
    }
}
