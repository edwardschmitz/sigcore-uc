using System.Collections.Generic;
using System.Threading;
using ControlPanel.Models;
//using LabJack.LabJackUD;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
//using static ControlPanelLib.ControlPanelManager;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace ControlPanelLib.Models {
    //public class SystemModel {
    //    private ModbusTcpServerWrapper server { get; set; }
    //    private readonly Dictionary<string, ControlPanelManager> Instances = new Dictionary<string, ControlPanelManager>();
    //    private readonly List<PidLoopModel> Pids = new List<PidLoopModel>();
    //    private Timer _systemTimer;
    //    private bool _systemRunning = false;
    //    private readonly object _syncLock = new object();
    //    public event EventHandler PidListChanged;

    //    Stopwatch stopwatch = Stopwatch.StartNew();
    //    public void StartSystem(int intervalMs = 250) {
    //        if (_systemRunning) return;
    //        _systemRunning = true;

    //        _systemTimer = new Timer(_ => {
    //            lock (_syncLock) {
    //                foreach (var manager in Instances.Values) {
    //                    manager.PollingLoop();
    //                }

    //                double dt = stopwatch.Elapsed.TotalSeconds;
    //                stopwatch.Restart(); 

    //                foreach (var pid in Pids) {
    //                    if (pid.TryParsePvSource(out string sn, out string ch) &&
    //                        TryGetValue(sn, ch, out double pv)) {
    //                        pid.UpdateProcessVariable(pv);
    //                    }

    //                    if (pid.IsAutoMode) {
    //                        pid.Compute(dt);
    //                    }

    //                    if (pid.TryParseOutputDestination(out string sn2, out string ch2)) {
    //                        TrySetValue(sn2, ch2, pid.Output);
    //                    }
    //                }
    //            }
    //        }, null, 0, intervalMs); // Start immediately, repeat every interval
    //    }
    //    public void StopSystem() {
    //        _systemRunning = false;
    //        _systemTimer?.Dispose();
    //    }

    //    public SystemModel() {
    //        server = new ModbusTcpServerWrapper();
    //    }

    //    public ControlPanelManager GetInstance(string serial) {
    //        return Instances.ContainsKey(serial) ? Instances[serial] : null;
    //    }

    //    public List<ControlPanelManager> GetAllInstances() {
    //        List<ControlPanelManager> instances = new List<ControlPanelManager>();
    //        foreach (KeyValuePair<string, ControlPanelManager> kvp in Instances) {
    //            instances.Add(kvp.Value);
    //        }
    //        return instances;
    //    }

    //    public List<PidLoopModel> GetPidLoops() {
    //        return new List<PidLoopModel>(Pids);
    //    }

    //    public ModbusTcpServerWrapper Server => server;

    //    public void FindControlPanels() {
    //        Instances.Clear();
    //        List<Units> controlPanels = new List<Units>();

    //        try {
    //            int numDevices = 0;
    //            int[] aSerialNumbers = new int[128];
    //            double[] aAddresses = new double[128];
    //            int[] IDs = new int[128];

    //            LJUD.ListAll(LJUD.DEVICE.U3, LJUD.CONNECTION.USB, ref numDevices, aSerialNumbers, IDs, aAddresses);

    //            for (int i = 0; i < numDevices; i++) {
    //                try {
    //                    string lj_serial = aSerialNumbers[i].ToString();
    //                    U3 u3 = new U3(LJUD.CONNECTION.USB, lj_serial, false);

    //                    (string cp_part, string cp_serial) = ControlPanelManager.ReadStrings(u3);

    //                    controlPanels.Add(new Units {
    //                        CPPartNum = cp_part,
    //                        CPSerialNum = cp_serial,
    //                        LJSerialNum = lj_serial
    //                    });
    //                } catch (LabJackUDException ex) {
    //                    Console.WriteLine($"LabJackUDException for device {aSerialNumbers[i]}: {ex.Message}");
    //                } catch (Exception ex) {
    //                    Console.WriteLine($"General exception for device {aSerialNumbers[i]}: {ex.Message}");
    //                }
    //            }
    //        } catch (LabJackUDException ex) {
    //            Console.WriteLine($"LabJackUDException during device listing: {ex.Message}");
    //        } catch (Exception ex) {
    //            Console.WriteLine($"General exception during device listing: {ex.Message}");
    //        }

    //        foreach (Units unit in controlPanels) {
    //            ControlPanelManager panel = new ControlPanelManager(unit);
    //            panel.LoadConfig();
    //            Instances.Add(unit.CPSerialNum, panel);
    //        }
    //    }

    //    public void Save() {
    //        try {
    //            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "en-Z-em", "PartD88A42");
    //            Directory.CreateDirectory(basePath);
    //            string path = Path.Combine(basePath, "System.json");

    //            SystemState state = new SystemState {
    //                Server = server,
    //                Pids = Pids
    //            };

    //            string json = JsonConvert.SerializeObject(state, Formatting.Indented);
    //            File.WriteAllText(path, json);

    //            foreach (ControlPanelManager manager in Instances.Values) {
    //                manager.SaveConfig();
    //            }
    //        } catch (Exception ex) {
    //            Console.WriteLine("Error saving SystemModel: " + ex.Message);
    //        }
    //    }

    //    public void Load() {
    //        try {
    //            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "en-Z-em", "PartD88A42");
    //            string path = Path.Combine(basePath, "System.json");

    //            if (File.Exists(path)) {
    //                string json = File.ReadAllText(path);
    //                SystemState state = JsonConvert.DeserializeObject<SystemState>(json);

    //                if (state != null) {
    //                    server = state.Server ?? new ModbusTcpServerWrapper();
    //                    Pids.Clear();
    //                    if (state.Pids != null) {
    //                        Pids.AddRange(state.Pids);
    //                        PidListChanged?.Invoke(this, EventArgs.Empty);
    //                    }
    //                }
    //            }

    //            Instances.Clear();
    //            FindControlPanels();

    //        } catch (Exception ex) {
    //            Console.WriteLine("Error loading SystemModel: " + ex.Message);
    //        }
    //    }

    //    public void AddPID(PidLoopModel pidLoopModel) {
    //        Pids.Add(pidLoopModel);
    //        PidListChanged?.Invoke(this, EventArgs.Empty);
    //    }

    //    private class SystemState {
    //        public ModbusTcpServerWrapper Server { get; set; }
    //        public List<PidLoopModel> Pids { get; set; }
    //    }
    //    public void RemovePID(PidLoopModel pidLoopModel) {
    //        if (Pids.Contains(pidLoopModel)) {
    //            Pids.Remove(pidLoopModel);
    //            PidListChanged?.Invoke(this, EventArgs.Empty);
    //        }
    //    }
    //    public bool TryGetValue(string serialNumber, string channelId, out double value) {
    //        value = double.NaN;

    //        if (!Instances.ContainsKey(serialNumber)) {
    //            return false;
    //        }

    //        ControlPanelManager manager = Instances[serialNumber];
    //        return manager.TryGetValue(channelId, out value);
    //    }

    //    public bool TrySetValue(string serialNumber, string channelId, double newValue) {
    //        if (!Instances.ContainsKey(serialNumber)) {
    //            return false;
    //        }

    //        ControlPanelManager manager = Instances[serialNumber];
    //        return manager.TrySetValue(channelId, newValue);
    //    }
    //}
}
