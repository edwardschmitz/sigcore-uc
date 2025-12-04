using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControlPanel.Controls {
    public class PVLevelIndicator : Control {
        static PVLevelIndicator() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PVLevelIndicator),
                new FrameworkPropertyMetadata(typeof(PVLevelIndicator)));
        }

        public static readonly DependencyProperty PVProperty =
            DependencyProperty.Register("PV", typeof(double), typeof(PVLevelIndicator),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SetpointProperty =
            DependencyProperty.Register("Setpoint", typeof(double), typeof(PVLevelIndicator),
                new FrameworkPropertyMetadata(50.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TickIntervalProperty =
            DependencyProperty.Register("TickInterval", typeof(double), typeof(PVLevelIndicator),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PVMinimumProperty =
            DependencyProperty.Register("PVMinimum", typeof(double), typeof(PVLevelIndicator),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PVMaximumProperty =
            DependencyProperty.Register("PVMaximum", typeof(double), typeof(PVLevelIndicator),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));
        public double PVMinimum {
            get => (double)GetValue(PVMinimumProperty);
            set => SetValue(PVMinimumProperty, value);
        }

        public double PVMaximum {
            get => (double)GetValue(PVMaximumProperty);
            set => SetValue(PVMaximumProperty, value);
        }

        public double PV {
            get => (double)GetValue(PVProperty);
            set => SetValue(PVProperty, value);
        }

        public double Setpoint {
            get => (double)GetValue(SetpointProperty);
            set => SetValue(SetpointProperty, value);
        }

        public double TickInterval {
            get => (double)GetValue(TickIntervalProperty);
            set => SetValue(TickIntervalProperty, value);
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;

            // Draw background and border
            dc.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 1), new Rect(0, 0, width, height));

            double range = PVMaximum - PVMinimum;
            if (range <= 0) return;

            // Clamp PV
            double pvRatio = (PV - PVMinimum) / range;
            pvRatio = Math.Max(0, Math.Min(1, pvRatio));
            double pvY = height * (1 - pvRatio);

            // Draw PV level as red fill from bottom up
            dc.DrawRectangle(Brushes.Red, null, new Rect(0.5, pvY, width - 1, height - pvY));

            // Draw setpoint marker
            double spRatio = (Setpoint - PVMinimum) / range;
            spRatio = Math.Max(0, Math.Min(1, spRatio));
            double spY = height * (1 - spRatio);

            dc.DrawLine(new Pen(Brushes.Black, 1), new Point(0, spY), new Point(width, spY));

            // Draw tick marks
            if (TickInterval > 0) {
                for (double v = PVMinimum; v <= PVMaximum; v += TickInterval) {
                    double y = height * (1 - (v - PVMinimum) / range);
                    dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(0, y), new Point(width * 0.2, y));
                }
            }
        }
    }
}
