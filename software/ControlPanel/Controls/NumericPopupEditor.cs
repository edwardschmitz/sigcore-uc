using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ControlPanel.Controls {
    public class NumericPopupEditor : Control {
        static NumericPopupEditor() {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(NumericPopupEditor),
                new FrameworkPropertyMetadata(typeof(NumericPopupEditor)));
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(NumericPopupEditor),
                new FrameworkPropertyMetadata(
                    0.0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public double Value {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty FormatProperty =
            DependencyProperty.Register(
                nameof(Format),
                typeof(string),
                typeof(NumericPopupEditor),
                new PropertyMetadata("F3"));

        public string Format {
            get => (string)GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        private Popup _popup;
        private TextBox _editBox;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            Border open = GetTemplateChild("PART_Open") as Border;
            _popup = GetTemplateChild("PART_Popup") as Popup;
            _editBox = GetTemplateChild("PART_EditBox") as TextBox;

            if (open != null && _popup != null) {
                open.MouseLeftButtonUp += (s, e) => {
                    _popup.IsOpen = true;

                    if (_editBox != null) {
                        _editBox.Focus();
                        _editBox.SelectAll();
                    }
                };
            }

            if (_editBox != null && _popup != null) {
                _editBox.KeyDown += (s, e) => {
                    if (e.Key == Key.Enter) {
                        _popup.IsOpen = false;
                        e.Handled = true;
                    }

                    if (e.Key == Key.Escape) {
                        _popup.IsOpen = false;
                        e.Handled = true;
                    }
                };
            }
        }
    }
}