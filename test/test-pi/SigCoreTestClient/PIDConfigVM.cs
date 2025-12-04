using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Linq;
using SigCoreCommon;

namespace SigCoreTestClient {
    public class PIDConfigVM : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private PID_LOOP.Config _config;
        public PID_LOOP.Config Config => _config;

        public PIDConfigVM(PID_LOOP.Config config) {
            _config = config;

            Title = config.Title;
            IsEnabled = config.Enabled;
            Kp = config.Kp;
            Ki = config.Ki;
            Kd = config.Kd;
            OutputMin = config.OutputMin;
            OutputMax = config.OutputMax;
            PVMin = config.PVMin;
            PVMax = config.PVMax;
            IsAutoMode = config.Auto;
            PvSource = config.PvSource;
            OutputDestination = config.OutputDestination;

            // New anti-windup config fields
            IntegralLimit = config.IntegralLimit;
            ResetOnModeChange = config.ResetOnModeChange;
        }

        private string _title;
        public string Title {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        private bool _isEnabled;
        public bool IsEnabled {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        private double _kp;
        public double Kp {
            get => _kp;
            set { _kp = value; OnPropertyChanged(nameof(Kp)); }
        }

        private double _ki;
        public double Ki {
            get => _ki;
            set { _ki = value; OnPropertyChanged(nameof(Ki)); }
        }

        private double _kd;
        public double Kd {
            get => _kd;
            set { _kd = value; OnPropertyChanged(nameof(Kd)); }
        }

        private double _outputMin;
        public double OutputMin {
            get => _outputMin;
            set { _outputMin = value; OnPropertyChanged(nameof(OutputMin)); }
        }

        private double _outputMax;
        public double OutputMax {
            get => _outputMax;
            set { _outputMax = value; OnPropertyChanged(nameof(OutputMax)); }
        }

        private double _pvMin;
        public double PVMin {
            get => _pvMin;
            set { _pvMin = value; OnPropertyChanged(nameof(PVMin)); }
        }

        private double _pvMax;
        public double PVMax {
            get => _pvMax;
            set { _pvMax = value; OnPropertyChanged(nameof(PVMax)); }
        }

        // === New anti-windup properties ===
        private double _integralLimit;
        public double IntegralLimit {
            get => _integralLimit;
            set { _integralLimit = value; OnPropertyChanged(nameof(IntegralLimit)); }
        }

        private bool _resetOnModeChange;
        public bool ResetOnModeChange {
            get => _resetOnModeChange;
            set { _resetOnModeChange = value; OnPropertyChanged(nameof(ResetOnModeChange)); }
        }

        private bool _isAutoMode;
        public bool IsAutoMode {
            get => _isAutoMode;
            set { _isAutoMode = value; OnPropertyChanged(nameof(IsAutoMode)); }
        }

        private uint _pvSource;
        public uint PvSource {
            get => _pvSource;
            set { _pvSource = value; OnPropertyChanged(nameof(PvSource)); }
        }

        private uint _outputDest;
        public uint OutputDestination {
            get => _outputDest;
            set { _outputDest = value; OnPropertyChanged(nameof(OutputDestination)); }
        }

        public void ApplyChanges() {
            try {
                _config.Title = Title;
                _config.Enabled = IsEnabled;
                _config.Kp = Kp;
                _config.Ki = Ki;
                _config.Kd = Kd;
                _config.OutputMin = OutputMin;
                _config.OutputMax = OutputMax;
                _config.PVMin = PVMin;
                _config.PVMax = PVMax;
                _config.Auto = IsAutoMode;
                _config.PvSource = PvSource;
                _config.OutputDestination = OutputDestination;

                // New anti-windup config fields
                _config.IntegralLimit = IntegralLimit;
                _config.ResetOnModeChange = ResetOnModeChange;
            } catch (Exception ex) {
                System.Windows.MessageBox.Show(
                    $"Error applying changes: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        // Dropdown values
        public uint[] PvSources => Enumerable.Range(0, 4).Select(i => (uint)i).ToArray();
        public uint[] OutputDestinations => Enumerable.Range(0, 4).Select(i => (uint)i).ToArray();
    }
}
