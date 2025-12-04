using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

namespace SigCoreCommon {
    public class SigCoreDiscovery {
        private readonly List<IZeroconfHost> _discoveredDevices = new List<IZeroconfHost>();
        private readonly string _serviceType = "_sigcore._tcp.local."; // Replace with your service type
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);

        public event Action<IZeroconfHost> DeviceDiscovered;

        public void Start() {
            _ = Task.Run(async () => {
                while (!_stopEvent.WaitOne(0)) {
                    try {
                        IReadOnlyList<IZeroconfHost> results = await ZeroconfResolver.ResolveAsync(_serviceType);

                        foreach (IZeroconfHost host in results) {
                            if (!_discoveredDevices.Exists(d => d.IPAddress == host.IPAddress)) {
                                _discoveredDevices.Add(host);
                                DeviceDiscovered?.Invoke(host);
                            }
                        }
                    } catch (Exception ex) {
                        Console.WriteLine($"[SigCoreDiscovery] Error: {ex.Message}");
                    }

                    await Task.Delay(5000); // Re-scan every 5 seconds
                }
            });
        }

        public void Stop() {
            _stopEvent.Set();
        }

        public IReadOnlyList<IZeroconfHost> GetDiscoveredDevices() {
            return _discoveredDevices.AsReadOnly();
        }
    }
}
