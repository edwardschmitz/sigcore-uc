using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControlPanel.Controls {
    public class LedIndicator : Control, INotifyPropertyChanged {
        static LedIndicator() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LedIndicator),
                new FrameworkPropertyMetadata(typeof(LedIndicator)));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsOn {
            get { return (bool)GetValue(IsOnProperty); }
            set {
                SetValue(IsOnProperty, value);
                OnPropertyChanged(nameof(IsOn));
                OnPropertyChanged(nameof(CurrentColor)); // Notify UI when changing
            }
        }

        public static readonly DependencyProperty IsOnProperty =
            DependencyProperty.Register(nameof(IsOn), typeof(bool),
                typeof(LedIndicator), new PropertyMetadata(false, OnIsOnChanged));

        private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is LedIndicator led) {
                led.OnPropertyChanged(nameof(CurrentColor));
            }
        }

        public string Label {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string),
                typeof(LedIndicator), new PropertyMetadata("R1"));

        public Brush OnColor {
            get { return (Brush)GetValue(OnColorProperty); }
            set { SetValue(OnColorProperty, value); }
        }

        public static readonly DependencyProperty OnColorProperty =
            DependencyProperty.Register(nameof(OnColor), typeof(Brush),
                typeof(LedIndicator), new PropertyMetadata(Brushes.LimeGreen));

        public Brush OffColor {
            get { return (Brush)GetValue(OffColorProperty); }
            set { SetValue(OffColorProperty, value); }
        }

        public static readonly DependencyProperty OffColorProperty =
            DependencyProperty.Register(nameof(OffColor), typeof(Brush),
                typeof(LedIndicator), new PropertyMetadata(Brushes.DarkGray));

        // Computed Property for UI Binding
        public Brush CurrentColor => IsOn ? OnColor : OffColor;
    }
}
