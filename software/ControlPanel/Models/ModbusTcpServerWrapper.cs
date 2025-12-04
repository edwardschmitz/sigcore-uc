using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Timers;
//using EasyModbus;
//using Newtonsoft.Json.Linq;

namespace ControlPanelLib.Models {
    //public class ModbusTcpServerWrapper {
    //    private readonly ModbusServer _server;
    //    private List<D88A42ModbusAdapter> _adapters;
    //    private readonly Timer _syncTimer;

    //    private const int MaxRegisters = 3200;
    //    private readonly ushort[] _lastKnown;

    //    public ModbusTcpServerWrapper(int port = 1502) {
    //        _adapters = new List<D88A42ModbusAdapter>();

    //        _server = new ModbusServer {
    //            Port = port
    //        };

    //        _syncTimer = new Timer(100);
    //        _syncTimer.Elapsed += SyncLoop;

    //        //ushort offset = 0;
    //        //foreach (ControlPanelManager cp in controlPanels) {
    //        //    D88A42ModbusAdapter adapter = new D88A42ModbusAdapter(cp, this, offset);
    //        //    _adapters.Add(adapter);
    //        //    offset += 100;
    //        //}

    //        _lastKnown = new ushort[MaxRegisters];

    //        //float testFloat = 1;
    //        //ushort high, low;
    //        //EncodeFloat(testFloat, out high, out low);
    //        //testFloat = DecodeFloat(high, low);
    //        //Console.WriteLine($"Test encoding  {testFloat} = {high}, {low}");

    //    }

    //    public void Start() {
    //        _server.Listen();
    //        _syncTimer.Start();
    //    }

    //    public void Stop() {
    //        _syncTimer.Stop();
    //        _server.StopListening();
    //    }

    //    public void UpdateReg(ushort reg, ushort val) {
    //        //if (_lastKnown[reg] != val) {
    //        //    _lastKnown[reg] = val;
    //            _server.holdingRegisters[reg] = (short)val;
    //        //}
    //    }
    //    public void UpdateReg(ushort reg, float val) {
    //        ushort high, low;
    //        EncodeFloat(val, out high, out low);
    //        UpdateReg(reg, high);
    //        UpdateReg((ushort)(reg+1), low);
    //    }
    //    public ushort GetReg(ushort reg) {
    //        return (ushort)_server.holdingRegisters[reg];
    //    }
    //    public float GetRegFloat(ushort reg) {
    //        float rtn = DecodeFloat(GetReg(reg), GetReg((ushort)(reg + 1)));
    //        return rtn;
    //    }

    //    private void SyncLoop(object sender, ElapsedEventArgs e) {
    //        foreach (D88A42ModbusAdapter adapter in _adapters) {
    //            adapter.UpdateRegisters();
    //        }
    //    }
    //    public static void EncodeFloat(float value, out ushort high, out ushort low) {
    //        byte[] bytes = BitConverter.GetBytes(value); // Already in correct endian
    //        high = (ushort)((bytes[1] << 8) | bytes[0]);
    //        low = (ushort)((bytes[3] << 8) | bytes[2]);
    //    }

    //    public static float DecodeFloat(ushort high, ushort low) {
    //        byte[] bytes = new byte[4];
    //        bytes[0] = (byte)(high & 0xFF);
    //        bytes[1] = (byte)(high >> 8);
    //        bytes[2] = (byte)(low & 0xFF);
    //        bytes[3] = (byte)(low >> 8);
    //        return BitConverter.ToSingle(bytes, 0);
    //    }

    //}
}
