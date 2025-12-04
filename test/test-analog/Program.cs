using Iot.Device.Mcp23xxx;
using System.Device.I2c;
using System.Diagnostics;
using test_analog;

class Program {
    static void Main() {
        // --- MCP23017 SETUP ---
        I2cConnectionSettings mcpSettings = new I2cConnectionSettings(1, 0x22);
        I2cDevice mcpDevice = I2cDevice.Create(mcpSettings);
        Mcp23017 mcp = new Mcp23017(mcpDevice);
        ushort output = 0;
        double[] zeroComp = new double[] { 0, 0, 0, 0 };

        mcp.WriteUInt16(Register.IODIR, 0x0000);

        // --- ADS1115 SETUP ---
        I2cConnectionSettings adsSettings = new I2cConnectionSettings(1, 0x48);
        I2cDevice adsDevice = I2cDevice.Create(adsSettings);
        using (Ads1115Driver ads = new Ads1115Driver()) {
            ads.MeasuringRange = MeasuringRange.FS6144; // ±6.144V input range

            Thread.Sleep(10); // Delay to allow initialization to complete

            output = 0xFFFF;
            mcp.WriteUInt16(Register.OLAT, output);

            zeroComp = ads.ReadAllChannels();
            for (int ch = 0; ch < zeroComp.Length; ch++) {
                Console.Write($"AIN{ch}:{zeroComp[ch]:0.00000} V | ");
            }

            // output = 0xFF00 >>> 5V
            // output = 0xFF55 >>> 10V
            // output = 0xFFAA >>> Current
            // output = 0xFFFF >>> GND
            output = 0xFF00;
            mcp.WriteUInt16(Register.OLAT, output);

            Stopwatch sw = Stopwatch.StartNew();
            sw.Stop();

            double[] volts;
            while (true) {
                sw.Restart();
                volts = ads.ReadAllChannels();
                sw.Stop();
                for (int ch = 0; ch < volts.Length; ch++) {
                    Console.Write($"AIN{ch}:{volts[ch]:0.00000} V | ");
                }
                Console.WriteLine($"Elapsed Time:{sw.ElapsedMilliseconds}"); // Blank line between cycles
                Thread.Sleep(1000);
            }
        }

    }
}