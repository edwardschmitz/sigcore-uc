using ControlPanel.Dialogs;
using SigCoreCommon;
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
            A_IN.AInConfig config;

            config = await system.GetAInConfigAsync(Index);

            AInConfigDlgVM vm = new AInConfigDlgVM(config);
            AInConfigDlg dlg = new AInConfigDlg();
            dlg.DataContext = vm;

            dlg.Owner = Application.Current.MainWindow;
            bool? result = dlg.ShowDialog();
            if (result == true) {
                await system.SetAInConfigAsync(Index, vm.Config);
                ShowConfig(config);
            }
            dimmer?.SetDim(false);
        }
        public override void Initialize() {
            base.Initialize();
            _ = UpdateFromConfig();
        }
        public async Task UpdateFromConfig() {
            A_IN.AInConfig config = await system.GetAInConfigAsync(Index);
            ShowConfig(config);
        }

        private void ShowConfig(A_IN.AInConfig config) {
            UserID = config.Name;
            Units = config.Units;
            Precision = config.Precision;
            Display = (int)config.Display;
        }
    }
}
