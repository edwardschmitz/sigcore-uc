using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlPanel.Controls {
    public class RelayControl : Control, INotifyPropertyChanged {
        static RelayControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RelayControl),
                new FrameworkPropertyMetadata(typeof(RelayControl)));
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (GetTemplateChild("PopupButton") is ButtonBase popupButton) {
                popupButton.Click += (s, e) => {
                    if (ConfigCommand != null && ConfigCommand.CanExecute(null))
                        ConfigCommand.Execute(null);
                };
            }
        }

        // ===========================
        // Dependency Properties
        // ===========================

        public static readonly DependencyProperty IsRelayOnProperty =
            DependencyProperty.Register(
                nameof(IsRelayOn),
                typeof(bool),
                typeof(RelayControl),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnIsRelayOnChanged));

        public bool IsRelayOn {
            get { return (bool)GetValue(IsRelayOnProperty); }
            set { SetValue(IsRelayOnProperty, value); }
        }

        private static void OnIsRelayOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            RelayControl control = (RelayControl)d;

            bool newState = (bool)e.NewValue;
            control.OnRelayToggled(newState);

            control.OnPropertyChanged(nameof(IsNormallyOpenClosed));
            control.OnPropertyChanged(nameof(IsNormallyClosedClosed));
        }
        protected virtual void OnRelayToggled(bool isOn) {
            // optional LED or animation updates
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label),
                typeof(string),
                typeof(RelayControl),
                new PropertyMetadata("Relay"));

        public string Label {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public static readonly DependencyProperty UserIDProperty =
            DependencyProperty.Register(
                nameof(UserID),
                typeof(string),
                typeof(RelayControl),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string UserID {
            get { return (string)GetValue(UserIDProperty); }
            set { SetValue(UserIDProperty, value); }
        }

        public static readonly DependencyProperty ConfigCommandProperty =
            DependencyProperty.Register(
                nameof(ConfigCommand),
                typeof(ICommand),
                typeof(RelayControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public ICommand ConfigCommand {
            get { return (ICommand)GetValue(ConfigCommandProperty); }
            set { SetValue(ConfigCommandProperty, value); }
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(
                nameof(Command),
                typeof(ICommand),
                typeof(RelayControl),
                new PropertyMetadata(null));

        public ICommand Command {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty ChannelProperty =
            DependencyProperty.Register(
                nameof(Channel),
                typeof(uint),
                typeof(RelayControl),
                new FrameworkPropertyMetadata(0u, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public event PropertyChangedEventHandler? PropertyChanged;

        public uint Channel {
            get { return (uint)GetValue(ChannelProperty); }
            set { SetValue(ChannelProperty, value); }
        }
        public static readonly DependencyProperty InvertedLogicProperty =
          DependencyProperty.Register(
              nameof(InvertedLogic),
              typeof(bool),
              typeof(RelayControl),
              new FrameworkPropertyMetadata(
                  false,
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  OnInvertedLogicChanged));

        public bool InvertedLogic {
            get { return (bool)GetValue(InvertedLogicProperty); }
            set { SetValue(InvertedLogicProperty, value); }
        }

        private static void OnInvertedLogicChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            RelayControl control = (RelayControl)d;
            control.OnPropertyChanged(nameof(IsNormallyOpenClosed));
            control.OnPropertyChanged(nameof(IsNormallyClosedClosed));
        }
        public bool IsNormallyOpenClosed {
            get { return IsRelayOn ^ InvertedLogic; }
        }

        public bool IsNormallyClosedClosed {
            get { return !IsNormallyOpenClosed; }
        }

        private void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ===========================
    // Converters
    // ===========================
    public class BooleanToOffColorConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is bool isOn && isOn ? Brushes.Black : Brushes.LightGreen;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class InvertBooleanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is bool b ? !b : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is bool b ? !b : value;
        }
    }
}
