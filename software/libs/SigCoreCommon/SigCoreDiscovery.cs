using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Zeroconf;

namespace SigCoreCommon {
    public sealed class SigCoreDevice {
        public string Name { get; init; } = "";
        public string Id { get; init; } = "";

        public string? Address { get; init; }
        public IReadOnlyList<string> AdvertisedAddresses { get; init; } = Array.Empty<string>();

        public string? Version { get; init; }
        public int ServicePort { get; init; }
        public int ControlPort { get; init; }

        public bool Ready { get; init; }

        public IZeroconfHost RawHost { get; init; } = default!;
    }

    public class SigCoreDiscovery {
        private readonly string _serviceType = "_sigcore._tcp.local.";

        private const int ControlPort = 7020;
        private const int PingTimeoutMs = 1000;
        private const int TcpTimeoutMs = 1000;

        public async Task<IReadOnlyList<SigCoreDevice>> DiscoverAsync() {
            IReadOnlyList<IZeroconfHost> hosts =
                await ZeroconfResolver.ResolveAsync(_serviceType);

            List<SigCoreDevice> devices = new();

            foreach (IZeroconfHost host in hosts) {
                SigCoreDevice? device = await BuildDeviceAsync(host);

                if (device != null)
                    devices.Add(device);
            }

            return devices.AsReadOnly();
        }

        private static async Task<SigCoreDevice?> BuildDeviceAsync(IZeroconfHost host) {
            List<string> addresses = GetAdvertisedAddresses(host);

            int servicePort = GetAdvertisedServicePort(host);
            string? version = GetServiceProperty(host, "version");

            string? selectedAddress = null;
            bool ready = false;

            foreach (string address in OrderAddresses(addresses)) {
                bool pingOk = await CanPingAsync(address, PingTimeoutMs);

                if (!pingOk)
                    continue;

                selectedAddress = address;
                ready = await CanConnectAsync(address, ControlPort, TcpTimeoutMs);

                break;
            }

            if (selectedAddress == null)
                return null;

            return new SigCoreDevice {
                Name = host.DisplayName ?? host.Id ?? "SigCore UC",
                Id = host.Id ?? host.DisplayName ?? Guid.NewGuid().ToString(),

                Address = selectedAddress,
                AdvertisedAddresses = addresses,

                Version = version,
                ServicePort = servicePort,
                ControlPort = ControlPort,

                Ready = ready,
                RawHost = host
            };
        }

        private static List<string> GetAdvertisedAddresses(IZeroconfHost host) {
            HashSet<string> addresses = new(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(host.IPAddress))
                addresses.Add(host.IPAddress);

            if (host.IPAddresses != null) {
                foreach (string address in host.IPAddresses) {
                    if (!string.IsNullOrWhiteSpace(address))
                        addresses.Add(address);
                }
            }

            return addresses.ToList();
        }

        private static IEnumerable<string> OrderAddresses(IEnumerable<string> addresses) {
            return addresses
                .Select(address => {
                    IPAddress.TryParse(address, out IPAddress? parsed);
                    return new { Text = address, Parsed = parsed };
                })
                .Where(x => x.Parsed != null)
                .OrderByDescending(x => x.Parsed!.AddressFamily == AddressFamily.InterNetwork)
                .ThenBy(x => IsLinkLocal(x.Parsed!))
                .Select(x => x.Text);
        }

        private static bool IsLinkLocal(IPAddress address) {
            if (address.AddressFamily == AddressFamily.InterNetwork)
                return address.ToString().StartsWith("169.254.");

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return address.IsIPv6LinkLocal;

            return false;
        }

        private static int GetAdvertisedServicePort(IZeroconfHost host) {
            try {
                return host.Services?.Values.FirstOrDefault()?.Port ?? 0;
            } catch {
                return 0;
            }
        }

        private static string? GetServiceProperty(IZeroconfHost host, string key) {
            try {
                var service = host.Services?.Values.FirstOrDefault();
                var propertySet = service?.Properties?.FirstOrDefault();

                if (propertySet == null)
                    return null;

                return propertySet.TryGetValue(key, out string? value)
                    ? value
                    : null;
            } catch {
                return null;
            }
        }

        private static async Task<bool> CanPingAsync(string address, int timeoutMs) {
            try {
                using Ping ping = new();
                PingReply reply = await ping.SendPingAsync(address, timeoutMs);

                return reply.Status == IPStatus.Success;
            } catch {
                return false;
            }
        }

        private static async Task<bool> CanConnectAsync(string address, int port, int timeoutMs) {
            try {
                using TcpClient client = new();

                Task connectTask = client.ConnectAsync(address, port);
                Task timeoutTask = Task.Delay(timeoutMs);

                Task completed = await Task.WhenAny(connectTask, timeoutTask);

                if (completed != connectTask)
                    return false;

                await connectTask;
                return client.Connected;
            } catch {
                return false;
            }
        }
    }
}