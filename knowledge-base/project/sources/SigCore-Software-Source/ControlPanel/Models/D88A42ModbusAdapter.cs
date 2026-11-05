using System;
using System.Collections.Generic;
//using static ControlPanelLib.ControlPanelManager;

namespace ControlPanelLib.Models {
    //public class D88A42ModbusAdapter {
    //    private readonly ControlPanelManager _cp;
    //    private ModbusTcpServerWrapper _parent;
    //    private readonly ushort _serialSuffix;
    //    private ushort _offset;
    //    Dictionary<Relays, bool> relayLastVal = new Dictionary<Relays, bool>();
    //    Dictionary<AnalogOutputs, float> aoLastVal = new Dictionary<AnalogOutputs, float>();

    //    public D88A42ModbusAdapter(ControlPanelManager controlPanel, ModbusTcpServerWrapper parent, ushort offset) {
    //        _cp = controlPanel ?? throw new ArgumentNullException(nameof(controlPanel));
    //        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    //        _offset = (ushort)(offset + 1);

    //        string fullSerial = _cp.CPSerialNum;
    //        if (!string.IsNullOrEmpty(fullSerial) && fullSerial.Length >= 5) {
    //            string suffixStr = fullSerial.Substring(fullSerial.Length - 5);
    //            if (ushort.TryParse(suffixStr, out ushort suffix))
    //                _serialSuffix = suffix;
    //        }

    //        foreach (Relays relay in Enum.GetValues(typeof(Relays))) {
    //            relayLastVal[relay] = _cp.GetDigitalOutput(relay);
    //        }

    //        foreach (AnalogOutputs ao in Enum.GetValues(typeof(AnalogOutputs))) {
    //            aoLastVal[ao] = (float)_cp.GetAnalogOutput(ao);
    //        }
    //    }

    //    public void UpdateRegisters() {
    //        foreach (ControlPanelManager.Relays relay in Enum.GetValues(typeof(ControlPanelManager.Relays))) {
    //            ushort reg = (ushort)(relay + _offset); 
    //            bool state = _cp.GetDigitalOutput(relay);
    //            if (relayLastVal[relay] != state) { 
    //                _parent.UpdateReg(reg, (ushort)(state ? 1 : 0));
    //                relayLastVal[relay] = state;
    //            }
    //        }
    //        foreach(ControlPanelManager.DigitalInputs di in Enum.GetValues(typeof(ControlPanelManager.DigitalInputs))) {
    //            ushort reg = (ushort)(di+10 + _offset); 
    //            bool state = _cp.GetDigitalInput(di);
    //            _parent.UpdateReg(reg, (ushort)(state ? 1 : 0));
    //        }
    //        foreach (ControlPanelManager.AnalogInputs ai in Enum.GetValues(typeof(ControlPanelManager.AnalogInputs))) {
    //            ushort reg = (ushort)(((ushort)ai) * 2 - 1 + 20 + _offset);
    //            float state = (float)_cp.GetAnalogInput(ai);
    //            _parent.UpdateReg((ushort)reg, state);
    //        }
    //        foreach (ControlPanelManager.AnalogOutputs ao in Enum.GetValues(typeof(ControlPanelManager.AnalogOutputs))) {
    //            ushort reg = (ushort)(((ushort)ao) * 2 - 1 + 30 + _offset);
    //            float state = (float)_cp.GetAnalogOutput(ao);
    //            if (aoLastVal[ao] != state) {
    //                _parent.UpdateReg(reg, state);
    //                aoLastVal[ao] = state;
    //            }
    //        }

    //        foreach (ControlPanelManager.Relays relay in Enum.GetValues(typeof(ControlPanelManager.Relays))) {
    //            ushort reg = (ushort)(relay + _offset);
    //            bool modbusVal = _parent.GetReg(reg) == 1;
    //            if (modbusVal != relayLastVal[relay]) {
    //                _cp.SetDigitalOutputExt(relay, modbusVal);
    //                relayLastVal[relay] = modbusVal;
    //            }
    //        }
    //        foreach (ControlPanelManager.AnalogOutputs ao in Enum.GetValues(typeof(ControlPanelManager.AnalogOutputs))) {
    //            ushort reg = (ushort)(((ushort)ao) * 2 - 1  + 30 + _offset);
    //            float modbusVal = _parent.GetRegFloat(reg);
    //            if (modbusVal != aoLastVal[ao]) {
    //                _cp.SetAnalogOutputExt(ao, modbusVal);
    //                aoLastVal[ao] = modbusVal;
    //            }
    //        }
    //    }



    //}
}
