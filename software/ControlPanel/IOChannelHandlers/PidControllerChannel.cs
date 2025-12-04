using System;
using System.Threading.Tasks;
using System.Windows;
using ControlPanel.Dialogs;
using SigCoreCommon;

namespace ControlPanel.IOChannelHandlers {
    public class PidControllerChannel : IOChannel<double> {
        private readonly SigCoreSystem system;

        // ================================
        // === PID STATE PROPERTIES ===
        // ================================
        private bool _isAutoMode = true;
        public bool IsAutoMode {
            get => _isAutoMode;
            set {
                if (_isAutoMode != value) {
                    _isAutoMode = value;
                    OnPropertyChanged(nameof(IsAutoMode));
                    _ = system.SetPIDAutoAsync(Index, value);
                }
            }
        }

        private double _setpoint;
        public double Setpoint {
            get => _setpoint;
            set {
                if (_setpoint != value) {
                    _setpoint = value;
                    OnPropertyChanged(nameof(Setpoint));
                    if (IsAutoMode)
                        _ = system.SetPIDSPAsync(Index, value);
                }
            }
        }

        private double _processValue;
        public double ProcessValue {
            get => _processValue;
            set {
                if (_processValue != value) {
                    _processValue = value;
                    OnPropertyChanged(nameof(ProcessValue));
                }
            }
        }

        private double _output;
        public double Output {
            get => _output;
            set {
                if (_output != value) {
                    _output = value;
                    OnPropertyChanged(nameof(Output));
                    if (!IsAutoMode)
                        _ = system.SetPIDOutputAsync(Index, value);
                }
            }
        }

        private double _tolerance;
        public double Tolerance {
            get => _tolerance;
            set {
                if (_tolerance != value) {
                    _tolerance = value;
                    OnPropertyChanged(nameof(Tolerance));
                    _ = system.SetPIDTolAsync(Index, value);
                }
            }
        }

        private double _crossover = 0;
        public double Crossover {
            get => _crossover;
            set {
                if (_crossover != value) {
                    _crossover = value;
                    OnPropertyChanged(nameof(Crossover));
                }
            }
        }

        private double _tickInterval = 10;
        public double TickInterval {
            get => _tickInterval;
            set {
                if (_tickInterval != value) {
                    _tickInterval = value;
                    OnPropertyChanged(nameof(TickInterval));
                }
            }
        }

        private double outputMin;
        public double OutputMin {
            get => outputMin;
            set {
                if (outputMin != value) {
                    outputMin = value;
                    OnPropertyChanged(nameof(OutputMin));
                }
            }
        }

        private double outputMax;
        public double OutputMax {
            get => outputMax;
            set {
                if (outputMax != value) {
                    outputMax = value;
                    OnPropertyChanged(nameof(OutputMax));
                }
            }
        }

        private double pvMin;
        public double PVMin {
            get => pvMin;
            set {
                if (pvMin != value) {
                    pvMin = value;
                    OnPropertyChanged(nameof(PVMin));
                }
            }
        }

        private double pvMax;
        public double PVMax {
            get => pvMax;
            set {
                if (pvMax != value) {
                    pvMax = value;
                    OnPropertyChanged(nameof(PVMax));
                }
            }
        }

        // ================================
        // === NEW: RAMP PROPERTIES ===
        // ================================
        private double _rampTime;
        public double RampTime {
            get => _rampTime;
            set {
                if (_rampTime != value) {
                    _rampTime = value;
                    OnPropertyChanged(nameof(RampTime));
                    _ = system.SetPIDRampTimeAsync(Index, value);
                }
            }
        }

        private double _rampTarget;
        public double RampTarget {
            get => _rampTarget;
            set {
                if (_rampTarget != value) {
                    _rampTarget = value;
                    OnPropertyChanged(nameof(RampTarget));
                    _ = system.SetPIDRampTargetAsync(Index, value);
                }
            }
        }

        private bool _isRamping;
        public bool IsRamping {
            get => _isRamping;
            set {
                if (_isRamping != value) {
                    _isRamping = value;
                    OnPropertyChanged(nameof(IsRamping));
                    _ = system.SetPIDRampAsync(Index, value);
                }
            }
        }

        private bool _isEnabled;
        public bool IsEnabled {
            get => _isEnabled;
            set {
                if (_isEnabled != value) {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        // ================================
        // === CONSTRUCTOR ===
        // ================================
        public PidControllerChannel(uint index, string userID, SigCoreSystem system, IDimmer dimmer)
            : base("PID", index, userID, "", dimmer) {
            this.system = system;

            // Subscribe to system events
            system.PIDSPChanged += OnPIDSPChanged;
            system.PIDPVChanged += OnPIDPVChanged;
            system.PIDOutputChanged += OnPIDOutputChanged;
            system.PIDDevChanged += OnPIDDevChanged;
            system.PIDTolChanged += OnPIDTolChanged;
            system.PIDAutoChanged += OnPIDAutoChanged;

            // New ramp events
            system.PIDRampTimeChanged += OnPIDRampTimeChanged;
            system.PIDRampTargetChanged += OnPIDRampTargetChanged;
            system.PIDRampChanged += OnPIDRampChanged;
        }

        // ================================
        // === EVENT HANDLERS ===
        // ================================
        private void OnPIDSPChanged(uint ch, double value) {
            if (ch != Index) return;
            Setpoint = value;
        }

        private void OnPIDPVChanged(uint ch, double value) {
            if (ch != Index) return;
            ProcessValue = value;
        }

        private void OnPIDOutputChanged(uint ch, double value) {
            if (ch != Index) return;
            Output = value;
        }

        private void OnPIDDevChanged(uint ch, double value) {
            if (ch != Index) return;
            // optional: store deviation for display
        }

        private void OnPIDTolChanged(uint ch, double value) {
            if (ch != Index) return;
            Tolerance = value;
        }

        private void OnPIDAutoChanged(uint ch, bool isAuto) {
            if (ch != Index) return;
            IsAutoMode = isAuto;
        }

        private void OnPIDRampTimeChanged(uint ch, double rampTimeSec) {
            if (ch != Index) return;
            RampTime = rampTimeSec;
        }

        private void OnPIDRampTargetChanged(uint ch, double rampTarget) {
            if (ch != Index) return;
            RampTarget = rampTarget;
        }

        private void OnPIDRampChanged(uint ch, bool ramp) {
            if (ch != Index) return;
            IsRamping = ramp;
        }
        

        // ================================
        // === CONFIG / UI ===
        // ================================
        protected override void OnEditRequested() {
            base.OnEditRequested();
            _ = EditConfig();
        }

        private async Task EditConfig() {
            dimmer?.SetDim(true);
            PID_LOOP.Config config = await system.GetPIDConfigAsync(Index);
            PidConfigDlgVM vm = new PidConfigDlgVM(config);
            PidConfigDlg dlg = new PidConfigDlg { DataContext = vm };
            dlg.Owner = Application.Current.MainWindow;

            bool? result = dlg.ShowDialog();
            if (result == true) {
                await system.SetPIDConfigAsync(Index, config);
                ShowConfig(config);
            }
            dimmer?.SetDim(false);
        }

        public override void Initialize() {
            base.Initialize();
            _ = UpdateFromConfig();
        }

        public async Task UpdateFromConfig() {
            PID_LOOP.Config config = await system.GetPIDConfigAsync(Index);
            ShowConfig(config);
        }

        private void ShowConfig(PID_LOOP.Config config) {
            UserID = config.Title;
            IsEnabled = config.Enabled;
            OutputMax = config.OutputMax;
            OutputMin = config.OutputMin;
            PVMax = config.PVMax;
            PVMin = config.PVMin;
        }
    }
}
