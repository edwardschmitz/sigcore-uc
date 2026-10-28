"""
SigCore UC: Python.NET I/O example

Demonstrates reading the four I/O types and changing relay / analog outputs
through the existing SigCoreCommon .NET 8 library.

Install the example in the Examples folder under SigCore Common. The
published DLLs and dependencies are in the parent folder. Run from any directory:
    py test_sigcore_io.py
"""

from pathlib import Path
import time

from pythonnet import load
load("coreclr")  # Must be called before importing clr.

import clr
from System import UInt32

# Resolve the library relative to this script, not the working directory.
# Supports both Common/Examples/ and a script placed beside SigCoreCommon.dll.
SCRIPT_DIR = Path(__file__).resolve().parent
LIB_DIR = SCRIPT_DIR if (SCRIPT_DIR / "SigCoreCommon.dll").is_file() else SCRIPT_DIR.parent
if not (LIB_DIR / "SigCoreCommon.dll").is_file():
    raise FileNotFoundError(f"SigCoreCommon.dll not found in {LIB_DIR}")

import sys
sys.path.insert(0, str(LIB_DIR))
clr.AddReference("SigCoreCommon")
from SigCoreCommon import SigCoreSystem

# Connection settings. Use the IP address you successfully tested.
HOST = "192.168.0.128"
PORT = 7020
TIMEOUT_MS = 5000

# Output writing is OFF by default. Turn these on only when safe.
TEST_RELAY_WRITE = False
TEST_ANALOG_OUTPUT_WRITE = False
RELAY_TEST_CHANNEL = 4  # Zero-based: Relay 5
AO_TEST_CHANNEL = 3     # Zero-based: Analog Output 4
AO_TEST_VALUE = 1.0     # Example analog output value, in volts


def result(task):
    """Wait for a .NET Task or Task[T] and return its result, if any."""
    return task.GetAwaiter().GetResult()


def ch(index):
    """SigCore uses zero-based channel numbers represented as UInt32."""
    return UInt32(index)


def read_inputs_and_outputs(sc):
    # Digital inputs are read-only booleans (False/True).
    print("\nDIGITAL INPUTS")
    for i in range(8):
        state = bool(result(sc.GetDInAsync(ch(i))))
        print(f"  DI {i + 1}: {state}")

    # Relay outputs are booleans, and can also be controlled with SetRelayAsync.
    print("\nRELAY OUTPUTS")
    for i in range(8):
        state = bool(result(sc.GetRelayAsync(ch(i))))
        print(f"  Relay {i + 1}: {state}")

    # Analog inputs are read-only numeric values.
    # Interpretation/units depend on each channel's configuration.
    print("\nANALOG INPUTS")
    for i in range(4):
        value = float(result(sc.GetAnalogInputAsync(ch(i))))
        print(f"  AI {i + 1}: {value:.4f}")

    # Analog outputs return numeric values. Their modes/configuration matter.
    print("\nANALOG OUTPUTS")
    for i in range(4):
        value = float(result(sc.GetAOutValueAsync(ch(i))))
        print(f"  AO {i + 1}: {value:.4f}")


def test_relay(sc):
    """Toggle one relay and restore its original state, including on error."""
    channel = ch(RELAY_TEST_CHANNEL)
    original = bool(result(sc.GetRelayAsync(channel)))
    print(f"\nTesting Relay {RELAY_TEST_CHANNEL + 1}: original={original}")
    try:
        result(sc.SetRelayAsync(channel, not original))
        time.sleep(0.5)
        actual = bool(result(sc.GetRelayAsync(channel)))
        print(f"  Requested={not original}, read back={actual}")
        if actual != (not original):
            print("  WARNING: relay readback does not match")
    finally:
        result(sc.SetRelayAsync(channel, original))
        restored = bool(result(sc.GetRelayAsync(channel)))
        print(f"  Restored={restored}")


def test_analog_output(sc):
    """Set one analog output and restore the previous value, even on error."""
    channel = ch(AO_TEST_CHANNEL)
    original = float(result(sc.GetAOutValueAsync(channel)))
    print(f"\nTesting AO {AO_TEST_CHANNEL + 1}: original={original:.4f}")
    try:
        # Value must be appropriate for this output's configured mode/range.
        result(sc.SetAOutValueAsync(channel, float(AO_TEST_VALUE)))
        time.sleep(0.5)
        actual = float(result(sc.GetAOutValueAsync(channel)))
        print(f"  Requested={AO_TEST_VALUE:.4f}, read back={actual:.4f}")
    finally:
        result(sc.SetAOutValueAsync(channel, original))
        restored = float(result(sc.GetAOutValueAsync(channel)))
        print(f"  Restored={restored:.4f}")


def main():
    sc = SigCoreSystem()
    try:
        status = result(sc.ConnectAsync(HOST, PORT, TIMEOUT_MS))
        print(f"Connection status: {status}")
        if str(status) != "Connected":
            raise RuntimeError("Unable to connect to SigCore UC")

        read_inputs_and_outputs(sc)

        if TEST_RELAY_WRITE:
            test_relay(sc)
        if TEST_ANALOG_OUTPUT_WRITE:
            test_analog_output(sc)

    finally:
        sc.Dispose()


if __name__ == "__main__":
    main()
