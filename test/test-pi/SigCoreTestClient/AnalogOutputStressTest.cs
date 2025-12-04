using System;
using System.Threading.Tasks;
using SigCoreCommon;

public class AnalogOutputStressTest {
    private readonly SigCoreSystem _client;
    private readonly uint _outputCh = 2; // AO3
    private readonly uint _inputCh = 2;  // AIN1
    private readonly double _tolerance = 0.25;
    private readonly int _settleDelayMs = 1000;
    private int failureCount = 0;

    public AnalogOutputStressTest(SigCoreSystem client) {
        _client = client;
    }

    public async Task RunAsync() {
        Console.WriteLine("Starting analog output stress test (AO1 <-> AIN3)...\n");
        ulong step = 0;
        string stat = "";

        while (true) {
            stat = string.Empty;

            // Step 1: PWM 100%
            stat += await DoStepAsync(A_OUT.OutputMode.PWM, 100.0, 5.0);

            // Step 2: Voltage 2V
            stat += await DoStepAsync(A_OUT.OutputMode.Voltage, 2.0, 2.0);

            // Step 3: PWM 0%
            stat += await DoStepAsync(A_OUT.OutputMode.PWM, 0.0, 0.0);

            // Step 4: Voltage 4V
            stat += await DoStepAsync(A_OUT.OutputMode.Voltage, 4.0, 4.0);

            Console.WriteLine($"{stat} >>> Step: {step} >>> Failures: {failureCount}");
            step++;
        }
    }

    private async Task<string> DoStepAsync(A_OUT.OutputMode mode, double setValue, double expected) {
        A_OUT.AnalogOutChannelConfig config = await _client.GetAOutConfigAsync(_outputCh);
        config.Mode = mode;

        await Task.Delay(100); // allow output to settle

        await _client.SetAOutConfigAsync(_outputCh, config);

        await Task.Delay(100); // allow output to settle

        await _client.SetAOutValueAsync(_outputCh, setValue);

        await Task.Delay(_settleDelayMs); // allow output to settle

        double actual = await _client.GetAnalogInputAsync(_inputCh);
        double delta = Math.Abs(actual - expected);
        bool pass = delta <= _tolerance;
        string status = pass ? $"p  :" : $"FFF:";
        status += $" {actual:0.00}, ";
        if (!pass) failureCount++;

        return($"{status} ");
    }
}
