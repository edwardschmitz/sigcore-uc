using System;
using System.ComponentModel;
using System.Windows.Threading;
using ControlPanel.Models;

namespace ControlPanel.ViewModels {
    //public class PidLoopViewModel : INotifyPropertyChanged {
    //    public PidLoopModel Model { get; private set; }
    //    private DispatcherTimer _timer;

    //    private readonly Action<PidLoopViewModel> _removeCallback;

    //    public PidLoopViewModel(PidLoopModel model, Action<PidLoopViewModel> removeCallback = null) {
    //        Model = model;
    //        _removeCallback = removeCallback;
    //        _timer = new DispatcherTimer();
    //        _timer.Interval = TimeSpan.FromSeconds(.1);
    //        _timer.Tick += Timer_Tick;
    //        _timer.Start();

    //        RemoveCmd = new RelayCommand(Remove);
    //    }
    //    public PidLoopViewModel(PidLoopModel model) {
    //        Model = model;
    //        _timer = new DispatcherTimer();
    //        _timer.Interval = TimeSpan.FromSeconds(.1);
    //        _timer.Tick += Timer_Tick;
    //        _timer.Start();

    //        RemoveCmd = new RelayCommand(Remove); 
    //    }

    //    private void Remove() {
    //        _timer.Stop();
    //        _removeCallback?.Invoke(this);
    //    }

    //    private void Timer_Tick(object sender, EventArgs e) {
    //        if (Model.IsAutoMode) {
    //            OnPropertyChanged(nameof(Output));
    //        }

    //        OnPropertyChanged(nameof(ProcessValue));
    //    }

    //    // === BINDABLE PROPERTIES ===
    //    public RelayCommand RemoveCmd { get; private set; }

    //    public double Output {
    //        get => Model.Output;
    //        set {
    //            if (Model.Output != value) {
    //                Model.Output = value;
    //                OnPropertyChanged(nameof(Output));
    //            }
    //        }
    //    }

    //    public double Setpoint {
    //        get => Model.Setpoint;
    //        set { if (Model.Setpoint != value) { Model.Setpoint = value; OnPropertyChanged(nameof(Setpoint)); } }
    //    }

    //    public double Tolerance {
    //        get => Model.Tolerance;
    //        set { if (Model.Tolerance != value) { Model.Tolerance = value; OnPropertyChanged(nameof(Tolerance)); } }
    //    }

    //    public double ProcessValue {
    //        get => Model.ProcessVariable;
    //        set { if (Model.ProcessVariable != value) { Model.UpdateProcessVariable(value); OnPropertyChanged(nameof(ProcessValue)); } }
    //    }

    //    public bool IsAutoMode {
    //        get => Model.IsAutoMode;
    //        set { if (Model.IsAutoMode != value) { Model.IsAutoMode = value; OnPropertyChanged(nameof(IsAutoMode)); } }
    //    }

    //    public string PvSource {
    //        get => Model.PvSource;
    //        set { if (Model.PvSource != value) { Model.PvSource = value; OnPropertyChanged(nameof(PvSource)); } }
    //    }

    //    public string OutputDestination {
    //        get => Model.OutputDestination;
    //        set { if (Model.OutputDestination != value) { Model.OutputDestination = value; OnPropertyChanged(nameof(OutputDestination)); } }
    //    }

    //    public string Title {
    //        get => Model.Title;
    //        set { if (Model.Title != value) { Model.Title = value; OnPropertyChanged(nameof(Title)); } }
    //    }

    //    public double Kp {
    //        get => Model.Kp;
    //        set { if (Model.Kp != value) { Model.Kp = value; OnPropertyChanged(nameof(Kp)); } }
    //    }

    //    public double Ki {
    //        get => Model.Ki;
    //        set { if (Model.Ki != value) { Model.Ki = value; OnPropertyChanged(nameof(Ki)); } }
    //    }

    //    public double Kd {
    //        get => Model.Kd;
    //        set { if (Model.Kd != value) { Model.Kd = value; OnPropertyChanged(nameof(Kd)); } }
    //    }

    //    // 🎉 RENAMED AND CONSOLIDATED PROPERTIES:
    //    public double OutputValueMinimum {
    //        get => Model.OutputMin;
    //        set { if (Model.OutputMin != value) { Model.OutputMin = value; OnPropertyChanged(nameof(OutputValueMinimum)); } }
    //    }

    //    public double OutputValueMaximum {
    //        get => Model.OutputMax;
    //        set { if (Model.OutputMax != value) { Model.OutputMax = value; OnPropertyChanged(nameof(OutputValueMaximum)); } }
    //    }

    //    public bool ReverseAction {
    //        get => Model.ReverseAction;
    //        set { if (Model.ReverseAction != value) { Model.ReverseAction = value; OnPropertyChanged(nameof(ReverseAction)); } }
    //    }

    //    public double Deadband {
    //        get => Model.Deadband;
    //        set { if (Model.Deadband != value) { Model.Deadband = value; OnPropertyChanged(nameof(Deadband)); } }
    //    }

    //    public double IntegralZone {
    //        get => Model.IntegralZone;
    //        set { if (Model.IntegralZone != value) { Model.IntegralZone = value; OnPropertyChanged(nameof(IntegralZone)); } }
    //    }

    //    public bool AntiWindupEnabled {
    //        get => Model.AntiWindupEnabled;
    //        set { if (Model.AntiWindupEnabled != value) { Model.AntiWindupEnabled = value; OnPropertyChanged(nameof(AntiWindupEnabled)); } }
    //    }

    //    public bool DerivativeOnPV {
    //        get => Model.DerivativeOnPV;
    //        set { if (Model.DerivativeOnPV != value) { Model.DerivativeOnPV = value; OnPropertyChanged(nameof(DerivativeOnPV)); } }
    //    }

    //    public double FilterFactor {
    //        get => Model.FilterFactor;
    //        set { if (Model.FilterFactor != value) { Model.FilterFactor = value; OnPropertyChanged(nameof(FilterFactor)); } }
    //    }

    //    public double DutyCycle {
    //        get => Model.DutyCycle;
    //        set { if (Model.DutyCycle != value) { Model.DutyCycle = value; OnPropertyChanged(nameof(DutyCycle)); } }
    //    }

    //    public double RampTarget {
    //        get => Model.RampTarget;
    //        set { if (Model.RampTarget != value) { Model.RampTarget = value; OnPropertyChanged(nameof(RampTarget)); } }
    //    }

    //    public double RampRatePerSecond {
    //        get => Model.RampRatePerSecond;
    //        set { if (Model.RampRatePerSecond != value) { Model.RampRatePerSecond = value; OnPropertyChanged(nameof(RampRatePerSecond)); } }
    //    }

    //    public double ProcessValueMinimum {
    //        get => Model.PVMin;
    //        set { if (Model.PVMin != value) { Model.PVMin = value; OnPropertyChanged(nameof(ProcessValueMinimum)); } }
    //    }

    //    public double ProcessValueMaximum {
    //        get => Model.PVMax;
    //        set { if (Model.PVMax != value) { Model.PVMax = value; OnPropertyChanged(nameof(ProcessValueMaximum)); } }
    //    }

    //    public double Crossover {
    //        get => Model.Crossover;
    //        set { if (Model.Crossover != value) { Model.Crossover = value; OnPropertyChanged(nameof(Crossover)); } }
    //    }

    //    public event PropertyChangedEventHandler PropertyChanged;
    //    private void OnPropertyChanged(string propertyName) =>
    //        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    //}
}
