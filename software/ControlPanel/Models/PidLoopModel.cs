using System;
using System.Timers;

namespace ControlPanel.Models {
    public class PidLoopModel {
        // === Configuration ===
        public string Title { get; set; } = "";

        public double Kp { get; set; } = 10.0;
        public double Ki { get; set; } = 0.1;
        public double Kd { get; set; } = 0.0;

        public double OutputMin { get; set; } = 0.0;
        public double OutputMax { get; set; } = 5.0;

        public double PVMin { get; set; } = 0.0;
        public double PVMax { get; set; } = 5.0;

        /// <summary>
        /// Used only when ReverseAction is true. If Setpoint < Crossover, control direction is flipped.
        /// </summary>
        public double Crossover { get; set; } = 0.0;

        public bool ReverseAction { get; set; } = false;

        public double Deadband { get; set; } = 0.0;

        /// <summary>
        /// Maximum error range for integrating. Set to 0 to disable zone check (always integrate).
        /// </summary>
        public double IntegralZone { get; set; } = 0.0;

        public bool AntiWindupEnabled { get; set; } = true;
        public bool DerivativeOnPV { get; set; } = true;
        public double FilterFactor { get; set; } = 1.0;

        public double DutyCycle { get; set; } = 100.0;


        public bool IsAutoMode { get; set; } = true;

        public string PvSource { get; set; } = "";
        public string OutputDestination { get; set; } = "";

        // === State ===
        public double Setpoint { get; set; } = 0.0;
        public double ProcessVariable { get; private set; } = 0.0;

        private double _output = 0.0;
        public double Output {
            get => _output;
            set {
                if (!IsAutoMode) {
                    _output = value;
                }
            }
        }
        public double Tolerance { get; set; } = 0.0;

        private double _integral = 0.0;
        private double _previousError = 0.0;
        private double _previousPV = 0.0;
        private double _filteredDerivative = 0.0;

        // === Ramp ===
        public double RampTarget { get; set; } = 0.0;
        public double RampRatePerSecond { get; set; } = 0.0;
        private bool _isRamping => RampRatePerSecond > 0 && Math.Abs(Setpoint - RampTarget) > 0.001;


        // === Events ===
        public event EventHandler OutputChanged;

        public PidLoopModel() {
        }


        public void UpdateRamp() {
            if (!_isRamping) return;

            // this needs to be fixed
            // double delta = RampRatePerSecond * SampleTimeSeconds;
            //if (RampTarget > Setpoint)
            //    Setpoint = Math.Min(Setpoint + delta, RampTarget);
            //else
            //    Setpoint = Math.Max(Setpoint - delta, RampTarget);
        }

        public void UpdateProcessVariable(double pv) {
            ProcessVariable = pv;
        }

        public void Reset() {
            _integral = 0.0;
            _previousError = 0.0;
            _previousPV = ProcessVariable;
            _filteredDerivative = 0.0;
        }

        public void Compute(double dt) {
            if (!IsAutoMode) return;

            double error = Setpoint - ProcessVariable;

            if (ReverseAction) {
                if (Setpoint < Crossover) {
                    error = -error;
                }
            }

            // Deadband
            if (Math.Abs(error) < Deadband)
                error = 0;

            // Proportional term
            double P = Kp * error;

            if (IntegralZone == 0.0 || Math.Abs(error) < IntegralZone)
                _integral += error * dt;
            else
                _integral = 0;
            double I = Ki * _integral;

            double derivative = DerivativeOnPV
                ? -(ProcessVariable - _previousPV) / dt
                : (error - _previousError) / dt;

            _filteredDerivative = (_filteredDerivative * (FilterFactor - 1) + derivative) / FilterFactor;
            double D = Kd * _filteredDerivative;

            double rawOutput = P + I + D;

            // Anti-windup
            if (AntiWindupEnabled)
                rawOutput = Math.Max(OutputMin, Math.Min(OutputMax, rawOutput));

            _output = rawOutput;

            // Save for next cycle
            _previousError = error;
            _previousPV = ProcessVariable;
        }

        public bool TryParsePvSource(out string serialNumber, out string channel) {
            serialNumber = "";
            channel = "";

            if (string.IsNullOrWhiteSpace(PvSource))
                return false;

            string[] parts = PvSource.Split(':');
            if (parts.Length != 2)
                return false;

            serialNumber = parts[0];
            channel = parts[1];
            return true;
        }

        public bool TryParseOutputDestination(out string serialNumber, out string channel) {
            serialNumber = "";
            channel = "";

            if (string.IsNullOrWhiteSpace(OutputDestination))
                return false;

            string[] parts = OutputDestination.Split(':');
            if (parts.Length != 2)
                return false;

            serialNumber = parts[0];
            channel = parts[1];
            return true;
        }
    }
}
