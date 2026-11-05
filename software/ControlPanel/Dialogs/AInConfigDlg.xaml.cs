using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ControlPanel.Dialogs {
    public partial class AInConfigDlg : Window {
        private readonly AInConfigDlgVM vm;
        private readonly List<TextBox> coefficients = new List<TextBox>();
        private readonly List<(TextBox Input, TextBox Output)> points = new List<(TextBox, TextBox)>();

        public AInConfigDlg(AInConfigPayload config) {
            InitializeComponent();
            vm = new AInConfigDlgVM(config);
            DataContext = vm;
            // Keep editable text out of the payload until both integers are validated.
            AveragingSamplesBox.Text = vm.AveragingSamples.ToString(CultureInfo.InvariantCulture);
            PrecisionBox.Text = vm.Precision.ToString(CultureInfo.InvariantCulture);
            LoadCalibrationEditors();
            UpdateCalibrationVisibility(vm.CalibrationType);
            vm.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(AInConfigDlgVM.CalibrationType))
                    UpdateCalibrationVisibility(vm.CalibrationType);
                if (e.PropertyName == nameof(AInConfigDlgVM.Units))
                    PointOutputHeader.Text = string.IsNullOrWhiteSpace(vm.Units) ? "Output" : "Output (" + vm.Units + ")";
            };
            PointOutputHeader.Text = string.IsNullOrWhiteSpace(vm.Units) ? "Output" : "Output (" + vm.Units + ")";
        }

        private static bool TryNumber(string text, out double value) {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void LoadCalibrationEditors() {
            string polynomial = vm.PolynomialCoefficientsString ?? "";
            foreach (string term in polynomial.Split(',')) {
                if (!string.IsNullOrWhiteSpace(term)) AddCoefficient(term.Trim());
            }
            if (coefficients.Count == 0) AddCoefficient("0");
            RefreshCoefficients();

            string piecewise = vm.PiecewisePairsString ?? "";
            foreach (string pair in piecewise.Split(',')) {
                if (string.IsNullOrWhiteSpace(pair)) continue;
                string[] xy = pair.Trim().Split(':');
                if (xy.Length == 2) AddPoint(xy[0].Trim(), xy[1].Trim());
                else AddPoint(pair.Trim(), ""); // retain invalid input visibly for correction
            }
            if (points.Count == 0) { AddPoint("0", "0"); AddPoint("1", "1"); }
            else if (points.Count == 1) AddPoint("", "");
            RefreshPoints();
        }

        private TextBox NumericBox(string initial, int width = 130) {
            return new TextBox {
                Text = initial, Width = width, Margin = new Thickness(3, 2, 3, 2),
                Padding = new Thickness(5, 3, 5, 3),
                HorizontalAlignment = HorizontalAlignment.Left
            };
        }

        private void AddCoefficient(string value) {
            var box = NumericBox(value, 170);
            box.TextChanged += (s, e) => RefreshEquation();
            coefficients.Add(box);
        }

        private void RefreshEquation() {
            if (PolynomialEquation == null) return;
            PolynomialEquation.Text = "y = " + string.Join(" + ", coefficients.Select((c, i) =>
                "(" + c.Text + ")" + (i == 0 ? "" : i == 1 ? "x" : "x^" + i)));
        }

        private void RefreshCoefficients() {
            // TextBoxes cannot be reparented while they still belong to the old rows.
            // Keep the text, but rebuild the controls together with their rows.
            var values = coefficients.Select(c => c.Text).ToList();
            PolynomialRows.Children.Clear();
            coefficients.Clear();
            for (int i = 0; i < values.Count; i++) {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock {
                    Text = i == 0 ? "Constant" : i == 1 ? "x" : "x^" + i,
                    Width = 125, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(3, 0, 0, 0)
                });
                var box = NumericBox(values[i], 170);
                box.TextChanged += (s, e) => RefreshEquation();
                coefficients.Add(box);
                row.Children.Add(box);
                PolynomialRows.Children.Add(row);
            }
            RefreshEquation();
        }

        private void AddTerm_Click(object sender, RoutedEventArgs e) {
            AddCoefficient("0"); RefreshCoefficients();
        }
        private void RemoveTerm_Click(object sender, RoutedEventArgs e) {
            if (coefficients.Count <= 1) return;
            coefficients.RemoveAt(coefficients.Count - 1);
            RefreshCoefficients();
        }

        private void AddPoint(string x, string y) {
            points.Add((NumericBox(x), NumericBox(y)));
        }
        private void RefreshPoints() {
            // Keep entered values, but create fresh TextBoxes for each new row.
            // Reusing the old TextBoxes causes WPF's parent/child exception.
            var values = points.Select(p => (X: p.Input.Text, Y: p.Output.Text)).ToList();
            MultiPointRows.Children.Clear();
            points.Clear();
            for (int i = 0; i < values.Count; i++) {
                int index = i;
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock {
                    Text = (i + 1).ToString(CultureInfo.InvariantCulture), Width = 40,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0)
                });
                var input = NumericBox(values[i].X);
                var output = NumericBox(values[i].Y);
                points.Add((input, output));
                row.Children.Add(input);
                row.Children.Add(output);
                var remove = new Button { Content = "Remove", Padding = new Thickness(5, 2, 5, 2), Margin = new Thickness(3, 2, 0, 2) };
                remove.IsEnabled = values.Count > 2;
                remove.Click += (s, e) => { points.RemoveAt(index); RefreshPoints(); };
                row.Children.Add(remove);
                MultiPointRows.Children.Add(row);
            }
        }
        private void AddPoint_Click(object sender, RoutedEventArgs e) {
            AddPoint("", ""); RefreshPoints();
        }
        private void SortPoints_Click(object sender, RoutedEventArgs e) {
            if (points.Any(p => !TryNumber(p.Input.Text, out _))) {
                MessageBox.Show(this, "Enter valid numeric inputs before sorting.", "Calibration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            points.Sort((a, b) => double.Parse(a.Input.Text, CultureInfo.InvariantCulture)
                .CompareTo(double.Parse(b.Input.Text, CultureInfo.InvariantCulture)));
            RefreshPoints();
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            // Reject invalid numeric input before touching the configuration payload.
            if (!int.TryParse(AveragingSamplesBox.Text.Trim(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int averagingSamples) ||
                averagingSamples < 1 || averagingSamples > 100) {
                MessageBox.Show(this, "Averaging must be a whole number from 1 to 100.",
                    "Analog Input Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                AveragingSamplesBox.Focus();
                AveragingSamplesBox.SelectAll();
                return;
            }
            if (!int.TryParse(PrecisionBox.Text.Trim(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int precision) ||
                precision < 0 || precision > 9) {
                MessageBox.Show(this, "Precision must be a whole number from 0 to 9.",
                    "Analog Input Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                PrecisionBox.Focus();
                PrecisionBox.SelectAll();
                return;
            }

            // Calibration slope and offset are directly bound doubles. Invalid text can otherwise
            // leave the last valid property value in the payload without warning the user.
            if (MxBPanel.Visibility == Visibility.Visible &&
                (!DialogInputValidation.Validate(this,
                    DialogInputValidation.NumericRule.Real("M", "Calibration slope (M)"),
                    DialogInputValidation.NumericRule.Real("B", "Calibration offset (B)")) ||
                 !DialogInputValidation.CommitBindings(this)))
                return;

            switch ((A_IN.CalType)vm.CalibrationType) {
                case A_IN.CalType.Polynomial:
                    if (coefficients.Any(c => !TryNumber(c.Text, out _))) {
                        MessageBox.Show(this, "Every polynomial coefficient must be a valid number.", "Calibration", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    vm.PolynomialCoefficientsString = string.Join(", ", coefficients.Select(c => c.Text.Trim()));
                    break;
                case A_IN.CalType.MultiPoint:
                    if (points.Count < 2 || points.Any(p => !TryNumber(p.Input.Text, out _) || !TryNumber(p.Output.Text, out _))) {
                        MessageBox.Show(this, "Enter at least two points with valid numeric input and output values.", "Calibration", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (points.Select(p => double.Parse(p.Input.Text, CultureInfo.InvariantCulture)).Distinct().Count() != points.Count) {
                        MessageBox.Show(this, "Each calibration input must be unique.", "Calibration", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    // Store points in increasing input order for predictable interpolation.
                    vm.PiecewisePairsString = string.Join(", ", points.OrderBy(p => double.Parse(p.Input.Text, CultureInfo.InvariantCulture))
                        .Select(p => p.Input.Text.Trim() + ":" + p.Output.Text.Trim()));
                    break;
            }
            vm.AveragingSamples = averagingSamples;
            vm.Precision = precision;
            DialogResult = true;
        }

        private void UpdateCalibrationVisibility(int calType) {
            MxBPanel.Visibility = Visibility.Collapsed;
            PolynomialPanel.Visibility = Visibility.Collapsed;
            MultiPointPanel.Visibility = Visibility.Collapsed;
            switch ((A_IN.CalType)calType) {
                case A_IN.CalType.mXplusB:
                    MxBPanel.Visibility = Visibility.Visible;
                    FormulaText.Text = "y = m·x + b";
                    break;
                case A_IN.CalType.Log10:
                    MxBPanel.Visibility = Visibility.Visible;
                    FormulaText.Text = "y = 10^(m·x + b)";
                    break;
                case A_IN.CalType.MultiPoint:
                    MultiPointPanel.Visibility = Visibility.Visible;
                    break;
                case A_IN.CalType.Polynomial:
                    PolynomialPanel.Visibility = Visibility.Visible;
                    break;
            }
        }
    }
}
