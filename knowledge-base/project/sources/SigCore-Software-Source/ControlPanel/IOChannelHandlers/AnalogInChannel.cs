using ControlPanel.Dialogs;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using System.Windows;

namespace ControlPanel.IOChannelHandlers {
    public class AnalogInChannel : IOChannel<double> {
        private SigCoreSystem system;

        public AnalogInChannel(uint index, string userID, SigCoreSystem system, IDimmer dimmer)
            : base("AI", index, userID, "V", dimmer) { 
            this.system = system;
        }

        private int precision;
        public int Precision { 
            get => precision; 
            set {
                precision = value;
                OnPropertyChanged(nameof(Precision));
            }
        }
        private int display;
        public int Display {
            get => display;
            set {
                display = value;
                OnPropertyChanged(nameof(Display));
            }
        }

        protected override void OnEditRequested() {
            base.OnEditRequested();
            _ = EditConfig();
        }
        private async Task EditConfig() {
            dimmer?.SetDim(true);
            AInConfigPayload config;

            config = await system.GetAInConfigAsync(Index);

            AInConfigDlg dlg = new AInConfigDlg(config);

            dlg.Owner = Application.Current.MainWindow;
            bool? result = dlg.ShowDialog();
            if (result == true) {
                await system.SetAInConfigAsync(config);
                ShowConfig(config);
            }
            dimmer?.SetDim(false);
        }
        public override void Initialize() {
            base.Initialize();
            _ = UpdateFromConfig();
        }
        public async Task UpdateFromConfig() {
            AInConfigPayload config = await system.GetAInConfigAsync(Index);
            ShowConfig(config);
        }

        private void ShowConfig(AInConfigPayload config) {
            UserID = config.Name;
            Units = config.Units;
            Precision = config.Precision;
            Display = (int)config.Display;
        }
    }
}
