using ControlPanel.Dialogs;
using SigCoreCommon;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ControlPanel.IOChannelHandlers {

    public class AnalogOutChannel : IOChannel<double> {
        private readonly SigCoreSystem system;

        private string displayValue;
        public string DisplayValue {
            get => displayValue;
            set { displayValue = value; OnPropertyChanged(); }
        }

        private double editValue;
        public double EditValue {
            get => editValue;
            set {
                editValue = value;
                OnPropertyChanged();
                system.SetAOutValue(Index, value);
            }
        }

        private bool isEnabled = true;
        public bool IsEnabled {
            get => isEnabled;
            set { isEnabled = value; OnPropertyChanged(); }
        }

        private int precision;
        public int Precision {
            get => precision;
            set {
                precision = value;
                OnPropertyChanged();
                UpdateDisplayOnly();
            }
        }

        private int display;
        public int Display {
            get => display;
            set {
                display = value;
                OnPropertyChanged();
                UpdateDisplayOnly();
            }
        }

        private double minValue;
        public double MinValue {
            get => minValue;
            set { minValue = value; OnPropertyChanged(); }
        }

        private double maxValue;
        public double MaxValue {
            get => maxValue;
            set { maxValue = value; OnPropertyChanged(); }
        }

        public AnalogOutChannel(uint index, string userID, SigCoreSystem system, IDimmer dimmer)
            : base("AO", index, userID, "V", dimmer) {

            this.system = system;
        }

        // ------------------------------------------------------------
        // FORMATTING HELPERS
        // ------------------------------------------------------------
        private double FormatDouble(double v) {
            return Math.Round(v, Precision);
        }

        private string FormatString(double v) {
            if (Display == 1)
                return v.ToString($"E{Precision}");
            else
                return v.ToString($"F{Precision}");
        }

        private void UpdateDisplayOnly() {
            DisplayValue = FormatString(Value);
        }

        // ------------------------------------------------------------
        // UI → SYSTEM (popup commit)
        // ------------------------------------------------------------
        public void CommitEdit(double newValue) {

            double formatted = FormatDouble(newValue);

            // update the VM.Value to keep everything consistent
            SetValueFromServer(formatted);

            // push to hardware
            system.SetAOutValue(Index, formatted);
        }

        // ------------------------------------------------------------
        // SERVER → UI
        // ------------------------------------------------------------
        public override void SetValueFromServer(double raw) {
            double formatted = FormatDouble(raw);

            IsServerUpdate = true;
            IsServerUpdate = false;

            // update display string
            DisplayValue = FormatString(formatted);

        }

        // ------------------------------------------------------------
        // CONFIG / INIT
        // ------------------------------------------------------------
        protected override void OnEditRequested() {
            base.OnEditRequested();
            _ = EditConfig();
        }

        private async Task EditConfig() {
            dimmer?.SetDim(true);

            var config = await system.GetAOutConfigAsync(Index);

            var vm = new AOutConfigDlgVM(config);
            var dlg = new AOutConfigDlg { DataContext = vm };
            dlg.Owner = Application.Current.MainWindow;

            bool? result = dlg.ShowDialog();

            if (result == true) {
                await system.SetAOutConfigAsync(Index, vm.Config);
                ShowConfig(vm.Config);
            }

            dimmer?.SetDim(false);
        }

        public override void Initialize() {
            base.Initialize();
            _ = LoadFromSystem();
        }

        private async Task LoadFromSystem() {
            var config = await system.GetAOutConfigAsync(Index);
            double val = await system.GetAOutValueAsync(Index);

            ShowConfig(config);
            SetValueFromServer(val);
        }

        private void ShowConfig(A_OUT.AnalogOutChannelConfig config) {
            UserID = config.Name;
            Units = config.Units;

            Precision = config.Precision;
            Display = (int)config.Display;

            if (config.Mode == A_OUT.OutputMode.Voltage) {
                MinValue = config.VoltageScaleB;
                MaxValue = config.VoltageScaleM * 5 + config.VoltageScaleB;
            } else {
                MinValue = config.VoltageScaleB;
                MaxValue = config.VoltageScaleM * 100 + config.VoltageScaleB;
            }
        }
    }
}
