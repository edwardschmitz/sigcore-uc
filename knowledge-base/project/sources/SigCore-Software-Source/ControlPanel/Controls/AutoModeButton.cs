using System.Windows;
using System.Windows.Controls.Primitives;

namespace ControlPanel.Controls {
    public class AutoModeButton : ToggleButton {

        public static readonly DependencyProperty OnTextProperty =
            DependencyProperty.Register(nameof(OnText), typeof(string), typeof(AutoModeButton),
                new PropertyMetadata("AUTO"));

        public static readonly DependencyProperty OffTextProperty =
            DependencyProperty.Register(nameof(OffText), typeof(string), typeof(AutoModeButton),
                new PropertyMetadata("MANUAL"));

        public string OnText {
            get { return (string)GetValue(OnTextProperty); }
            set { SetValue(OnTextProperty, value); }
        }

        public string OffText {
            get { return (string)GetValue(OffTextProperty); }
            set { SetValue(OffTextProperty, value); }
        }

        static AutoModeButton() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoModeButton),
                new FrameworkPropertyMetadata(typeof(AutoModeButton)));
        }
    }
}
