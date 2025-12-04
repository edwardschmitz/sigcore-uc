using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControlPanel.Controls {
    public class ToleranceIndicator : Control {
        static ToleranceIndicator() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToleranceIndicator),
                new FrameworkPropertyMetadata(typeof(ToleranceIndicator)));
        }

        public static readonly DependencyProperty ToleranceProperty =
            DependencyProperty.Register("Tolerance", typeof(float), typeof(ToleranceIndicator),
                new FrameworkPropertyMetadata(1.0f, FrameworkPropertyMetadataOptions.AffectsRender));

        public float Tolerance {
            get => (float)GetValue(ToleranceProperty);
            set => SetValue(ToleranceProperty, value);
        }

        public static readonly DependencyProperty SetpointProperty =
            DependencyProperty.Register("Setpoint", typeof(double), typeof(ToleranceIndicator),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Setpoint {
            get => (double)GetValue(SetpointProperty);
            set => SetValue(SetpointProperty, value);
        }

        public static readonly DependencyProperty ProcessValueProperty =
            DependencyProperty.Register("ProcessValue", typeof(double), typeof(ToleranceIndicator),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double ProcessValue {
            get => (double)GetValue(ProcessValueProperty);
            set => SetValue(ProcessValueProperty, value);
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            double centerY = height / 2;

            // Define zones
            double zone100 = width / 2 * 1.0; // ±100% zone
            double zone200 = width / 2 * 2.0; // ±200% zone

            // Draw 200% zone (red)
            dc.DrawRectangle(Brushes.Red, new Pen(Brushes.Black, 1),
                new Rect((width - zone200) / 2, 0, zone200, height));

            // Draw 100% zone (green overlay)
            dc.DrawRectangle(Brushes.LightGreen, null,
                new Rect((width - zone100) / 2, 0.5, zone100, height - 1));

            // Draw center line (zero error)
            dc.DrawLine(new Pen(Brushes.DarkGray, 1), new Point(width / 2, 0), new Point(width / 2, height));

            // Calculate error
            double error = ProcessValue - Setpoint;

            // Compute normalized value
            double normalized = error / Tolerance;
            bool isClampedLeft = false;
            bool isClampedRight = false;

            // Clamp normalized and set clamping flags
            if (normalized < -2.0) {
                normalized = -2.0;
                isClampedLeft = true;
            } else if (normalized > 2.0) {
                normalized = 2.0;
                isClampedRight = true;
            }

            // Map normalized to indicatorX
            double indicatorX = width / 2 + (normalized * width / 4);

            // Draw indicator
            if (isClampedLeft) {
                // Draw arrow at left edge pointing right
                Point p1 = new Point(0, height / 2);
                Point p2 = new Point(10, height / 2 - 5);
                Point p3 = new Point(10, height / 2 + 5);

                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open()) {
                    ctx.BeginFigure(p1, true, true);
                    ctx.LineTo(p2, true, false);
                    ctx.LineTo(p3, true, false);
                }
                dc.DrawGeometry(Brushes.Black, null, geometry);
            } else if (isClampedRight) {
                // Draw arrow at right edge pointing left
                Point p1 = new Point(width, height / 2);
                Point p2 = new Point(width - 10, height / 2 - 5);
                Point p3 = new Point(width - 10, height / 2 + 5);

                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open()) {
                    ctx.BeginFigure(p1, true, true);
                    ctx.LineTo(p2, true, false);
                    ctx.LineTo(p3, true, false);
                }
                dc.DrawGeometry(Brushes.Black, null, geometry);
            } else {
                // Draw normal vertical indicator line
                dc.DrawLine(new Pen(Brushes.Black, 2), new Point(indicatorX, 0), new Point(indicatorX, height));
            }
        }
    }
}
