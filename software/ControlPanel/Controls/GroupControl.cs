using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ControlPanel.Controls {
    public class GroupControl : HeaderedContentControl {
        static GroupControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GroupControl),
                new FrameworkPropertyMetadata(typeof(GroupControl)));
        }

        public bool IsCollapsible {
            get => (bool)GetValue(IsCollapsibleProperty);
            set => SetValue(IsCollapsibleProperty, value);
        }

        public static readonly DependencyProperty IsCollapsibleProperty =
            DependencyProperty.Register(
                nameof(IsCollapsible),
                typeof(bool),
                typeof(GroupControl),
                new PropertyMetadata(false));

        // 🔽 New property to open/close the group
        public bool IsExpanded {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(
                nameof(IsExpanded),
                typeof(bool),
                typeof(GroupControl),
                new PropertyMetadata(true));
    }
}
