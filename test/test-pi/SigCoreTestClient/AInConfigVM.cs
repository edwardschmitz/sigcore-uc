using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using static SigCoreCommon.A_IN;
using Range = SigCoreCommon.A_IN.Range;

namespace SigCoreTestClient {
    public class AInConfigVM : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private AInConfig _config;
        public AInConfig Config => _config;

        public AInConfigVM(AInConfig config) {
            _config = config;
            Name = config.Name;
            Units = config.Units;
            AveragingSamples = config.AveragingSamples;
            CalibrationType = config.CalibrationType;
            M = config.M;
            B = config.B;
            InputRange = config.InputRange;
            InputPoints = string.Join(", ", config.InputPoints);
            AdjustedPoints = string.Join(", ", config.AdjustedPoints);
            PolynomialCoefficients = string.Join(", ", config.PolynomialCoefficients);
        }

        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }

        private string _units;
        public string Units { get => _units; set { _units = value; OnPropertyChanged(nameof(Units)); } }

        private int _avgSamples;
        public int AveragingSamples { get => _avgSamples; set { _avgSamples = value; OnPropertyChanged(nameof(AveragingSamples)); } }

        private CalType _calType;
        public CalType CalibrationType { get => _calType; set { _calType = value; OnPropertyChanged(nameof(CalibrationType)); } }

        private double _m;
        public double M { get => _m; set { _m = value; OnPropertyChanged(nameof(M)); } }

        private double _b;
        public double B { get => _b; set { _b = value; OnPropertyChanged(nameof(B)); } }

        private SigCoreCommon.A_IN.Range _range;
        public SigCoreCommon.A_IN.Range InputRange { get => _range; set { _range = value; OnPropertyChanged(nameof(InputRange)); } }

        private string _inputPoints;
        public string InputPoints { get => _inputPoints; set { _inputPoints = value; OnPropertyChanged(nameof(InputPoints)); } }

        private string _adjustedPoints;
        public string AdjustedPoints { get => _adjustedPoints; set { _adjustedPoints = value; OnPropertyChanged(nameof(AdjustedPoints)); } }

        private string _polyCoeff;
        public string PolynomialCoefficients { get => _polyCoeff; set { _polyCoeff = value; OnPropertyChanged(nameof(PolynomialCoefficients)); } }

        public void ApplyChanges() {
            try {
                _config.Name = Name;
                _config.Units = Units;
                _config.AveragingSamples = AveragingSamples;
                _config.CalibrationType = CalibrationType;
                _config.M = M;
                _config.B = B;
                _config.InputRange = InputRange;

                _config.InputPoints = ParseList(InputPoints);
                _config.AdjustedPoints = ParseList(AdjustedPoints);
                _config.PolynomialCoefficients = ParseList(PolynomialCoefficients);
            } catch (Exception ex) {
                MessageBox.Show($"Error applying changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private double[] ParseList(string s) {
            if (string.IsNullOrWhiteSpace(s))
                return Array.Empty<double>();
            return s.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(str => double.TryParse(str, out var v) ? v : 0.0)
                    .ToArray();
        }
        public CalType[] CalTypeValues => (CalType[])Enum.GetValues(typeof(CalType));
        public Range[] RangeValues => (Range[])Enum.GetValues(typeof(Range));
    }
}
