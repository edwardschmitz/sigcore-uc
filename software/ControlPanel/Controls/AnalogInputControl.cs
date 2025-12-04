using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ControlPanel.Controls {
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Globalization;
    using System.Windows.Controls.Primitives;
    using System.Diagnostics;
    using System.ComponentModel;

    public class AnalogInputControl : Control, INotifyPropertyChanged {
        static AnalogInputControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AnalogInputControl),
                new FrameworkPropertyMetadata(typeof(AnalogInputControl)));
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(AnalogInputControl),
                new PropertyMetadata(0.0, OnValueOrPrecisionChanged));

        public double Value {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty PrecisionProperty =
            DependencyProperty.Register(nameof(Precision), typeof(int), typeof(AnalogInputControl),
                new PropertyMetadata(2, OnValueOrPrecisionChanged));

        private static void OnValueOrPrecisionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is AnalogInputControl ctrl)
                ctrl.RaisePropertyChanged(nameof(FormattedValue));
        }
        private static void OnDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is AnalogInputControl ctrl)
                ctrl.RaisePropertyChanged(nameof(FormattedValue));
        }

        public int Precision {
            get => (int)GetValue(PrecisionProperty);
            set => SetValue(PrecisionProperty, value);
        }


        public string FormattedValue {
            get {
                switch (Display) {
                    case 1: // Scientific
                        return Value.ToString($"E{Precision}");
                    default: // Fixed
                        return Value.ToString($"F{Precision}");
                }
            }
        }


        public static readonly DependencyProperty DisplayProperty =
           DependencyProperty.Register(nameof(Display), typeof(int), typeof(AnalogInputControl),
               new PropertyMetadata(0, OnDisplayChanged));

        public int Display {
            get => (int)GetValue(DisplayProperty);
            set => SetValue(DisplayProperty, value);
        }


        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(AnalogInputControl),
                new PropertyMetadata("Analog Input"));

        public string Label {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty UserIDProperty =
            DependencyProperty.Register("UserID", typeof(string), typeof(AnalogInputControl),
                new FrameworkPropertyMetadata(
                    "",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault
                ));

        public string UserID {
            get => (string)GetValue(UserIDProperty);
            set => SetValue(UserIDProperty, value);
        }

        public static readonly DependencyProperty UnitsProperty =
            DependencyProperty.Register("Units", 
                typeof(string), 
                typeof(AnalogInputControl),
                new FrameworkPropertyMetadata(
                    "V",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault
                ));

        public string Units {
            get => (string)GetValue(UnitsProperty);
            set => SetValue(UnitsProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(AnalogInputControl),
                new PropertyMetadata(null));
        public static readonly DependencyProperty Input1Property =
            DependencyProperty.Register("Input1",
                typeof(double),
                typeof(AnalogInputControl),
                new FrameworkPropertyMetadata(
                    0.0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault
                ));

        public static readonly DependencyProperty ConfigCommandProperty =
            DependencyProperty.Register(nameof(ConfigCommand), typeof(ICommand), typeof(AnalogInputControl),
               new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public ICommand ConfigCommand {
            get => (ICommand)GetValue(ConfigCommandProperty);
            set => SetValue(ConfigCommandProperty, value);
        }
        public static readonly DependencyProperty ChannelProperty =
            DependencyProperty.Register(nameof(Channel), typeof(uint), typeof(AnalogInputControl),
                new FrameworkPropertyMetadata(0u, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public uint Channel {
            get => (uint)GetValue(ChannelProperty);
            set => SetValue(ChannelProperty, value);
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }
    public class UnitsHasParenthesis : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is string unit) {
                return unit.Length>0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
    public class CalEnabledToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool isCalEnabled) {
                return isCalEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
