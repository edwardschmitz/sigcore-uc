using SigCoreCommon;
using System.ComponentModel;

namespace ControlPanel.Dialogs {
    public class PidConfigDlgVM : INotifyPropertyChanged {
        public PID_LOOP.Config Config { get; }

        public PidConfigDlgVM(PID_LOOP.Config config) {
            Config = config;
        }

        public string Title {
            get => Config.Title;
            set { Config.Title = value; OnPropertyChanged(nameof(Title)); }
        }

        public double Kp {
            get => Config.Kp;
            set { Config.Kp = value; OnPropertyChanged(nameof(Kp)); }
        }

        public double Ki {
            get => Config.Ki;
            set { Config.Ki = value; OnPropertyChanged(nameof(Ki)); }
        }

        public double Kd {
            get => Config.Kd;
            set { Config.Kd = value; OnPropertyChanged(nameof(Kd)); }
        }

        public double OutputMin {
            get => Config.OutputMin;
            set { Config.OutputMin = value; OnPropertyChanged(nameof(OutputMin)); }
        }

        public double OutputMax {
            get => Config.OutputMax;
            set { Config.OutputMax = value; OnPropertyChanged(nameof(OutputMax)); }
        }

        public double PVMin {
            get => Config.PVMin;
            set { Config.PVMin = value; OnPropertyChanged(nameof(PVMin)); }
        }

        public double PVMax {
            get => Config.PVMax;
            set { Config.PVMax = value; OnPropertyChanged(nameof(PVMax)); }
        }

        public double IntegralLimit {
            get => Config.IntegralLimit;
            set { Config.IntegralLimit = value; OnPropertyChanged(nameof(IntegralLimit)); }
        }

        public bool Auto {
            get => Config.Auto;
            set { Config.Auto = value; OnPropertyChanged(nameof(Auto)); }
        }

        public bool Enabled {
            get => Config.Enabled;
            set { Config.Enabled = value; OnPropertyChanged(nameof(Enabled)); }
        }

        public bool ResetOnModeChange {
            get => Config.ResetOnModeChange;
            set { Config.ResetOnModeChange = value; OnPropertyChanged(nameof(ResetOnModeChange)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
