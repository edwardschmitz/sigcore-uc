using ControlPanel.Dialogs;
using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static SigCoreCommon.RELAY_OUT;

namespace ControlPanel.IOChannelHandlers {
    public class DigitalInChannel : IOChannel<bool> {
        private SigCoreSystem system;

        public DigitalInChannel(uint index, string userID, SigCoreCommon.SigCoreSystem system, IDimmer dimmer)
            : base("DI", index, userID, "", dimmer) { 
            this.system = system;
        }

        // Add any input-specific members here if you need later (e.g. debounce, filter)
        public bool IsFiltered { get; set; }

        protected override void OnEditRequested() {
            base.OnEditRequested();
            _ = EditConfig();
        }
        private async Task EditConfig() {
            dimmer?.SetDim(true);
            D_IN.DInConfig config;

            config = await system.GetDInConfigAsync(Index);

            DInConfigDlgVM vm = new DInConfigDlgVM(config);
            DInConfigDlg dlg = new DInConfigDlg();
            dlg.DataContext = vm;

            dlg.Owner = Application.Current.MainWindow;
            bool? result = dlg.ShowDialog();
            if (result == true) {
                await system.SetDInConfigAsync(Index, vm.Config);
                ShowConfig(config);
            }
            dimmer?.SetDim(false);
        }
        public override void Initialize() {
            base.Initialize();
            _ = UpdateFromConfig();
        }
        public async Task UpdateFromConfig() {
            D_IN.DInConfig config = await system.GetDInConfigAsync(Index);
            ShowConfig(config);
        }

        private void ShowConfig(D_IN.DInConfig config) {
            UserID = config.Name;
        }
    }
}
