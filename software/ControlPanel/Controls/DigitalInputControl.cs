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
    public class DigitalInputControl : Control {
        static DigitalInputControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DigitalInputControl),
                new FrameworkPropertyMetadata(typeof(DigitalInputControl)));
        }

        public static readonly DependencyProperty IsInputActiveProperty =
            DependencyProperty.Register("IsInputActive", typeof(bool), typeof(DigitalInputControl),
                new PropertyMetadata(false));

        public bool IsInputActive {
            get => (bool)GetValue(IsInputActiveProperty);
            set => SetValue(IsInputActiveProperty, value);
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(DigitalInputControl),
                new PropertyMetadata("Digital Input"));

        public string Label {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty UserIDProperty =
            DependencyProperty.Register("UserID", typeof(string), typeof(DigitalInputControl),
                new FrameworkPropertyMetadata(
                    "",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault
                ));

        public string UserID {
            get => (string)GetValue(UserIDProperty);
            set => SetValue(UserIDProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(DigitalInputControl),
                new PropertyMetadata(null));

        public ICommand Command {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }
        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
        }
        public static readonly DependencyProperty ConfigCommandProperty =
           DependencyProperty.Register(nameof(ConfigCommand), typeof(ICommand), typeof(DigitalInputControl),
               new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public ICommand ConfigCommand {
            get => (ICommand)GetValue(ConfigCommandProperty);
            set => SetValue(ConfigCommandProperty, value);
        }
        public static readonly DependencyProperty ChannelProperty =
            DependencyProperty.Register(nameof(Channel), typeof(uint), typeof(DigitalInputControl),
                new FrameworkPropertyMetadata(0u, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public uint Channel {
            get => (uint)GetValue(ChannelProperty);
            set => SetValue(ChannelProperty, value);
        }
    }
}
