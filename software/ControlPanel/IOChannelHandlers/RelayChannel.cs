using ControlPanel.Dialogs;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ControlPanel.IOChannelHandlers {
    public class RelayChannel : IOChannel<bool> {
        private SigCoreSystem system;
        public RelayChannel(uint index, string userID, SigCoreCommon.SigCoreSystem system, IDimmer dimmer)
            : base("R", index, userID, "", dimmer) {
            this.system = system;
        }
        public bool DefaultState { get; set; }
        protected override void OnEditRequested() {
            base.OnEditRequested();
            _ = EditConfig();
        }
        protected override void OnUIValueChanged(bool newValue) {
            _ = system.SetRelayAsync(Index, newValue); 
        }
        private async Task EditConfig() {
            dimmer?.SetDim(true);
            RelayConfigPayload config;

            config = await system.GetRelayConfigAsync(Index);

            RelayConfigDlgVM vm = new RelayConfigDlgVM(config);
            RelayConfigDlg dlg = new RelayConfigDlg();
            dlg.DataContext = vm;

            dlg.Owner = Application.Current.MainWindow;
            bool? result = dlg.ShowDialog();
            if (result == true) {
                await system.SetRelayConfigAsync(Index, vm.Config);
                ShowConfig(config);
            }
            dimmer?.SetDim(false);
        }
        public override void Initialize() {
            base.Initialize();
            _ = UpdateFromConfig();
        }
        public async Task UpdateFromConfig() {
            RelayConfigPayload config = await system.GetRelayConfigAsync(Index);
            bool val = await system.GetRelayAsync(Index);
            Value = val;
            ShowConfig(config);
        }

        private void ShowConfig(RelayConfigPayload config) {
            UserID = config.Name;
        }
    }
}
