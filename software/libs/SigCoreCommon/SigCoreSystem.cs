using Iot.Device.Display;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SigCoreCommon.Payloads;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Reactive.Joins;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using System.Threading.Tasks;



namespace SigCoreCommon {
    public enum ConnectStatus {
        Connected,
        TcpFailed,
        ProtocolRejected,
        Timeout
    }


    /// <summary>
    /// High-level client API for interacting with a SigCore UC device.
    /// 
    /// SigCoreSystem acts as the primary façade over the SigCore messaging
    /// protocol. It manages connection lifecycle, request/response correlation,
    /// fire-and-forget commands, configuration retrieval and updates, and
    /// real-time alert subscriptions.
    /// 
    /// Responsibilities:
    /// • Establish and validate session handshake with the server
    /// • Send control commands (relay, analog out, PID, system, network)
    /// • Perform typed request/response operations with timeout handling
    /// • Expose strongly-typed configuration payloads
    /// • Dispatch server-initiated alerts as .NET events
    /// • Correlate responses using message ID tracking
    /// 
    /// All operations are async-first. Synchronous wrappers are provided
    /// where necessary, but the recommended usage pattern is await-based.
    /// 
    /// This class is intended to be the single integration surface between
    /// application code (UI, services, automation logic) and the SigCore
    /// transport layer.
    /// </summary>
    public class SigCoreSystem : IDispatcher, IDisposable {
        private readonly SigCoreClient _client;
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<JObject>> _pending;
        private TaskCompletionSource<bool> _connectHandshakeTcs;

        // ===============================
        // Event Declarations
        // ===============================

        /// <remarks>
        /// These events are raised in response to server-initiated alerts.
        /// They may be invoked on a non-UI thread. Consumers are responsible
        /// for marshaling to the appropriate synchronization context.
        /// </remarks>

        /// <summary>
        /// Raised when one or more digital input states change.
        /// </summary>
        /// <remarks>
        /// The array contains the current state of all digital input channels.
        /// The index corresponds to the channel number.
        /// </remarks>
        public event Action<bool[]> DigitalInChanged;

        /// <summary>
        /// Raised when one or more analog input values change.
        /// </summary>
        /// <remarks>
        /// The array contains the current value of all analog input channels.
        /// The index corresponds to the channel number.
        /// </remarks>
        public event Action<double[]> AnalogInChanged;

        /// <summary>
        /// Raised when a relay output state changes.
        /// </summary>
        /// <param name="channel">The relay channel number.</param>
        /// <param name="state">The new relay state.</param>
        public event Action<uint, bool> RelayChanged;

        /// <summary>
        /// Raised when an analog output value changes.
        /// </summary>
        /// <param name="channel">The analog output channel number.</param>
        /// <param name="value">The new output value.</param>
        /// <param name="auto">True if the channel is under automatic control; otherwise false.</param>
        public event Action<uint, double, bool> AnalogOutChanged;

        /// <summary>
        /// Raised when a PID setpoint changes.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="value">The new setpoint value.</param>
        public event Action<uint, double> PIDSPChanged;

        /// <summary>
        /// Raised when a PID process variable changes.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="value">The new process variable value.</param>
        public event Action<uint, double> PIDPVChanged;

        /// <summary>
        /// Raised when a PID output value changes.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="value">The new output value.</param>
        public event Action<uint, double> PIDOutputChanged;

        /// <summary>
        /// Raised when a PID tolerance value changes.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="value">The new tolerance value.</param>
        public event Action<uint, double> PIDTolChanged;

        /// <summary>
        /// Raised when a PID deviation value changes.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="value">The new deviation value.</param>
        public event Action<uint, double> PIDDevChanged;

        /// <summary>
        /// Raised when the PID automatic mode state changes.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="auto">True if in automatic mode; otherwise false.</param>
        public event Action<uint, bool> PIDAutoChanged;

        /// <summary>
        /// Raised when the PID ramp time changes.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="value">The new ramp time in seconds.</param>
        public event Action<uint, double> PIDRampTimeChanged;

        /// <summary>
        /// Raised when the PID ramp target changes.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="value">The new ramp target value.</param>
        public event Action<uint, double> PIDRampTargetChanged;

        /// <summary>
        /// Raised when the PID ramp enable state changes.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="enabled">True if ramping is enabled; otherwise false.</param>
        public event Action<uint, bool> PIDRampChanged;

        /// <summary>
        /// Raised when the logger status changes.
        /// </summary>
        /// <param name="status">The current logger status description.</param>
        /// <param name="logging">True if logging is active; otherwise false.</param>
        public event Action<string, bool> LoggerStatusChanged;


        /// <summary>
        /// Gets a value indicating whether the client is currently connected to the SigCore server.
        /// </summary>
        /// <remarks>
        /// Returns true once the underlying transport connection has been established.
        /// This does not necessarily imply that initialization or subscription has completed.
        /// </remarks>
        public bool IsConnected => _client.IsConnected;

        /// <summary>
        /// Gets or sets a value indicating whether this client session has commander privileges.
        /// </summary>
        /// <remarks>
        /// Commander sessions are allowed to issue control and configuration commands.
        /// This value is assigned during the connection handshake and reflects the
        /// role granted by the server.
        /// </remarks>
        public bool IsCommander { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SigCoreSystem"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor initializes the underlying transport client,
        /// prepares the request/response correlation dictionary, and creates
        /// the handshake completion source used during connection negotiation.
        /// 
        /// No network connection is established by this constructor. Call
        /// <see cref="ConnectAsync(string, int)"/> to connect to a SigCore server.
        /// </remarks>
        public SigCoreSystem() {
            _client = new SigCoreClient(this);
            _pending = new ConcurrentDictionary<ulong, TaskCompletionSource<JObject>>();
            _connectHandshakeTcs =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task<ConnectStatus> ConnectAsync(
            string ip,
            int port = 7020,
            int timeoutMs = 5000) {
            bool tcpConnected = await _client.ConnectAsync(ip, port);

            if (!tcpConnected)
                return ConnectStatus.TcpFailed;

            try {
                JObject payload =
                    await RequestAsync(
                        SigCoreMessage.CreateConnect(),
                        timeoutMs);  // Let RequestAsync handle timeout

                bool accepted =
                    payload.Value<bool>("accepted");

                if (!accepted) {
                    _client.Close();
                    return ConnectStatus.ProtocolRejected;
                }

                return ConnectStatus.Connected;
            } catch (TimeoutException) {
                _client.Close();
                return ConnectStatus.Timeout;
            }
        }

        /// <summary>
        /// Closes the current connection to the SigCore server.
        /// </summary>
        /// <remarks>
        /// This immediately terminates the underlying transport connection.
        /// Any pending requests may fail or timeout.
        /// </remarks>
        public void Close() {
            _client.Close();
        }

        /// <summary>
        /// Releases resources used by the <see cref="SigCoreSystem"/>.
        /// </summary>
        /// <remarks>
        /// This method closes the underlying connection. After disposal,
        /// the instance should not be used.
        /// </remarks>
        public void Dispose() {
            _client.Close();
        }

        // ===============================
        // Pattern A — Request / Response
        // ===============================
        private async Task<JObject> RequestAsyncInternal(
            SigCoreMessage msg,
            int timeoutMs) {
            TaskCompletionSource<JObject> tcs =
                new TaskCompletionSource<JObject>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            _pending[msg.MsgId] = tcs;

            try {
                await _client.SendAsync(msg);

                Task delay = Task.Delay(timeoutMs);
                Task completed = await Task.WhenAny(tcs.Task, delay);

                if (completed == delay)
                    throw new TimeoutException();

                return await tcs.Task;
            } finally {
                _pending.TryRemove(msg.MsgId, out _);
            }
        }

        private async Task<T> RequestAsync<T>(
            SigCoreMessage msg,
            Func<JObject, T> parser,
            int timeoutMs = 3000) {
            JObject payload =
                await RequestAsyncInternal(msg, timeoutMs);

            return parser(payload);
        }

        private Task<JObject> RequestAsync(
            SigCoreMessage msg,
            int timeoutMs = 3000) {
            return RequestAsyncInternal(msg, timeoutMs);
        }

        // ===============================
        // Pattern B — Fire and Forget
        // ===============================
        private async Task SendCommandAsync(SigCoreMessage msg) {
            await _client.SendAsync(msg);
        }


        // ===============================
        // Pattern C — Alerts (generic)
        // ===============================
        private void OnAlert<T>(Action<T> handler, T payload) {
            if (handler != null) handler(payload);
        }
        private void OnAlert<T1, T2>(Action<T1, T2> handler, T1 a1, T2 a2) {
            handler?.Invoke(a1, a2);
        }
        private void OnAlert<T1, T2, T3>(Action<T1, T2, T3> handler, T1 a1, T2 a2, T3 a3) {
            handler?.Invoke(a1, a2, a3);
        }




        public async Task SetRelayAsync(uint channel, bool state) {
            SigCoreMessage msg = SigCoreMessage.CreateSetRelay(channel, state);
            await _client.SendAsync(msg);
        }


        public async Task SetAOutValueAsync(uint channel, double value) {
            SigCoreMessage msg = SigCoreMessage.CreateSetAOut(channel, value);
            await _client.SendAsync(msg);
        }


        /// <summary>
        /// Updates the configuration for a relay output channel.
        /// </summary>
        /// <param name="channel">
        /// The relay channel number to configure.
        /// </param>
        /// <param name="config">
        /// The relay configuration payload containing the desired settings.
        /// The <see cref="RelayConfigPayload.Channel"/> property will be
        /// automatically assigned to match the provided <paramref name="channel"/>.
        /// </param>
        /// <returns>
        /// A task that completes once the configuration command has been
        /// transmitted to the server.
        /// </returns>
        /// <remarks>
        /// This operation sends the new relay configuration to the server.
        /// The configuration includes:
        ///
        /// • <see cref="RelayConfigPayload.Name"/> — User-defined relay label  
        /// • <see cref="RelayConfigPayload.DefaultState"/> — State applied at startup  
        /// • <see cref="RelayConfigPayload.FailSafeState"/> — State applied during fault conditions  
        ///
        /// This method does not perform a read-back verification. If confirmation
        /// of the applied configuration is required, call
        /// <c>GetRelayConfigAsync</c> after the update.
        ///
        /// Commander privileges are required to modify relay configuration.
        /// </remarks>
        public async Task SetRelayConfigAsync(uint channel, RelayConfigPayload config) {
            config.Channel = channel;
            SigCoreMessage msg = SigCoreMessage.CreateSetRelayConfig(config);
            await _client.SendAsync(msg);
        }


        /// <summary>
        /// Updates the configuration for a digital input channel.
        /// </summary>
        /// <param name="config">
        /// The digital input configuration payload containing the desired settings.
        /// The <see cref="DInConfigPayload.Channel"/> property determines which
        /// channel will be updated.
        /// </param>
        /// <returns>
        /// A task that completes once the configuration command has been
        /// transmitted to the server.
        /// </returns>
        /// <remarks>
        /// This operation sends the updated digital input configuration to the server.
        /// The configuration includes:
        ///
        /// • <see cref="DInConfigPayload.Name"/> — User-defined channel label  
        /// • <see cref="DInConfigPayload.DebounceMs"/> — Software debounce time in milliseconds  
        /// • <see cref="DInConfigPayload.Inverted"/> — Logical inversion of the input state  
        ///
        /// Debounce filtering is applied on the device to suppress rapid transitions
        /// caused by mechanical switch bounce or electrical noise.
        ///
        /// This method does not automatically perform a read-back verification.
        /// If confirmation of the applied configuration is required, call
        /// <c>GetDInConfigAsync</c> after the update.
        ///
        /// Commander privileges are required to modify digital input configuration.
        /// </remarks>
        public async Task SetDInConfigAsync(DInConfigPayload config) {
            SigCoreMessage msg = SigCoreMessage.CreateSetDInConfig(config);
            await _client.SendAsync(msg);
        }


        /// <summary>
        /// Updates the configuration for an analog input channel.
        /// </summary>
        /// <param name="config">
        /// The analog input configuration payload containing the desired settings.
        /// The <see cref="AInConfigPayload.Channel"/> property determines which
        /// channel will be updated.
        /// </param>
        /// <returns>
        /// A task that completes once the configuration command has been
        /// transmitted to the server.
        /// </returns>
        /// <remarks>
        /// This operation sends the updated analog input configuration to the server.
        ///
        /// Core settings:
        /// • <see cref="AInConfigPayload.Name"/> — User-defined channel label  
        /// • <see cref="AInConfigPayload.Units"/> — Engineering units for display  
        /// • <see cref="AInConfigPayload.AveragingSamples"/> — Sample averaging count  
        /// • <see cref="AInConfigPayload.Precision"/> — Display precision  
        ///
        /// Range and formatting:
        /// • <see cref="AInConfigPayload.InputRange"/> — Hardware input range selection  
        /// • <see cref="AInConfigPayload.DisplayFormat"/> — Display format mode  
        ///
        /// Calibration:
        /// • <see cref="AInConfigPayload.CalibrationType"/>  
        /// • <see cref="AInConfigPayload.CalibrationM"/>  
        /// • <see cref="AInConfigPayload.CalibrationB"/>  
        ///
        /// This method does not automatically perform a read-back verification.
        /// If confirmation of the applied configuration is required, call
        /// <c>GetAInConfigAsync</c> after the update.
        ///
        /// Commander privileges are required to modify analog input configuration.
        /// </remarks>
        public async Task SetAInConfigAsync(AInConfigPayload config) {
            SigCoreMessage msg = SigCoreMessage.CreateSetAInConfig(config);
            await _client.SendAsync(msg);
        }

        /// <summary>
        /// Updates the configuration for an analog output channel.
        /// </summary>
        /// <param name="config">
        /// The analog output configuration payload containing the desired settings.
        /// The <see cref="AOutConfigPayload.Channel"/> property determines which
        /// channel will be updated.
        /// </param>
        /// <returns>
        /// A task that completes once the configuration command has been
        /// transmitted to the server.
        /// </returns>
        /// <remarks>
        /// This operation sends the updated analog output configuration to the server.
        ///
        /// Core settings:
        /// • <see cref="AOutConfigPayload.Name"/> — User-defined channel label  
        /// • <see cref="AOutConfigPayload.Units"/> — Engineering units for display  
        /// • <see cref="AOutConfigPayload.IsAuto"/> — Enables automatic (closed-loop) control mode  
        /// • <see cref="AOutConfigPayload.Precision"/> — Display precision  
        ///
        /// Mode and formatting:
        /// • <see cref="AOutConfigPayload.Mode"/> — Output operating mode (for example voltage)  
        /// • <see cref="AOutConfigPayload.Display"/> — Display format mode  
        ///
        /// Voltage scaling:
        /// • <see cref="AOutConfigPayload.VoltageScaleM"/>  
        /// • <see cref="AOutConfigPayload.VoltageScaleB"/>  
        ///
        /// When operating in voltage mode, the device applies linear scaling
        /// (mX + B) to convert engineering units to the physical output value.
        ///
        /// This method does not automatically perform a read-back verification.
        /// If confirmation of the applied configuration is required, call
        /// <c>GetAOutConfigAsync</c> after the update.
        ///
        /// Commander privileges are required to modify analog output configuration.
        /// </remarks>
        public async Task SetAOutConfigAsync(AOutConfigPayload config) {
            SigCoreMessage msg = SigCoreMessage.CreateSetAOutConfig(config.ToJson());
            await _client.SendAsync(msg);
        }

        /// <summary>
        /// Sets the PID setpoint for the specified channel.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="sp">The desired setpoint value in engineering units.</param>
        /// <remarks>
        /// Updates the target value the PID controller attempts to maintain.
        /// Commander privileges are required.
        /// </remarks>
        public async Task SetPIDSPAsync(uint channel, double sp) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDSP(channel, sp);
            await _client.SendAsync(msg);
        }

        /// <summary>
        /// Sets the PID manual output value for the specified channel.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="output">The desired output value.</param>
        /// <remarks>
        /// This value is applied when the controller is operating in manual mode.
        /// Commander privileges are required.
        /// </remarks>
        public async Task SetPIDOutputAsync(uint channel, double output) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDOutput(channel, output);
            await _client.SendAsync(msg);
        }

        /// <summary>
        /// Sets the PID tolerance value for the specified channel.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="tol">The allowable deviation from setpoint.</param>
        /// <remarks>
        /// Tolerance defines the acceptable error band around the setpoint.
        /// Commander privileges are required.
        /// </remarks>
        public async Task SetPIDTolAsync(uint channel, double tol) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDTol(channel, tol);
            await _client.SendAsync(msg);
        }

        /// <summary>
        /// Enables or disables automatic PID control mode.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="auto">
        /// True to enable automatic control; false to enable manual mode.
        /// </param>
        /// <remarks>
        /// When automatic mode is disabled, the controller output must be set
        /// manually using <see cref="SetPIDOutputAsync(uint, double)"/>.
        /// Commander privileges are required.
        /// </remarks>
        public async Task SetPIDAutoAsync(uint channel, bool auto) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDAuto(channel, auto);
            await _client.SendAsync(msg);
        }

        /// <summary>
        /// Sets the PID ramp time in seconds.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="rampTime">Ramp duration in seconds.</param>
        /// <remarks>
        /// Ramp time defines how quickly the controller transitions toward
        /// the ramp target when ramping is enabled.
        /// Commander privileges are required.
        /// </remarks>
        public async Task SetPIDRampTimeAsync(uint channel, double rampTime) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDRampTime(channel, rampTime);
            await _client.SendAsync(msg);
        }

        /// <summary>
        /// Sets the PID ramp target value.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="rampTarget">The target value for ramping.</param>
        /// <remarks>
        /// When ramping is enabled, the controller will transition toward this
        /// target over the configured ramp time.
        /// Commander privileges are required.
        /// </remarks>
        public async Task SetPIDRampTargetAsync(uint channel, double rampTarget) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDRampTarget(channel, rampTarget);
            await _client.SendAsync(msg);
        }

        /// <summary>
        /// Enables or disables PID ramping behavior.
        /// </summary>
        /// <param name="channel">The PID channel number.</param>
        /// <param name="ramp">True to enable ramping; otherwise false.</param>
        /// <remarks>
        /// When enabled, the setpoint transitions gradually toward the ramp target
        /// according to the configured ramp time.
        /// Commander privileges are required.
        /// </remarks>
        public async Task SetPIDRampAsync(uint channel, bool ramp) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDRamp(channel, ramp);
            await _client.SendAsync(msg);
        }

        /// <summary>
        /// Resets the PID controller error state for the specified channel.
        /// </summary>
        /// <param name="channel">
        /// The PID channel number.
        /// </param>
        /// <remarks>
        /// This operation clears the accumulated controller error, including
        /// integral accumulation and deviation tracking, allowing the loop to
        /// resume from a neutral state.
        ///
        /// This is typically used after large disturbances, configuration changes,
        /// or when transitioning between operating modes to prevent windup effects.
        ///
        /// Commander privileges are required.
        /// </remarks>
        public async Task ResetPIDAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateResetPID(channel);
            await _client.SendAsync(msg);
        }


        /// <summary>
        /// Updates the configuration parameters for a PID controller channel.
        /// </summary>
        /// <param name="config">
        /// The PID configuration payload containing controller settings.
        /// The <see cref="PidConfigPayload.Channel"/> property determines which
        /// PID channel will be updated.
        /// </param>
        /// <returns>
        /// A task that completes once the configuration command has been
        /// transmitted to the server.
        /// </returns>
        /// <remarks>
        /// This operation updates the full PID controller configuration on the device.
        ///
        /// Identity and mode:
        /// • <see cref="PidConfigPayload.Title"/> — User-defined loop name  
        /// • <see cref="PidConfigPayload.IsEnabled"/> — Enables or disables the loop  
        /// • <see cref="PidConfigPayload.AutoMode"/> — Sets initial automatic/manual mode  
        ///
        /// PID gains:
        /// • <see cref="PidConfigPayload.Kp"/> — Proportional gain  
        /// • <see cref="PidConfigPayload.Ki"/> — Integral gain  
        /// • <see cref="PidConfigPayload.Kd"/> — Derivative gain  
        ///
        /// Limits:
        /// • <see cref="PidConfigPayload.OutputMin"/> / <see cref="PidConfigPayload.OutputMax"/> — Output clamp range  
        /// • <see cref="PidConfigPayload.PVMin"/> / <see cref="PidConfigPayload.PVMax"/> — Process variable scaling limits  
        ///
        /// Routing:
        /// • <see cref="PidConfigPayload.PvSource"/> — Source channel for process variable  
        /// • <see cref="PidConfigPayload.OutputDestination"/> — Destination channel for controller output  
        ///
        /// Integral behavior:
        /// • <see cref="PidConfigPayload.IntegralLimit"/> — Maximum accumulated integral value  
        /// • <see cref="PidConfigPayload.ResetOnModeChange"/> — Clears accumulated error when switching modes  
        ///
        /// This method does not automatically perform a read-back verification.
        /// If confirmation of the applied configuration is required, call
        /// <c>GetPIDConfigAsync</c> after the update.
        ///
        /// Commander privileges are required to modify PID configuration.
        /// </remarks>
        public async Task SetPIDConfigAsync(PidConfigPayload config) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDConfig(config);
            await _client.SendAsync(msg);
        }


        /// <summary>
        /// Updates the global system configuration for the device.
        /// </summary>
        /// <param name="config">
        /// The system configuration payload containing device-wide settings.
        /// </param>
        /// <returns>
        /// A task that completes once the configuration command has been
        /// transmitted to the server.
        /// </returns>
        /// <remarks>
        /// This operation updates global device configuration parameters.
        ///
        /// System identity:
        /// • <see cref="SystemConfigPayload.SystemName"/>  
        /// • <see cref="SystemConfigPayload.SystemSerialNumber"/>  
        /// • <see cref="SystemConfigPayload.HardwareVersion"/>  
        /// • <see cref="SystemConfigPayload.SoftwareVersion"/>  
        ///
        /// Startup behavior:
        /// • <see cref="SystemConfigPayload.UseLastKnownValuesAtStartup"/> — 
        ///   Restores persisted output and control states during boot  
        ///
        /// Timing and control:
        /// • <see cref="SystemConfigPayload.AnalogSamplingRate"/> — 
        ///   Global analog input sampling rate  
        /// • <see cref="SystemConfigPayload.PWMFrequency"/> — 
        ///   Default PWM output frequency  
        /// • <see cref="SystemConfigPayload.IsPIDOpen"/> — 
        ///   Enables or disables PID subsystem availability  
        ///
        /// Security:
        /// • <see cref="SystemConfigPayload.SystemPassword"/> — 
        ///   Device authentication password  
        ///
        /// Some settings may require a device restart to fully apply.
        /// Persistent fields intended for non-volatile storage are serialized
        /// through <see cref="SystemConfigPayload.ToFramPayload"/> on the device.
        ///
        /// This method does not automatically perform a read-back verification.
        /// If confirmation of the applied configuration is required, call
        /// <c>GetGlobalConfigAsync</c> after the update.
        ///
        /// Commander privileges are required to modify global configuration.
        /// </remarks>
        public async Task SetGlobalConfigAsync(SystemConfigPayload config) {
            SigCoreMessage msg = SigCoreMessage.CreateSetGlobalConfig(config.ToJson());
            await _client.SendAsync(msg);
        }


        /// <summary>
        /// Updates the network configuration for the device.
        /// </summary>
        /// <param name="payload">
        /// The network settings payload containing hostname, Ethernet,
        /// and Wi-Fi configuration parameters.
        /// </param>
        /// <returns>
        /// A task that completes once the configuration command has been
        /// transmitted to the server.
        /// </returns>
        /// <remarks>
        /// This operation updates device networking parameters.
        ///
        /// System:
        /// • <see cref="NetworkSettingsPayload.Hostname"/> — Device hostname  
        ///
        /// Ethernet:
        /// • <see cref="NetworkSettingsPayload.EthDhcpEnabled"/> — Enables DHCP  
        /// • <see cref="NetworkSettingsPayload.EthIpAddress"/>  
        /// • <see cref="NetworkSettingsPayload.EthSubnetMask"/>  
        /// • <see cref="NetworkSettingsPayload.EthGateway"/>  
        /// • <see cref="NetworkSettingsPayload.EthDns"/>  
        ///
        /// Wi-Fi:
        /// • <see cref="NetworkSettingsPayload.WifiSsid"/> — Wireless network SSID  
        /// • <see cref="NetworkSettingsPayload.WifiPsk"/> — Pre-shared key  
        /// • <see cref="NetworkSettingsPayload.WifiDhcpEnabled"/> — Enables DHCP  
        /// • <see cref="NetworkSettingsPayload.WifiIpAddress"/>  
        /// • <see cref="NetworkSettingsPayload.WifiSubnetMask"/>  
        /// • <see cref="NetworkSettingsPayload.WifiGateway"/>  
        /// • <see cref="NetworkSettingsPayload.WifiDns"/>  
        ///
        /// When DHCP is enabled for an interface, static IP fields are ignored.
        /// Applying network changes may temporarily interrupt the current
        /// connection session. In some cases, a restart may be required for
        /// changes to fully take effect.
        ///
        /// This method does not automatically perform a read-back verification.
        /// If confirmation of the applied configuration is required, call
        /// <c>GetNetworkConfigAsync</c> after the update.
        ///
        /// Commander privileges are required to modify network configuration.
        /// </remarks>
        public async Task SetNetworkConfigAsync(NetworkSettingsPayload payload) {
            SigCoreMessage msg = SigCoreMessage.CreateSetNetworkConfig(payload.ToJson());
            await _client.SendAsync(msg);
        }


        /// <summary>
        /// Subscribes to real-time device state updates.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous subscribe request.
        /// </returns>
        /// <remarks>
        /// This method registers the client to receive real-time state updates
        /// from the device. After subscription, the server will:
        ///
        /// • Send an initial full-state snapshot (all inputs, outputs, PID states, etc.)
        /// • Push subsequent state changes as delta-based alerts
        ///
        /// This method follows the standard fire-and-forget command pattern.
        /// It only confirms that the subscribe message was successfully sent
        /// to the transport layer.
        ///
        /// It does not wait for or validate receipt of the initial snapshot.
        /// Consumers should rely on alert events such as:
        ///
        /// • <c>AnalogInChanged</c>
        /// • <c>DigitalInChanged</c>
        /// • <c>RelayChanged</c>
        /// • <c>AnalogOutChanged</c>
        /// • <c>PIDCurValChanged</c>
        /// • <c>LoggerStatusChanged</c>
        ///
        /// Subscribe should typically be called once after a successful
        /// <see cref="ConnectAsync(string, int)"/>.
        ///
        /// Commander privileges are not required.
        /// </remarks>
        public Task SubscribeAsync() {
            SigCoreMessage msg = SigCoreMessage.CreateSubscribe();
            return _client.SendAsync(msg);
        }

        // ===============================
        // Query / Get Operations
        // ===============================

        /// <summary>
        /// Retrieves the configuration for an analog input channel.
        /// </summary>
        /// <param name="channel">
        /// The analog input channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to an <see cref="AInConfigPayload"/> containing
        /// the current configuration of the specified channel.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the current configuration for the specified channel.
        ///
        /// The returned payload includes core channel properties, range selection,
        /// display formatting, and calibration parameters.
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read configuration.
        /// </remarks>
        public Task<AInConfigPayload> GetAInConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetAInConfig(channel);
            return RequestAsync(msg, payload =>
            {
                AInConfigPayload cfg = new AInConfigPayload(payload);
                return cfg;
            });
        }


        /// <summary>
        /// Retrieves the current value of an analog input channel.
        /// </summary>
        /// <param name="channel">
        /// The analog input channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to the current analog input value in engineering units.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the latest sampled value for the specified channel.
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read input values.
        /// </remarks>
        public Task<double> GetAnalogInputAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetAnalogIn(channel);
            return RequestAsync(msg, payload => payload.Value<double>("value"));
        }


        // ==========================================
        // DIGITAL INPUT
        // ==========================================

        /// <summary>
        /// Retrieves the configuration for a digital input channel.
        /// </summary>
        /// <param name="channel">
        /// The digital input channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to a <see cref="DInConfigPayload"/> containing
        /// the current configuration of the specified channel.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the active configuration for the specified digital input.
        ///
        /// The returned payload includes:
        /// • Channel identity  
        /// • User-defined name  
        /// • Debounce timing (milliseconds)  
        /// • Logical inversion state  
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read configuration.
        /// </remarks>
        public Task<DInConfigPayload> GetDInConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetDInConfig(channel);
            return RequestAsync(msg, payload =>
            {
                DInConfigPayload cfg = new DInConfigPayload(payload);
                return cfg;
            });
        }


        /// <summary>
        /// Retrieves the current logical state of a digital input channel.
        /// </summary>
        /// <param name="channel">
        /// The digital input channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to the current state of the specified input.
        /// True indicates an active (high) state; false indicates inactive (low).
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the current debounced state of the specified input.
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read input values.
        /// </remarks>
        public Task<bool> GetDInAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetDIn(channel);
            return RequestAsync(msg, payload => payload.Value<bool>("state"));
        }



        /// <summary>
        /// Retrieves the configuration for a relay output channel.
        /// </summary>
        /// <param name="channel">
        /// The relay channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to a <see cref="RelayConfigPayload"/> containing
        /// the current configuration of the specified relay.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the active relay configuration.
        ///
        /// The returned payload includes:
        /// • Channel identity  
        /// • User-defined relay name  
        /// • Default startup state  
        /// • Fail-safe state applied during fault conditions  
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read configuration.
        /// </remarks>
        public Task<RelayConfigPayload> GetRelayConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetRelayConfig(channel);
            return RequestAsync(msg, payload =>
            {
                return new RelayConfigPayload(payload);
            });
        }


        /// <summary>
        /// Retrieves the current state of a relay output channel.
        /// </summary>
        /// <param name="channel">
        /// The relay channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to the current relay state.
        /// True indicates the relay is energized; false indicates it is de-energized.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the current relay state.
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read relay state.
        /// </remarks>
        public Task<bool> GetRelayAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetRelay(channel);
            return RequestAsync(msg, payload => payload.Value<bool>("state"));
        }


        /// <summary>
        /// Retrieves the configuration for an analog output channel.
        /// </summary>
        /// <param name="channel">
        /// The analog output channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to an <see cref="AOutConfigPayload"/> containing
        /// the current configuration of the specified channel.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the active analog output configuration.
        ///
        /// The returned payload includes:
        /// • Channel identity  
        /// • User-defined name and engineering units  
        /// • Output mode and display format  
        /// • Automatic/manual state  
        /// • Output scaling parameters (mX + B when in voltage mode)  
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read configuration.
        /// </remarks>
        public Task<AOutConfigPayload> GetAOutConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetAOutConfig(channel);
            return RequestAsync(msg, payload =>
            {
                AOutConfigPayload cfg = new AOutConfigPayload(payload);
                return cfg;
            });
        }

        /// <summary>
        /// Retrieves the current value of an analog output channel.
        /// </summary>
        /// <param name="channel">
        /// The analog output channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to the current output value in engineering units.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the current output value of the specified channel.
        ///
        /// The returned value reflects the device’s active output state, whether
        /// under manual control or automatic (PID) control.
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read output values.
        /// </remarks>
        public Task<double> GetAOutValueAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetAOut(channel);
            return RequestAsync(msg, payload => payload.Value<double>("value"));
        }


        /// <summary>
        /// Retrieves the configuration for a PID controller channel.
        /// </summary>
        /// <param name="channel">
        /// The PID channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to a <see cref="PidConfigPayload"/> containing
        /// the current configuration of the specified PID loop.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the active PID configuration.
        ///
        /// The returned payload includes:
        /// • Loop identity and enable state  
        /// • Automatic/manual mode setting  
        /// • Proportional, integral, and derivative gains  
        /// • Output and process variable limits  
        /// • Input/output routing  
        /// • Integral behavior and reset configuration  
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read configuration.
        /// </remarks>
        public Task<PidConfigPayload> GetPIDConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetPIDConfig(channel);
            return RequestAsync(msg, payload =>
            {
                PidConfigPayload cfg = new PidConfigPayload(payload);
                return cfg;
            });
        }


        /// <summary>
        /// Retrieves the current runtime values of a PID controller channel.
        /// </summary>
        /// <param name="channel">
        /// The PID channel number.
        /// </param>
        /// <returns>
        /// A task that resolves to a tuple containing:
        /// (<c>setPoint</c>, <c>processVariable</c>, <c>output</c>,
        /// <c>tolerance</c>, <c>autoMode</c>, <c>deviation</c>).
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and returns a
        /// snapshot of the PID loop’s live operating state:
        ///
        /// • <c>setPoint</c> — Current target value  
        /// • <c>processVariable</c> — Measured input value  
        /// • <c>output</c> — Controller output value  
        /// • <c>tolerance</c> — Configured acceptable error band  
        /// • <c>autoMode</c> — Indicates automatic (true) or manual (false) control  
        /// • <c>deviation</c> — Current error (setPoint − processVariable)  
        ///
        /// This method provides a deterministic snapshot and is useful for
        /// diagnostics, polling clients, or validation logic.
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read current PID values.
        /// </remarks>
        public Task<(double setPoint, double processVariable, double output, double tolerance, bool autoMode, double deviation)>
            GetPIDCurValAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetPIDCurVal(channel);
            return RequestAsync(msg, payload => (
                payload.Value<double>("setPoint"),
                payload.Value<double>("processVariable"),
                payload.Value<double>("output"),
                payload.Value<double>("tolerance"),
                payload.Value<bool>("autoMode"),
                payload.Value<double>("deviation")
            ));
        }


        /// <summary>
        /// Retrieves the current logging configuration for the device.
        /// </summary>
        /// <returns>
        /// A task that resolves to a <see cref="LoggerConfigPayload"/> containing
        /// the active logging settings.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the current logger configuration.
        ///
        /// The returned payload includes logger enable state, timing parameters,
        /// destination settings, and status information as defined by
        /// <see cref="LoggerConfigPayload"/>.
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read logging configuration.
        /// </remarks>
        public Task<LoggerConfigPayload> GetLoggingConfigAsync() {
            SigCoreMessage msg = SigCoreMessage.CreateGetLoggingConfig();
            return RequestAsync(msg, payload => {
                LoggerConfigPayload cfg = new LoggerConfigPayload(payload);
                return cfg;
            });
        }

        /// <summary>
        /// Updates the device logging configuration.
        /// </summary>
        /// <param name="config">
        /// A <see cref="LoggerConfigPayload"/> containing the desired logging
        /// settings, including enable state, timing, connection parameters,
        /// schema information, and timeout values.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous send operation.
        /// </returns>
        /// <remarks>
        /// This method transmits updated logger settings to the device.
        ///
        /// Core configuration includes:
        /// • Enable/disable state  
        /// • Logging interval (seconds)  
        ///
        /// Database connection parameters include:
        /// • Server address and port  
        /// • Database name  
        /// • Authentication credentials  
        ///
        /// Schema settings define:
        /// • Target table name  
        /// • Schema version  
        ///
        /// Timeout settings control:
        /// • Connection timeout  
        /// • Command execution timeout  
        ///
        /// This operation sends the configuration command but does not
        /// return the applied configuration. To verify the active settings,
        /// call <c>GetLoggingConfigAsync</c> after completion.
        ///
        /// Commander privileges are required to modify logging configuration.
        /// </remarks>
        public Task SetLoggingConfigAsync(LoggerConfigPayload config) {
            SigCoreMessage msg = SigCoreMessage.CreateSetLoggingConfig(config.ToJson());
            return _client.SendAsync(msg);
        }
        /// <summary>
        /// Retrieves the current global system configuration from the device.
        /// </summary>
        /// <returns>
        /// A task that resolves to a <see cref="SystemConfigPayload"/> containing
        /// the active device-wide configuration.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// server to return the current global configuration.
        ///
        /// The returned payload includes:
        /// • System identity (name, serial number, hardware/software version)  
        /// • Startup behavior settings  
        /// • Analog sampling rate and PWM configuration  
        /// • PID subsystem availability  
        /// • Security settings  
        ///
        /// If the server does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read global configuration.
        /// </remarks>
        public Task<SystemConfigPayload> GetGlobalConfigAsync() {
            SigCoreMessage msg = SigCoreMessage.CreateGetGlobalConfig();

            return RequestAsync(msg, payloadJson => {
                SystemConfigPayload payload =
                    new SystemConfigPayload(payloadJson);
                return payload;
            });
        }

        /// <summary>
        /// Retrieves the current network configuration of the device.
        /// </summary>
        /// <returns>
        /// A task that resolves to a <see cref="NetworkSettingsPayload"/>
        /// containing hostname, Ethernet, and Wi-Fi settings.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and waits for the
        /// device to return its active network configuration.
        ///
        /// The returned payload includes:
        /// • Hostname  
        /// • Ethernet DHCP/static configuration  
        /// • Wi-Fi SSID, security, and DHCP/static configuration  
        ///
        /// A longer timeout is used for this request because network queries
        /// may involve operating system–level interactions.
        ///
        /// If the device does not respond within the timeout window,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to read network configuration.
        /// </remarks>
        public Task<NetworkSettingsPayload> GetNetworkConfigAsync() {
            SigCoreMessage msg = SigCoreMessage.CreateGetNetworkConfig();

            return RequestAsync(
                msg,
                payloadJson => new NetworkSettingsPayload(payloadJson),
                timeoutMs: 10000
            );
        }




        /// <summary>
        /// Performs a connectivity health check with the device using a ping/pong exchange.
        /// </summary>
        /// <param name="timeoutMs">
        /// The maximum time, in milliseconds, to wait for a pong response.
        /// Default is 1000 ms.
        /// </param>
        /// <returns>
        /// A task that resolves to:
        /// • <c>true</c> if the device responds within the specified timeout.
        /// • <c>false</c> if the device does not respond within the timeout period.
        /// </returns>
        /// <remarks>
        /// This method sends a ping message to the device and waits for the
        /// corresponding pong response.
        ///
        /// Behavior:
        /// • Returns <c>true</c> if a pong response is received before timeout.
        /// • Returns <c>false</c> if the timeout period expires.
        /// • Propagates transport-level exceptions (for example, connection loss).
        ///
        /// This method is useful for:
        /// • Connection health checks
        /// • UI connection validation
        /// • Periodic heartbeat monitoring
        ///
        /// Unlike other request/response operations, this method does not
        /// throw <see cref="TimeoutException"/> for non-response. A timeout
        /// is interpreted as a failed health check and results in <c>false</c>.
        ///
        /// Commander privileges are not required.
        /// </remarks>
        public async Task<bool> PingAsync(int timeoutMs = 1000) {
            SigCoreMessage msg = SigCoreMessage.CreatePing();

            try {
                await RequestAsync(msg, timeoutMs);
                return true;            // Pong received
            } catch (TimeoutException) {
                return false;           // No response in time
            }
        }

        /// <summary>
        /// Initiates a graceful shutdown of the device.
        /// </summary>
        /// <remarks>
        /// Sends a shutdown command to the device. Once accepted, the device
        /// will terminate its services and power down according to its platform
        /// configuration.
        ///
        /// This is a fire-and-forget operation and does not wait for confirmation.
        /// The current session will be disconnected as part of the shutdown process.
        ///
        /// Commander privileges are required.
        /// </remarks>
        public async Task Shutdown() {
            SigCoreMessage msg = SigCoreMessage.CreateShutdown();
            await SendCommandAsync(msg);
        }

        /// <summary>
        /// Initiates a restart of the device.
        /// </summary>
        /// <remarks>
        /// Sends a restart command to the device. Once accepted, the device
        /// will reboot and reinitialize all subsystems.
        ///
        /// This is a fire-and-forget operation and does not wait for confirmation.
        /// The current session will be disconnected during the restart process.
        ///
        /// Commander privileges are required.
        /// </remarks>
        public async Task RestartAsync() {
            SigCoreMessage msg = SigCoreMessage.CreateRestart();
            await SendCommandAsync(msg);
        }

        /// <summary>
        /// Retrieves the current runtime status information from the device.
        /// </summary>
        /// <returns>
        /// A task that resolves to a <see cref="JObject"/> containing
        /// device status information.
        /// </returns>
        /// <remarks>
        /// This method performs a request/response operation and returns
        /// structured status data from the device. The returned JSON may include
        /// system health, subsystem state, version information, or other
        /// runtime diagnostics.
        ///
        /// If the device does not respond within the configured timeout,
        /// a <see cref="TimeoutException"/> is thrown.
        ///
        /// Commander privileges are not required to retrieve status information.
        /// </remarks>
        public async Task<JObject> GetStatusAsync() {
            SigCoreMessage msg = SigCoreMessage.CreateGetStatus();
            return await RequestAsync(msg);
        }

        /// <summary>
        /// Performs a factory reset of the device.
        /// </summary>
        /// <param name="serialNo">Device serial number to apply.</param>
        /// <param name="rev">Hardware revision identifier.</param>
        /// <param name="ver">Software version identifier.</param>
        /// <param name="host">Hostname to assign after reset.</param>
        /// <param name="sysName">System display name.</param>
        /// <param name="manifestURL">Update manifest URL.</param>
        /// <param name="sigUrl">Manifest signature URL.</param>
        /// <param name="url">Primary update package URL.</param>
        /// <param name="signature">Cryptographic signature for validation.</param>
        /// <remarks>
        /// This operation resets the device to factory configuration and
        /// reinitializes system identity, networking, and update metadata.
        ///
        /// The provided parameters are used to seed initial configuration,
        /// update endpoints, and identity values during the reset process.
        ///
        /// This is a fire-and-forget operation. The device may restart or
        /// disconnect during the reset process.
        ///
        /// Commander privileges are required.
        /// </remarks>
        public async Task FactoryReset(
            string serialNo,
            string rev,
            string ver,
            string host,
            string sysName,
            string manifestURL,
            string sigUrl,
            string url,
            string signature) {

            SigCoreMessage msg = SigCoreMessage.CreateFactoryReset(
                serialNo, rev, ver, host, sysName,
                manifestURL, sigUrl, url, signature);

            await SendCommandAsync(msg);
        }



        // ===============================
        // Chunk 5 — Event Handling / Alerts
        // ===============================

        public SigCoreMessage HandleDInChangeAlert(JObject payload, ulong msgId, ISession session) {
            bool[] values = ((JArray)payload["values"]).ToObject<bool[]>();
            OnAlert(DigitalInChanged, values);
            return null;
        }

        public SigCoreMessage HandleAInChangeAlert(JObject payload, ulong msgId, ISession session) {
            double[] values = ((JArray)payload["values"]).ToObject<double[]>();
            OnAlert(AnalogInChanged, values);
            return null;
        }

        public SigCoreMessage HandleRelayChangeAlert(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            bool state = payload.Value<bool>("value");
            OnAlert(RelayChanged, channel, state);
            return null;
        }

        public SigCoreMessage HandleAOutChangeAlert(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            double value = payload.Value<double>("value");
            bool auto = payload.Value<bool>("auto");
            OnAlert(AnalogOutChanged, channel, value, auto);
            return null;
        }

        public SigCoreMessage HandleLoggingStatus(JObject payload, ulong msgId, ISession session) {
            string status = payload.Value<string>("status");
            bool logging = payload.Value<bool>("logging");
            OnAlert(LoggerStatusChanged, status, logging);
            return null;
        }

        public SigCoreMessage HandlePIDCurValChangeAlert(JObject payload, ulong msgId, ISession session) {
            uint channel = 0;
            JToken channelToken = payload["channel"];
            if (channelToken != null)
                channel = channelToken.Value<uint>();

            JObject values = payload["values"] as JObject;
            if (values == null)
                return null;

            foreach (JProperty property in values.Properties()) {
                string key = property.Name.ToLowerInvariant();
                JToken val = property.Value;

                switch (key) {
                    case "sp":
                        PIDSPChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "pv":
                        PIDPVChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "out":
                        PIDOutputChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "dev":
                        PIDDevChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "tol":
                        PIDTolChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "auto":
                        PIDAutoChanged?.Invoke(channel, val.Value<bool>());
                        break;

                    // === New ramp handlers ===
                    case "ramptime":
                        PIDRampTimeChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "ramptarget":
                        PIDRampTargetChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "ramp":
                        PIDRampChanged?.Invoke(channel, val.Value<bool>());
                        break;
                }
            }

            return null;
        }

        public SigCoreMessage HandlePing(JObject payload, ulong msgId, ISession session) {
            Console.WriteLine("Pong");
            return SigCoreMessage.CreatePong(msgId);
        }


        public SigCoreMessage TrySetResult(JObject payload, ulong msgId) {
            if (_pending.TryRemove(msgId, out TaskCompletionSource<JObject> tcs)) {
                tcs.TrySetResult(payload);
            }
            return null;
        }


        public SigCoreMessage HandlePong(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleRelay(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleAOut(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleAnalogInValue(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleDIn(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleAInConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleAOutConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleGlobalConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleRelayConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleDInConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandlePIDCurVal(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandlePIDConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleFRAM(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleError(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleLoggingConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleStatus(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleNetworkConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleConnectResult(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);

        //
        //
        //

        public SigCoreMessage HandleConnect(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetRelay(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetAnalogIn(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetAInConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetDIn(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetAOut(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetAOut(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandlePWM(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetAInConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetGlobalConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetGlobalConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetRelayConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetRelayConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetDInConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetDInConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetAOutConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetAOutConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetPIDConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetPIDConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetPIDCurVal(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetPIDCurVal(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetRelay(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSubscribe(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetFRAM(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleResetPID(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetLoggingStatus(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetLoggingConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetLoggingConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleRestart(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleShutdown(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetStatus(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleFactoryReset(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetNetworkConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetNetworkConfig(JObject payload, ulong msgId, ISession session) => null;


        public Task NotifyDInChanged(bool[] vals) => Task.CompletedTask;
        public Task NotifyAInChanged(double[] vals) => Task.CompletedTask;
        public Task NotifyRelayChange(uint channel, bool val) => Task.CompletedTask;
        public Task NotifyAOutChanged(uint channel, double val, bool auto) => Task.CompletedTask;
        public Task NotifyPIDChanged(uint channel, JObject vals) => Task.CompletedTask;
        public Task NotifyLoggerChanged(string status, bool logging) => Task.CompletedTask;

    }
}
