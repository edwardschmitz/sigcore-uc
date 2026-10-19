using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

// A single SigCoreCommon connection and the latest subscribed operating state.
internal sealed class CoreState {
    public readonly SigCoreSystem core = new SigCoreSystem();
    public readonly Log log;
public readonly object stateLock = new object();

public bool[] digitalInputs = Array.Empty<bool>();
public double[] analogInputs = Array.Empty<double>();
public bool[] relays = new bool[8];
public double[] analogOutputs = new double[4];
public bool[] analogOutputAuto = new bool[4];

public Dictionary<uint, double> pidSetPoints = new Dictionary<uint, double>();
public Dictionary<uint, double> pidProcessValues = new Dictionary<uint, double>();
public Dictionary<uint, double> pidOutputs = new Dictionary<uint, double>();
public Dictionary<uint, double> pidTolerances = new Dictionary<uint, double>();
public Dictionary<uint, double> pidDeviations = new Dictionary<uint, double>();
public Dictionary<uint, bool> pidAuto = new Dictionary<uint, bool>();
public Dictionary<uint, double> pidRampTimes = new Dictionary<uint, double>();
public Dictionary<uint, double> pidRampTargets = new Dictionary<uint, double>();
public Dictionary<uint, bool> pidRampEnabled = new Dictionary<uint, bool>();

public string loggerStatus = string.Empty;
public bool loggerActive = false;

    public CoreState(Log logger) {
        log = logger;
    core.DigitalInChanged += values => {
        lock (stateLock) {
            digitalInputs = (bool[])values.Clone();
        }
    };
    
    core.AnalogInChanged += values => {
        lock (stateLock) {
            analogInputs = (double[])values.Clone();
        }
    };
    
    core.RelayChanged += (channel, state) => {
        lock (stateLock) {
            EnsureLength(ref relays, (int)channel + 1);
            relays[channel] = state;
        }
    };
    
    core.AnalogOutChanged += (channel, value, auto) => {
        lock (stateLock) {
            EnsureLength(ref analogOutputs, (int)channel + 1);
            EnsureLength(ref analogOutputAuto, (int)channel + 1);
    
            analogOutputs[channel] = value;
            analogOutputAuto[channel] = auto;
        }
    };
    
    core.PIDSPChanged += (channel, value) => {
        lock (stateLock) {
            pidSetPoints[channel] = value;
        }
    };
    
    core.PIDPVChanged += (channel, value) => {
        lock (stateLock) {
            pidProcessValues[channel] = value;
        }
    };
    
    core.PIDOutputChanged += (channel, value) => {
        lock (stateLock) {
            pidOutputs[channel] = value;
        }
    };
    
    core.PIDTolChanged += (channel, value) => {
        lock (stateLock) {
            pidTolerances[channel] = value;
        }
    };
    
    core.PIDDevChanged += (channel, value) => {
        lock (stateLock) {
            pidDeviations[channel] = value;
        }
    };
    
    core.PIDAutoChanged += (channel, value) => {
        lock (stateLock) {
            pidAuto[channel] = value;
        }
    };
    
    core.PIDRampTimeChanged += (channel, value) => {
        lock (stateLock) {
            pidRampTimes[channel] = value;
        }
    };
    
    core.PIDRampTargetChanged += (channel, value) => {
        lock (stateLock) {
            pidRampTargets[channel] = value;
        }
    };
    
    core.PIDRampChanged += (channel, value) => {
        lock (stateLock) {
            pidRampEnabled[channel] = value;
        }
    };
    
    core.LoggerStatusChanged += (status, logging) => {
        lock (stateLock) {
            loggerStatus = status;
            loggerActive = logging;
        }
    };
    
    core.ConnectionStateChanged += state => {
        Console.WriteLine($"[Bridge] SigCore connection state: {state}");
    };
    
    core.ConnectionWarning += warning => {
        Console.WriteLine($"[Bridge] SigCore warning: {warning}");
    };
    }

    public void Start() {
        _ = Task.Run(async () => {
            try {
                await core.StartAsync("127.0.0.1", 7020);
                Console.WriteLine("[Bridge] Connected to SigCore Core Server.");
            } catch (Exception ex) {
                Console.WriteLine("[Bridge] SigCore connection failed: " + ex.Message);
            }
        });
    }

    public void Stop() {
        Console.WriteLine("[Bridge] Stopping...");
        core.Stop();
        core.Dispose();
    }

    public static void EnsureLength<T>(ref T[] array, int length) {
        if (array.Length < length) Array.Resize(ref array, length);
    }
}
