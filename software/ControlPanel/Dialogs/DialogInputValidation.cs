using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ControlPanel.Dialogs {
    /// <summary>
    /// Validate the actual textbox text before allowing the bound payload to be submitted.
    /// WPF normally retains the previous numeric property value if text conversion fails;
    /// inspecting only the view model would miss that invalid input.
    /// </summary>
    internal static class DialogInputValidation {
        internal sealed class NumericRule {
            internal string PropertyName { get; }
            internal string Label { get; }
            internal bool Integer { get; }
            internal double Minimum { get; }
            internal double Maximum { get; }
            internal bool ExclusiveMinimum { get; }

            private NumericRule(string propertyName, string label, bool integer,
                                double minimum, double maximum, bool exclusiveMinimum) {
                PropertyName = propertyName;
                Label = label;
                Integer = integer;
                Minimum = minimum;
                Maximum = maximum;
                ExclusiveMinimum = exclusiveMinimum;
            }

            internal static NumericRule Whole(string propertyName, string label,
                                              int minimum = int.MinValue, int maximum = int.MaxValue) {
                return new NumericRule(propertyName, label, true, minimum, maximum, false);
            }

            internal static NumericRule Real(string propertyName, string label,
                                             double minimum = double.NegativeInfinity,
                                             double maximum = double.PositiveInfinity,
                                             bool exclusiveMinimum = false) {
                return new NumericRule(propertyName, label, false, minimum, maximum, exclusiveMinimum);
            }
        }

        internal static TextBox FindTextBox(DependencyObject root, string propertyName) {
            foreach (TextBox box in TextBoxes(root)) {
                var binding = BindingOperations.GetBinding(box, TextBox.TextProperty);
                if (binding?.Path?.Path == propertyName)
                    return box;
            }
            return null;
        }

        private static IEnumerable<TextBox> TextBoxes(DependencyObject root) {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                if (child is TextBox box)
                    yield return box;
                foreach (TextBox nested in TextBoxes(child))
                    yield return nested;
            }
        }

        internal static bool Reject(Window owner, TextBox box, string message) {
            MessageBox.Show(owner, message, owner.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            if (box != null) {
                box.Focus();
                box.SelectAll();
            }
            return false;
        }

        internal static bool Validate(Window owner, params NumericRule[] rules) {
            foreach (NumericRule rule in rules) {
                TextBox box = FindTextBox(owner, rule.PropertyName);
                if (box == null)
                    return Reject(owner, null, "Unable to locate the " + rule.Label + " input field.");

                string input = box.Text.Trim();
                CultureInfo culture = box.Language.GetSpecificCulture();
                bool parsed;
                double value;
                if (rule.Integer) {
                    parsed = int.TryParse(input, NumberStyles.Integer, culture, out int integer);
                    value = integer;
                } else {
                    parsed = double.TryParse(input, NumberStyles.Float, culture, out value);
                }
                if (!parsed || double.IsNaN(value) || double.IsInfinity(value) ||
                    value < rule.Minimum || value > rule.Maximum ||
                    (rule.ExclusiveMinimum && value == rule.Minimum)) {
                    string bounds = rule.Minimum == double.NegativeInfinity && rule.Maximum == double.PositiveInfinity
                        ? "a finite " + (rule.Integer ? "whole number" : "number")
                        : rule.Maximum == double.PositiveInfinity
                            ? (rule.ExclusiveMinimum ? "greater than " : "at least ") + rule.Minimum.ToString(culture)
                            : rule.Minimum == double.NegativeInfinity
                                ? "at most " + rule.Maximum.ToString(culture)
                                : "from " + rule.Minimum.ToString(culture) + " to " + rule.Maximum.ToString(culture);
                    return Reject(owner, box, rule.Label + " must be " + bounds + ".");
                }
            }
            return true;
        }

        /// <summary>Commit pending LostFocus bindings and flag any conversion failures.</summary>
        internal static bool CommitBindings(Window owner) {
            foreach (TextBox box in TextBoxes(owner)) {
                BindingExpression expression = box.GetBindingExpression(TextBox.TextProperty);
                if (expression == null)
                    continue;
                expression.UpdateSource();
                if (Validation.GetHasError(box))
                    return Reject(owner, box, "Check the value entered in the highlighted field.");
            }
            return true;
        }
    }
}
