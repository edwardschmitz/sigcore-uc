---
title: "Measurement and Control Timing"
status: source-draft-review-required
hardware_revisions: "Rev B"
software_versions: "Supplied source snapshot; exact commit/release unknown; acquisition implementation may predate newer averaging work"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Measurement and Control Timing

> **Source-draft timing model, not a deterministic performance guarantee.** The values below are requested .NET loop delays, ADC conversion settings, and recorder intervals observed in supplied source. They are **not** certified sensor-to-actuator response times, frequency guarantees, or physical test results. In particular, the supplied ADC averaging routine appears to predate more recent moving-average development and must be reconciled with the released Core source.

## 1. Independent timing domains

| Domain | Scheduling / data rate in reviewed code | Meaning and constraints |
|---|---|---|
| ADS1115 conversion | `Low=8`, `Medium=32`, `High=475` samples/s ADC setting | **Per ADC conversion**, not per-channel or all-four-channel update rate |
| Analog acquisition | Outer Core loop requests `Task.Delay(1 ms)` before each pass through all four channels | Actual pass includes sequential mux and ADC waits, so delay is not total scan time |
| Digital inputs | Loop requests `Task.Delay(1 ms)` before `SampleInputs()` | Polling interval subject to OS task scheduling, I²C reads, and debounce |
| PID control | Loop requests `Task.Delay(100 ms)` then `ComputeAll()` | Approximate cadence; `ComputeAll()` also calls `Thread.Sleep(10)` in this snapshot |
| SQL data logger | Sleeps `IntervalSec` after each logging operation | Period is **logging work duration plus requested wait**, not fixed sample-on-clock cadence |
| WebBridge chart recorder | `PeriodicTimer(recordIntervalSec)`; default `1.0 s` | Records the WebBridge's latest four cached AI values when Core connection is ready |
| Browser chart live refresh | Separate UI/display concern | Display refresh frequency is not ADC sampling frequency and not necessarily the recording rate |

## 2. Analog measurement acquisition chain

The four analog inputs share the ADC behind external TMUX selection and input-conditioning paths. `A_IN.GetValue()` in the reviewed source iterates **AI0–AI3 sequentially**. For each channel:

1. Select the one enabled external TMUX using `SelectOnlyMux(ch)`; the other three mux-enable signals are disabled.
2. In `Ads1115Driver.ReadChannel(ch, configuredCount)`, set ADC channel multiplexer and gain.
3. Sleep for an ADC conversion-time delay; read and discard the first conversion after mux change.
4. Sleep again; read `configuredCount` ADC results, with a conversion-time sleep after each, accumulating and returning their average.
5. Store the result for that input; after all four channels, notify subscribers if any raw averaged value changed (or an all-values send was requested).
6. Apply that channel's calibration when consumers request `CurrentReading()` or the outgoing `CurrentValues()` array.

The driver uses these conservative, hard-coded conversion waits:

| Selected mode | ADS1115 configuration | Each programmed wait |
|---|---:|---:|
| Low | 8 SPS | 130 ms |
| Medium | 32 SPS | 34 ms |
| High | 475 SPS | 3 ms |

For **one channel** of averaging count `N`, this driver schedules roughly `(N + 2)` such waits, **plus** ADC/I²C operations and mux-switch overhead. A full four-channel acquisition pass incurs those waits **for each channel**. This expression is a source-derived scheduling observation, not a measured throughput claim. For instance, selecting Medium and N=1 does not imply 32 readings per second for **each** analog input.

### 2.1 Version-sensitive averaging behavior

The supplied files `A_IN.cs` and `Ads1115Driver.cs` implement **batch average N consecutive conversions per channel on each scan**. They do not show the more recently requested one-channel-per-iteration moving buffer. `Ads1115Driver.ReadChannel` rejects `samplesPerChannel < 1` but does **not** enforce an upper bound of 100; the acceptance of averaging values in Core, Control Panel, and WebBridge must be reconciled with the current deployment before documenting a universal `1..100` guarantee.

**Do not claim both algorithms are simultaneously current.** Engineering review must identify the exact source commit used in an approved product release. Refer to [Analog Inputs](../Hardware/Analog-Inputs.md) for the actual measurement ranges and calibration configuration.

## 3. Digital input polling and debounce

`HardwareManager.SampleDigitalInLoop()` requests 1 ms before each `DigitalIn.SampleInputs()`. `D_IN` maintains per-channel last-transition timestamps and configured `debounce` durations; the debounced, inverted logical states are reported to listeners on change. Configuring debounce changes interpretation of transitions; a 1 ms task wait does **not** mean a 1 ms guaranteed detection or safety reaction time. GPIO expansion and USB/network/UI delays are separate from this loop.

## 4. PID update timing and elapsed time

The PID loop requests a 100 ms delay, invokes `PIDs.ComputeAll()`, and sends changed PID values to subscribers. `PID_LOOP.ComputeAll()` uses a `Stopwatch` to determine elapsed wall-clock `dt` from its previous invocation. In the reviewed code it computes all enabled controllers and then sleeps **10 ms** before returning. The P, I, and D computations use `dt`, not a hard-coded exactly-0.1-s timestep. A blocked loop, source exception, or OS scheduling delay can therefore affect derivative and integral terms.

**Control-loop latency** comprises analog acquisition (potentially sequential and multi-conversion), scheduling and PID work, AO driver update, and the external process response. It is not the 100 ms PID timer alone.

## 5. Change events, snapshots and chart history

- Core can send analog notifications when reported values change; subscription network traffic is **event driven** and may be less frequent than conversion work. A stable reading does not imply sampling has stopped.
- `GetSnapshot()` takes available current values for AI/AO/DI/relay/PID at the moment called. It does not start a synchronized conversion of all channels and does not claim a simultaneous physical measurement instant.
- The SQL logger wakes after the previous database operation and delay, producing historical snapshots with their own timestamps.
- WebBridge `ChartRecorder` periodically captures the latest **cached** analog inputs only when its Core connection is `Connected`. The recording interval is independently configurable from `0.1` to `60` seconds in this source, default `1.0` second.
- Chart history queries can resample to the requested number of evenly spaced output times. They do **not** change the original recorder interval or recover samples before WebBridge's buffer start.

For recorder retention and history sampling rules see [Logging and History](Logging-and-History.md).

## 6. Practical interpretation for AI answers

If asked "How fast does SigCore sample?", distinguish **ADC conversion rate**, **effective AI channel updates**, **PID compute cadence**, **WebBridge chart sampling**, **SQL logging interval**, and **browser refresh**. Ask which one matters if necessary. Avoid conflating 475 SPS with four-channel throughput or declaring 100 ms closed-loop response. For maximum reliable update rate, use a measured performance characterization on the specific hardware, configured averaging, ADC rate, and released firmware version.

## 7. Source evidence and verification

- `libs/SigCoreCommon/HardwareManager.cs`: `ConfigureAnalogSampling`, `SampleAnalogInLoop`, `SampleDigitalInLoop`, `SamplePIDLoop`, `LoggerLoop`.
- `libs/SigCoreCommon/A_IN.cs`, `Ads1115Driver.cs`: mux selection, conversion averaging, data rate and waits.
- `libs/SigCoreCommon/D_IN.cs`, `PID_LOOP.cs`, `PidLoopModel.cs`.
- `WebBridge/ChartRecorder.cs`, `WebBridge/CoreState.cs`.

**Verify:** deployed moving-average implementation, per-channel effective latency under representative loads, max averaging validation, watchdog/error recovery, and measured PID/control jitter.
