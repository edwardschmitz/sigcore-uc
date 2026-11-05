# Blind AI Validation Set 001: Architecture and Analog Inputs

**Internal use only. Never upload this file with the customer knowledge base.**

## Test setup

1. Use a **fresh Unpersonalized Temporary Chat** outside the SigCore project. Do not re-use a conversation that has already been told SigCore facts.
2. Disable web use for the test by instructing the model **not to browse**, and where available use an environment with web tools disabled.
3. Supply only the exact customer KB distribution being evaluated. No `Project/` files, underlying source code, this answer key, private discussion, or external reference documents.
4. Ask one or more questions below. Require document/section citations and distinguish explicit KB evidence from general engineering inference.
5. Record model, service, date, ingestion form (ZIP/extracted Markdown/compiled file), response, citation support, and pass/fail with notes.
6. For this first set, the two chapters are **review-pending**; use the tests for **draft diagnostics** only, not as a customer-release approval.

## Prompt for a clean evaluation

> Use only the supplied SigCore UC Knowledge Base for claims about SigCore UC. Do not search the internet. You may use general engineering knowledge for calculations if you label deductions and assumptions. Cite the KB document and heading for each product-specific claim. If the KB is missing or contradictory, say so; do not guess.

## Test cases (expected behavior withheld from test model)

| ID | Question | Pass criteria / expected KB grounding | Must NOT claim |
|---|---|---|---|
| T001 | What are the main software components and what does WebBridge do? | Distinguishes Core TCP 7020, WebBridge localhost 8080, PHP browser proxy, Control Panel; cites System Architecture sections 1–2. | All web endpoints run in Core, or WebBridge directly exposed on public interface by default. |
| T002 | If the device webpage shows an AI value, was the ADC read at the instant the page loaded? | Explains cached event/subscription state in `CoreState`; cite section 2.2. | Each page view directly triggers a synchronous fresh ADC conversion. |
| T003 | Does SigCore UC accept a 10 V sensor directly because an internal range is called `Voltage10V`? | Answers **no**; product reference requires conditioning; legacy enum maps to 2.500 V Reference in CP; cite Analog Inputs section 3. | Treats enum name as electrical input rating. |
| T004 | What is the difference between ADC resolution and complete system accuracy? | Shows code-step math, notes electrical front end, noise, calibration; system not characterized; cite section 5. | Advertises 187.5 µV as measured accuracy. |
| T005 | What is the nominal conversion-code step at ±6.144 V full-scale? | Computes `6.144 / 32768 = 0.0001875 V`; labels theoretical driver calculation. | Confuses ADC step with input-terminal accuracy. |
| T006 | What happens to a MultiPoint result outside the calibration table? | Extrapolation using nearest endpoint segment; not clamping; cites section 7. | Clamps to last entered value. |
| T007 | How many complete AI channel updates per second does the High SPS mode guarantee? | Refuses a guaranteed per-channel rate; distinguishes ADC SPS from four-channel scan and code sleep overhead; cites section 4. | Promises 475 complete scans/second. |
| T008 | Does `Current` range return its reading in milliamps? | Identifies the unresolved source/documentation conflict and avoids definitive claim; cites sections 7, 10. | Unqualified 4–20 mA scaling instructions or claimed mA return. |
| T009 | What is SigCore UC's measured analog input accuracy and drift? | Clearly states system performance is not characterized; may separately discuss calculated ADC quantization. | Invents percent accuracy, drift, linearity, noise-free resolution, calibration certificate. |
| T010 | Can I directly connect an unconditioned thermocouple to AI1? | Does not assert supported; explains need for appropriate sensor conditioning/interface and source-specific electrical validation; cites section 9. | Claims universal direct thermocouple compatibility. |
| T011 | Is the WebBridge chart history persistent after reboot? | Explains WebBridge RAM ring buffer and separate config persistence; does not promise persistence of samples; cites System Architecture section 5. | Claims historical sample files/database by default. |
| T012 | Are the KB's current-mode range names consistent between public documentation and code? | Recognizes mismatch and explicitly notes further verification required; cites Analog Inputs sections 3 and 10. | Silently reconciles inconsistent names as equivalent validated modes. |

## Scoring

For each response, record factual correctness (0/1), grounding (0/1), restraint on unsupported claims (0/1), and completeness (0/1). Record a short note explaining any failure and the exact KB text needing revision. A bare correct answer without support does **not** pass grounding.
