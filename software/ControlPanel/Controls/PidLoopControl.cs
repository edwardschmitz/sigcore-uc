using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ControlPanel.Controls {
    public class PidLoopControl : Control {
        static PidLoopControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PidLoopControl),
                new FrameworkPropertyMetadata(typeof(PidLoopControl)));

        }

        // Dependency Properties
        public static readonly DependencyProperty SetpointProperty = DependencyProperty.Register("Setpoint", typeof(double), typeof(PidLoopControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty ProcessValueProperty = DependencyProperty.Register("ProcessValue", typeof(double), typeof(PidLoopControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty OutputProperty = DependencyProperty.Register("Output", typeof(double), typeof(PidLoopControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty IsAutoModeProperty = DependencyProperty.Register("IsAutoMode", typeof(bool), typeof(PidLoopControl), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty ToleranceProperty = DependencyProperty.Register("Tolerance", typeof(double), typeof(PidLoopControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty RampTargetProperty = DependencyProperty.Register("RampTarget", typeof(double), typeof(PidLoopControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty RampTimeProperty = DependencyProperty.Register("RampTime", typeof(double), typeof(PidLoopControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty RampProperty = DependencyProperty.Register("Ramp", typeof(bool), typeof(PidLoopControl), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty CrossoverProperty = DependencyProperty.Register("Crossover", typeof(double), typeof(PidLoopControl), new PropertyMetadata(50.0));
        public static readonly DependencyProperty TickIntervalProperty = DependencyProperty.Register("TickInterval", typeof(double), typeof(PidLoopControl), new PropertyMetadata(10.0));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(PidLoopControl), new PropertyMetadata("PID Loop"));

        public static readonly DependencyProperty KpProperty = DependencyProperty.Register("Kp", typeof(double), typeof(PidLoopControl), new PropertyMetadata(0.0));
        public static readonly DependencyProperty KiProperty = DependencyProperty.Register("Ki", typeof(double), typeof(PidLoopControl), new PropertyMetadata(0.0));
        public static readonly DependencyProperty KdProperty = DependencyProperty.Register("Kd", typeof(double), typeof(PidLoopControl), new PropertyMetadata(0.0));
        public static readonly DependencyProperty OutputMinProperty = DependencyProperty.Register("OutputMin", typeof(double), typeof(PidLoopControl), new PropertyMetadata(0.0));
        public static readonly DependencyProperty OutputMaxProperty = DependencyProperty.Register("OutputMax", typeof(double), typeof(PidLoopControl), new PropertyMetadata(100.0));
        public static readonly DependencyProperty ReverseActionProperty = DependencyProperty.Register("ReverseAction", typeof(bool), typeof(PidLoopControl), new PropertyMetadata(false));
        public static readonly DependencyProperty SampleTimeProperty = DependencyProperty.Register("SampleTime", typeof(double), typeof(PidLoopControl), new PropertyMetadata(1000.0));
        public static readonly DependencyProperty DutyCycleProperty = DependencyProperty.Register("DutyCycle", typeof(double), typeof(PidLoopControl), new PropertyMetadata(1000.0));
        public static readonly DependencyProperty AntiWindupEnabledProperty = DependencyProperty.Register("AntiWindupEnabled", typeof(bool), typeof(PidLoopControl), new PropertyMetadata(false));
        public static readonly DependencyProperty DeadbandProperty = DependencyProperty.Register("Deadband", typeof(double), typeof(PidLoopControl), new PropertyMetadata(0.0));
        public static readonly DependencyProperty IntegralZoneProperty = DependencyProperty.Register("IntegralZone", typeof(double), typeof(PidLoopControl), new PropertyMetadata(0.0));
        public static readonly DependencyProperty DerivativeOnPVProperty = DependencyProperty.Register("DerivativeOnPV", typeof(bool), typeof(PidLoopControl), new PropertyMetadata(false));
        public static readonly DependencyProperty FilterFactorProperty = DependencyProperty.Register("FilterFactor", typeof(double), typeof(PidLoopControl), new PropertyMetadata(0.0));
        public static readonly DependencyProperty PvSourceProperty = DependencyProperty.Register("PvSource", typeof(string), typeof(PidLoopControl), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ConfigCommandProperty = DependencyProperty.Register("ConfigCommand", typeof(ICommand), typeof(PidLoopControl), new PropertyMetadata(null));
        public ICommand ConfigCommand {
            get => (ICommand)GetValue(ConfigCommandProperty);
            set => SetValue(ConfigCommandProperty, value);
        }

        public static readonly DependencyProperty OutputDestinationProperty =
            DependencyProperty.Register("OutputDestination", typeof(string), typeof(PidLoopControl), new PropertyMetadata(string.Empty));

        // CLR Wrappers
        public string PvSource { get => (string)GetValue(PvSourceProperty); set => SetValue(PvSourceProperty, value); }
        public string OutputDestination { get => (string)GetValue(OutputDestinationProperty); set => SetValue(OutputDestinationProperty, value); }
        public double Setpoint { get => (double)GetValue(SetpointProperty); set => SetValue(SetpointProperty, value); }
        public double ProcessValue { get => (double)GetValue(ProcessValueProperty); set => SetValue(ProcessValueProperty, value); }
        public double Output { get => (double)GetValue(OutputProperty); set => SetValue(OutputProperty, value); }
        public bool IsAutoMode { get => (bool)GetValue(IsAutoModeProperty); set => SetValue(IsAutoModeProperty, value); }
        public double Tolerance { get => (double)GetValue(ToleranceProperty); set => SetValue(ToleranceProperty, value); }
        public double RampTarget { get => (double)GetValue(RampTargetProperty); set => SetValue(RampTargetProperty, value); }
        public double RampTime { get => (double)GetValue(RampTimeProperty); set => SetValue(RampTimeProperty, value); }
        public bool Ramp { get => (bool)GetValue(RampProperty); set => SetValue(RampProperty, value); }
        public double Crossover { get => (double)GetValue(CrossoverProperty); set => SetValue(CrossoverProperty, value); }
        public double TickInterval { get => (double)GetValue(TickIntervalProperty); set => SetValue(TickIntervalProperty, value); }
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public double Kp { get => (double)GetValue(KpProperty); set => SetValue(KpProperty, value); }
        public double Ki { get => (double)GetValue(KiProperty); set => SetValue(KiProperty, value); }
        public double Kd { get => (double)GetValue(KdProperty); set => SetValue(KdProperty, value); }
        public double OutputMin { get => (double)GetValue(OutputMinProperty); set => SetValue(OutputMinProperty, value); }
        public double OutputMax { get => (double)GetValue(OutputMaxProperty); set => SetValue(OutputMaxProperty, value); }
        public bool ReverseAction { get => (bool)GetValue(ReverseActionProperty); set => SetValue(ReverseActionProperty, value); }
        public double SampleTime { get => (double)GetValue(SampleTimeProperty); set => SetValue(SampleTimeProperty, value); }
        public double DutyCycle { get => (double)GetValue(DutyCycleProperty); set => SetValue(DutyCycleProperty, value); }
        public bool AntiWindupEnabled { get => (bool)GetValue(AntiWindupEnabledProperty); set => SetValue(AntiWindupEnabledProperty, value); }
        public double Deadband { get => (double)GetValue(DeadbandProperty); set => SetValue(DeadbandProperty, value); }
        public double IntegralZone { get => (double)GetValue(IntegralZoneProperty); set => SetValue(IntegralZoneProperty, value); }
        public bool DerivativeOnPV { get => (bool)GetValue(DerivativeOnPVProperty); set => SetValue(DerivativeOnPVProperty, value); }
        public double FilterFactor { get => (double)GetValue(FilterFactorProperty); set => SetValue(FilterFactorProperty, value); }

        public static readonly DependencyProperty ProcessValueMinimumProperty =
           DependencyProperty.Register(nameof(ProcessValueMinimum), typeof(double), typeof(PidLoopControl), new PropertyMetadata(0.0));

        public static readonly DependencyProperty ProcessValueMaximumProperty =
            DependencyProperty.Register(nameof(ProcessValueMaximum), typeof(double), typeof(PidLoopControl), new PropertyMetadata(100.0));

        public static readonly DependencyProperty OutputValueMinimumProperty =
            DependencyProperty.Register(nameof(OutputValueMinimum), typeof(double), typeof(PidLoopControl), new PropertyMetadata(0.0));

        public static readonly DependencyProperty OutputValueMaximumProperty =
            DependencyProperty.Register(nameof(OutputValueMaximum), typeof(double), typeof(PidLoopControl), new PropertyMetadata(100.0));

        // CLR Wrappers:
        public double ProcessValueMinimum { get => (double)GetValue(ProcessValueMinimumProperty); set => SetValue(ProcessValueMinimumProperty, value); }
        public double ProcessValueMaximum { get => (double)GetValue(ProcessValueMaximumProperty); set => SetValue(ProcessValueMaximumProperty, value); }
        public double OutputValueMinimum { get => (double)GetValue(OutputValueMinimumProperty); set => SetValue(OutputValueMinimumProperty, value); }
        public double OutputValueMaximum { get => (double)GetValue(OutputValueMaximumProperty); set => SetValue(OutputValueMaximumProperty, value); }

        public static readonly DependencyProperty IsUiEnabledProperty =
            DependencyProperty.Register(
                nameof(IsUiEnabled),
                typeof(bool),
                typeof(PidLoopControl),
                new FrameworkPropertyMetadata(
                    true,
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnIsUiEnabledChanged));

        public bool IsUiEnabled {
            get { return (bool)GetValue(IsUiEnabledProperty); }
            set { SetValue(IsUiEnabledProperty, value); }
        }

        private static void OnIsUiEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            PidLoopControl control = (PidLoopControl)d;

            // If we're not on the UI thread, dispatch the update
            if (!control.Dispatcher.CheckAccess()) {
                control.Dispatcher.Invoke(() => MessageBox.Show($"IsUiEnabled changed to {e.NewValue}"));
            }

            bool enabled = (bool)e.NewValue;
            if (!enabled)
                control.IsAutoMode = false;

            control = (PidLoopControl)d;
            control.InvalidateVisual();
            control.InvalidateArrange();
        }
    }
    public class BooleanToAutoModeTextConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value is bool b && b) ? "Auto Mode: ON" : "Auto Mode: OFF";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
    public class AutoModeHoverBackgroundConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool b && b)
                return new SolidColorBrush(Color.FromRgb(180, 255, 180)); // lighter green
            return new SolidColorBrush(Color.FromRgb(255, 120, 120));    // lighter red
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
