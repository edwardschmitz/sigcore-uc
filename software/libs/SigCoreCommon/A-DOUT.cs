using System;
using System.Collections.Generic;
using System.Device.I2c;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigCoreCommon {
    public class A_DOUT : Mcp23017Driver {
        private const int address = 0x22;

        public A_DOUT() : base(address) {
        }
        public void ChangeState(int output, bool state) {
            if (output < 0 || output >= Outputs.Length) {
                throw new ArgumentOutOfRangeException(nameof(output), "Output index must be between 0 and 15.");
            }

            Outputs[output] = state;
            SendRegisters();  // Update the MCP23017 with the new output state
        }
    }
}
