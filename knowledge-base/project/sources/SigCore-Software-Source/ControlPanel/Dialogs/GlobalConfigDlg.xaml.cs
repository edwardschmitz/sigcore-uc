using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ControlPanel.Dialogs {
    public partial class GlobalConfigDlg : Window {
        public GlobalConfigDlg() {
            InitializeComponent();
        }

        //private void Ok_Click(object sender, RoutedEventArgs e) {
        //    DialogResult = true;
        //    Close();
        //}

        //public static NameValueItem[] SampleRates {
        //    get {
        //        (string Name, int Value)[] src = SystemConfigPayload.SamplingRates;
        //        NameValueItem[] result = new NameValueItem[src.Length];

        //        for (int i = 0; i < src.Length; i++) {
        //            result[i] = new NameValueItem(src[i].Name, src[i].Value);
        //        }

        //        return result;
        //    }
        //}
    }

}
