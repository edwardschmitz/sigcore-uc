using System;
using System.ComponentModel;
using static SigCoreCommon.A_OUT;

namespace SigCoreTestClient {
    public class AOutConfigVM : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private AnalogOutChannelConfig _config;
        public AnalogOutChannelConfig Config => _config;

        public AOutConfigVM(AnalogOutChannelConfig config) {
            _config = config;
            Name = config.Name;
            Units = config.Units;
            VoltageScaleM = config.VoltageScaleM;
            VoltageScaleB = config.VoltageScaleB;
            Mode = config.Mode;
            IsAuto = config.IsAuto;
        }

        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }

        private string _units;
        public string Units { get => _units; set { _units = value; OnPropertyChanged(nameof(Units)); } }

        private double _voltM;
        public double VoltageScaleM { get => _voltM; set { _voltM = value; OnPropertyChanged(nameof(VoltageScaleM)); } }

        private double _voltB;
        public double VoltageScaleB { get => _voltB; set { _voltB = value; OnPropertyChanged(nameof(VoltageScaleB)); } }

        private OutputMode _mode;
        public OutputMode Mode { get => _mode; set { _mode = value; OnPropertyChanged(nameof(Mode)); } }

        private bool _isAuto;
        public bool IsAuto { get => _isAuto; set { _isAuto = value; OnPropertyChanged(nameof(IsAuto)); } }

        public void ApplyChanges() {
            try {
                _config.Name = Name;
                _config.Units = Units;
                _config.VoltageScaleM = VoltageScaleM;
                _config.VoltageScaleB = VoltageScaleB;
                _config.Mode = Mode;
                _config.IsAuto = IsAuto;
            } catch (Exception ex) {
                System.Windows.MessageBox.Show($"Error applying changes: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public OutputMode[] ModeValues => (OutputMode[])Enum.GetValues(typeof(OutputMode));
    }
}
