using System;

namespace SigCoreCommon {
    public class PidLoopModel {
        // === Runtime State ===
        private double _integral = 0.0;
        private double _previousError = 0.0;
        private readonly PIDVals curVals;
        private double _rampStartSP = 0.0;
        private double _rampElapsed = 0.0;

        public PidLoopModel(PIDVals vals) {
            curVals = vals;
            _integral = 0.0;
            _previousError = 0.0;
        }

        public void Reset() {
            _integral = 0.0;
            _previousError = 0.0;
            _rampElapsed = 0.0;
        }

        public bool Compute(double dt) {
            bool result = false;

            lock (curVals.Lock) {
                if (!curVals.Enabled || !curVals.Auto || dt <= 0.0)
                    return false;

                // === Handle Ramping ===
                if (curVals.Ramp && curVals.RampTimeSec > 0.0) {
                    // Start ramp if just initiated
                    if (_rampElapsed == 0.0)
                        _rampStartSP = curVals.SP;

                    _rampElapsed += dt;

                    double t = Math.Min(_rampElapsed / curVals.RampTimeSec, 1.0);
                    curVals.SP = _rampStartSP + (curVals.RampTarget - _rampStartSP) * t;

                    // Ramp complete
                    if (t >= 1.0) {
                        curVals.SP = curVals.RampTarget;
                        curVals.Ramp = false;
                        _rampElapsed = 0.0;
                    }
                }

                // Clamp PV within limits
                if (curVals.PVMin < curVals.PVMax)
                    curVals.PV = Math.Clamp(curVals.PV, curVals.PVMin, curVals.PVMax);

                double error = curVals.SP - curVals.PV;
                double absDiff = Math.Abs(error);
                curVals.Dev = (curVals.Tol > 0.0) ? absDiff / curVals.Tol : 0.0;

                // === Proportional ===
                double P = curVals.Kp * error;

                // === Derivative ===
                double derivative = (error - _previousError) / dt;
                double D = curVals.Kd * derivative;

                // === Integral ===
                double outputEstimate = P + curVals.Ki * _integral + D;
                bool atUpperLimit = outputEstimate >= curVals.OutputMax;
                bool atLowerLimit = outputEstimate <= curVals.OutputMin;

                if (!((atUpperLimit && error > 0.0) || (atLowerLimit && error < 0.0))) {
                    _integral += error * dt;
                }

                _integral = Math.Clamp(_integral, -curVals.IntegralLimit, curVals.IntegralLimit);
                double I = curVals.Ki * _integral;
                double rawOutput = P + I + D;

                // Clamp final output
                bool saturated = false;
                if (curVals.OutputMin < curVals.OutputMax) {
                    double clamped = Math.Clamp(rawOutput, curVals.OutputMin, curVals.OutputMax);
                    saturated = clamped != rawOutput;
                    rawOutput = clamped;
                }

                curVals.Output = rawOutput;
                _previousError = error;

                // === Update monitor ===
                curVals.Monitor.Error = error;
                curVals.Monitor.P = P;
                curVals.Monitor.I = I;
                curVals.Monitor.D = D;
                curVals.Monitor.IntegralRaw = _integral;
                curVals.Monitor.AtUpperLimit = atUpperLimit;
                curVals.Monitor.AtLowerLimit = atLowerLimit;
                curVals.Monitor.Saturated = saturated;

                result = true;
            }

            return result;
        }

        public void ResetIntegralToOutput() {
            lock (curVals.Lock) {
                _integral = curVals.Output / (curVals.Ki != 0 ? curVals.Ki : 1.0);
                _integral = Math.Clamp(_integral, -curVals.IntegralLimit, curVals.IntegralLimit);
            }
        }
    }

    // === Runtime monitor for debug/telemetry ===
    public class PidMonitor {
        public double Error { get; set; }
        public double P { get; set; }
        public double I { get; set; }
        public double D { get; set; }
        public double IntegralRaw { get; set; }
        public bool AtUpperLimit { get; set; }
        public bool AtLowerLimit { get; set; }
        public bool Saturated { get; set; }
    }

    public abstract class PIDVals {
        protected readonly object SyncRoot = new object();

        public abstract double Output { get; set; }

        public virtual double PV { get; set; }
        public virtual double SP { get; set; }
        public virtual double Tol { get; set; }
        public virtual double Dev { get; set; }
        public virtual bool Auto { get; set; }
        public virtual bool Enabled { get; set; }
        public virtual double RampTimeSec { get; set; }
        public virtual double RampTarget { get; set; }
        public virtual bool Ramp { get; set; }

        public virtual double Kp { get; set; }
        public virtual double Ki { get; set; }
        public virtual double Kd { get; set; }

        // === Anti-windup parameters ===
        public virtual double IntegralLimit { get; set; } = 1000.0;
        public virtual bool ResetOnModeChange { get; set; } = false;

        // === Output & PV bounds ===
        public virtual double OutputMin { get; set; } = 0.0;
        public virtual double OutputMax { get; set; } = 100.0;
        public virtual double PVMin { get; set; } = 0.0;
        public virtual double PVMax { get; set; } = 100.0;

        // === Runtime monitor snapshot ===
        public virtual PidMonitor Monitor { get; } = new PidMonitor();

        public object Lock => SyncRoot;
    }
}
