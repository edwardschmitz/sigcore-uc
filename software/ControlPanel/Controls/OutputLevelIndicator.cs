using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControlPanel.Controls {
    public class OutputLevelIndicator : Control {
        static OutputLevelIndicator() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(OutputLevelIndicator),
                new FrameworkPropertyMetadata(typeof(OutputLevelIndicator)));
        }

        // Renamed properties to avoid conflicts
        public static readonly DependencyProperty OutputMinimumProperty =
            DependencyProperty.Register(nameof(OutputMinimum), typeof(double), typeof(OutputLevelIndicator),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty OutputMaximumProperty =
            DependencyProperty.Register(nameof(OutputMaximum), typeof(double), typeof(OutputLevelIndicator),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CrossoverProperty =
            DependencyProperty.Register(nameof(Crossover), typeof(double), typeof(OutputLevelIndicator),
                new FrameworkPropertyMetadata(50.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty OutputValueProperty =
            DependencyProperty.Register(nameof(OutputValue), typeof(double), typeof(OutputLevelIndicator),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TickIntervalProperty =
            DependencyProperty.Register(nameof(TickInterval), typeof(double), typeof(OutputLevelIndicator),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double OutputMinimum {
            get => (double)GetValue(OutputMinimumProperty);
            set => SetValue(OutputMinimumProperty, value);
        }

        public double OutputMaximum {
            get => (double)GetValue(OutputMaximumProperty);
            set => SetValue(OutputMaximumProperty, value);
        }

        public double Crossover {
            get => (double)GetValue(CrossoverProperty);
            set => SetValue(CrossoverProperty, value);
        }

        public double OutputValue {
            get => (double)GetValue(OutputValueProperty);
            set => SetValue(OutputValueProperty, value);
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

            double range = OutputMaximum - OutputMinimum;
            if (range <= 0) return;

            // Clamp Crossover and OutputValue within range
            double clampedCrossover = Math.Max(OutputMinimum, Math.Min(OutputMaximum, Crossover));
            double clampedOutput = Math.Max(OutputMinimum, Math.Min(OutputMaximum, OutputValue));

            // Calculate Y positions
            double crossoverY = height * (1 - (clampedCrossover - OutputMinimum) / range);
            double outputY = height * (1 - (clampedOutput - OutputMinimum) / range);

            // Draw the output bar from crossover toward clamped output value
            double barTop = Math.Min(crossoverY, outputY);
            double barHeight = Math.Abs(outputY - crossoverY);

            dc.DrawRectangle(Brushes.LightGray, null,
                new Rect(0.5, barTop, width - 1, barHeight));

            // Draw crossover line
            dc.DrawLine(new Pen(Brushes.Black, 1), new Point(0, crossoverY), new Point(width, crossoverY));

            // Draw tick marks
            if (TickInterval > 0) {
                for (double v = OutputMinimum; v <= OutputMaximum; v += TickInterval) {
                    double y = height * (1 - (v - OutputMinimum) / range);
                    dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(0, y), new Point(width * 0.2, y));
                }
            }
        }
    }
}
