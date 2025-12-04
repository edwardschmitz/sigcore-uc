using SigCoreCommon;

namespace ControlPanel.Dialogs {
    public class AInConfigDlgVM : ViewModelBase {
        private readonly A_IN.AInConfig _config;

        public AInConfigDlgVM(A_IN.AInConfig config) {
            _config = config;
        }

        public string Name {
            get => _config.Name;
            set { if (_config.Name != value) { _config.Name = value; OnPropertyChanged(); } }
        }

        public string Units {
            get => _config.Units;
            set { if (_config.Units != value) { _config.Units = value; OnPropertyChanged(); } }
        }

        public int AveragingSamples {
            get => _config.AveragingSamples;
            set { if (_config.AveragingSamples != value) { _config.AveragingSamples = value; OnPropertyChanged(); } }
        }

        public A_IN.Range InputRange {
            get => _config.InputRange;
            set { if (_config.InputRange != value) { _config.InputRange = value; OnPropertyChanged(); } }
        }

        public A_IN.CalType CalibrationType {
            get => _config.CalibrationType;
            set { if (_config.CalibrationType != value) { _config.CalibrationType = value; OnPropertyChanged(); } }
        }

        public double M {
            get => _config.M;
            set { if (_config.M != value) { _config.M = value; OnPropertyChanged(); } }
        }

        public double B {
            get => _config.B;
            set { if (_config.B != value) { _config.B = value; OnPropertyChanged(); } }
        }

        public string PolynomialCoefficientsString {
            get => _config.PolynomialCoefficientsString;
            set { if (_config.PolynomialCoefficientsString != value) { _config.PolynomialCoefficientsString = value; OnPropertyChanged(); } }
        }

        public string PiecewisePairsString {
            get => _config.PiecewisePairsString;
            set { if (_config.PiecewisePairsString != value) { _config.PiecewisePairsString = value; OnPropertyChanged(); } }
        }

        public int Precision {
            get => _config.Precision;
            set { if (_config.Precision != value) { _config.Precision = value; OnPropertyChanged(); } }
        }

        public A_IN.DisplayFormat Display {
            get => _config.Display;
            set { if (_config.Display != value) { _config.Display = value; OnPropertyChanged(); } }
        }

        public A_IN.AInConfig Config => _config;

        public static A_IN.CalType[] CalTypes => (A_IN.CalType[])System.Enum.GetValues(typeof(A_IN.CalType));
        public static A_IN.Range[] Ranges => (A_IN.Range[])System.Enum.GetValues(typeof(A_IN.Range));
    }
}
