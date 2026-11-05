using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ControlPanel.Controls {

    public class AnalogOutputControl : Control, INotifyPropertyChanged {

        static AnalogOutputControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AnalogOutputControl),
                new FrameworkPropertyMetadata(typeof(AnalogOutputControl)));
        }

        public AnalogOutputControl() { }

        // ------------------------------------------------------------
        // 1) DISPLAY VALUE  (One-way, server -> control)
        // ------------------------------------------------------------
        public string DisplayValue {
            get => (string)GetValue(DisplayValueProperty);
            set => SetValue(DisplayValueProperty, value);
        }

        public static readonly DependencyProperty DisplayValueProperty =
            DependencyProperty.Register(nameof(DisplayValue), typeof(string),
                typeof(AnalogOutputControl),
                new PropertyMetadata("0.000"));

        // ------------------------------------------------------------
        // 2) EDIT VALUE  (Two-way, popup -> control -> server)
        // ------------------------------------------------------------
        public double EditValue {
            get => (double)GetValue(EditValueProperty);
            set => SetValue(EditValueProperty, value);
        }

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(nameof(EditValue), typeof(double),
                typeof(AnalogOutputControl),
                new PropertyMetadata(0.0));

        // ------------------------------------------------------------
        // Commands / Units / Labels / Channel
        // ------------------------------------------------------------
        public string Label {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string),
                typeof(AnalogOutputControl),
                new PropertyMetadata("Analog Output"));

        public string UserID {
            get => (string)GetValue(UserIDProperty);
            set => SetValue(UserIDProperty, value);
        }

        public static readonly DependencyProperty UserIDProperty =
            DependencyProperty.Register(nameof(UserID), typeof(string),
                typeof(AnalogOutputControl),
                new PropertyMetadata(""));

        public string Units {
            get => (string)GetValue(UnitsProperty);
            set => SetValue(UnitsProperty, value);
        }

        public static readonly DependencyProperty UnitsProperty =
            DependencyProperty.Register(nameof(Units), typeof(string),
                typeof(AnalogOutputControl),
                new PropertyMetadata("V"));

        public ICommand ConfigCommand {
            get => (ICommand)GetValue(ConfigCommandProperty);
            set => SetValue(ConfigCommandProperty, value);
        }

        public static readonly DependencyProperty ConfigCommandProperty =
            DependencyProperty.Register(nameof(ConfigCommand), typeof(ICommand),
                typeof(AnalogOutputControl), new PropertyMetadata(null));

        public uint Channel {
            get => (uint)GetValue(ChannelProperty);
            set => SetValue(ChannelProperty, value);
        }

        public static readonly DependencyProperty ChannelProperty =
            DependencyProperty.Register(nameof(Channel), typeof(uint),
                typeof(AnalogOutputControl), new PropertyMetadata(0u));

        public bool IsInputEnabled {
            get => (bool)GetValue(IsInputEnabledProperty);
            set => SetValue(IsInputEnabledProperty, value);
        }

        public static readonly DependencyProperty IsInputEnabledProperty =
            DependencyProperty.Register(nameof(IsInputEnabled), typeof(bool),
                typeof(AnalogOutputControl), new PropertyMetadata(true));

        public double MinSlider {
            get => (double)GetValue(MinSliderProperty);
            set => SetValue(MinSliderProperty, value);
        }

        public static readonly DependencyProperty MinSliderProperty =
            DependencyProperty.Register(nameof(MinSlider), typeof(double),
                typeof(AnalogOutputControl), new PropertyMetadata(0.0));

        public double MaxSlider {
            get => (double)GetValue(MaxSliderProperty);
            set => SetValue(MaxSliderProperty, value);
        }

        public static readonly DependencyProperty MaxSliderProperty =
            DependencyProperty.Register(nameof(MaxSlider), typeof(double),
                typeof(AnalogOutputControl), new PropertyMetadata(10.0));

        // ------------------------------------------------------------
        // TEMPLATE HOOKUP
        // ------------------------------------------------------------
        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            AnalogOutputControl self = this;

            FrameworkElement clickable =
                GetTemplateChild("PART_OpenPopup") as FrameworkElement;

            Popup popup =
                GetTemplateChild("PART_Popup") as Popup;

            TextBox editBox =
                GetTemplateChild("PART_EditBox") as TextBox;

            Slider slider =
                GetTemplateChild("PART_Slider") as Slider;

            // ------------------------------------------------------------
            // OPEN POPUP
            // ------------------------------------------------------------
            if (clickable != null && popup != null) {
                clickable.MouseLeftButtonUp +=
                    (object sender, MouseButtonEventArgs e) => {
                        popup.IsOpen = true;

                        if (editBox != null) {
                            editBox.Text = self.EditValue.ToString();
                            editBox.Focus();
                            editBox.SelectAll();
                        }
                    };
            }

            // ------------------------------------------------------------
            // TEXTBOX: ENTER commits + closes popup
            // ------------------------------------------------------------
            if (editBox != null) {

                editBox.KeyDown +=
                    (object sender, KeyEventArgs e) => {
                        if (e.Key == Key.Enter) {
                            BindingExpression binding =
                                editBox.GetBindingExpression(TextBox.TextProperty);

                            if (binding != null) {
                                binding.UpdateSource();      // push EditValue -> VM
                            }

                            self.RaiseEditCommitted(self.EditValue);

                            if (popup != null) {
                                popup.IsOpen = false;
                            }

                            e.Handled = true;
                        }
                    };

                // Losing focus also commits
                editBox.LostKeyboardFocus +=
                    (object sender, KeyboardFocusChangedEventArgs e) => {
                        BindingExpression binding =
                            editBox.GetBindingExpression(TextBox.TextProperty);

                        if (binding != null) {
                            binding.UpdateSource();
                        }

                        self.RaiseEditCommitted(self.EditValue);
                    };
            }

            // ------------------------------------------------------------
            // SLIDER: updates EditValue (through TextBox binding)
            // ------------------------------------------------------------
            if (slider != null && editBox != null) {
                slider.ValueChanged +=
                    (object sender, RoutedPropertyChangedEventArgs<double> e) => {
                        editBox.Text = slider.Value.ToString();

                        BindingExpression binding =
                            editBox.GetBindingExpression(TextBox.TextProperty);

                        if (binding != null) {
                            binding.UpdateSource();
                        }

                        self.RaiseEditCommitted(self.EditValue);
                    };
            }
        }

        // ------------------------------------------------------------
        // EVENT: popup committed
        // ------------------------------------------------------------
        public event Action<double> EditCommitted;

        private void RaiseEditCommitted(double v) {
            EditCommitted?.Invoke(v);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
