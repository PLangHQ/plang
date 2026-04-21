# Security review v1 — runtime2-test-module

## What this is
First security pass on the `runtime2-test-module` branch, which adds a PLang test module (`test.discover`, `test.run`, `test.report`, `test.tag`) with per-test child-App isolation, timeout, parallel semaphore, thread-safe Coverage tracking, AssertionError variable snapshots, and AfterAction event payload widening.

## Threat model applied
Test mode is opt-in via `--test` — local dev / CI tooling. Trust boundary (signed .pr) is unchanged. The per-test child App pattern is the right isolation unit. Attack surface worth auditing: output rendering to console/XML/JSON, path handling in discover, isolation between parallel tests, variable snapshot info disclosure.

## What was done
Reviewed ~70 files and 5.2k LOC of change. Verdict: **pass**. 4 low-severity findings, no critical/high.

Files audited in depth:
- `PLang/App/modules/test/discover.cs` — recursion depth guard + visited set + fs.ValidatePath ✓
- `PLang/App/modules/test/run.cs` — child App + linked CTS + ConcurrentQueue Results ✓
- `PLang/App/modules/test/report.cs` — **ANSI strip incomplete** + **no C0 filter for XML**
- `PLang/App/modules/test/tag.cs` — no-op outside test mode ✓
- `PLang/App/Test/{Coverage,Results,TestRun,this}.cs` — thread-safe ✓
- `PLang/App/Errors/AssertionError.cs` + `PLang/App/modules/assert/AssertSnapshot.cs` — **snapshot may carry secrets**
- `PLang/App/modules/condition/if.cs` — orchestration guard key on Context (not Variables) ✓
- `PLang/Executor.cs` — `--test={…}` apply with bounds check ✓

## Findings
1. **Low / info-disclosure** — `StripAnsi` regex matches only CSI; OSC/DCS slip through and can forge hyperlinks or manipulate terminal title from captured test stdout.
2. **Low / info-disclosure** — `SecurityElement.Escape` does not strip XML-invalid C0 control chars; a failing test with binary bytes in error breaks strict JUnit parsers (DoS-of-reporting).
3. **Low / info-disclosure** — `Variables.Snapshot()` ignores `[Sensitive]`; a failing test's tokens/PII land in `results.json`.
4. **Accepted-risk** — Recursion is O(N) in user's own goal tree under `--test`; not a real attack.

## Code example (recommended fix pattern for #1)

```csharp
// PLang/App/modules/test/report.cs — replace single-CSI regex with a broader strip.
private static readonly Regex AnsiEscape = new(
    @"\x1B(?:\][^\x07\x1B]*(?:\x07|\x1B\\)" +   // OSC (ends BEL or ST)
    @"|P[^\x1B]*\x1B\\" +                         // DCS
    @"|\[[0-?]*[ -/]*[@-~]" +                     // CSI (kept)
    @"|\([AB012]" +                               // charset designator
    @"|.)",                                       // other single-byte escapes
    RegexOptions.Compiled);
```

## Outputs
- `.bot/runtime2-test-module/security-report.json` — full attack surface + findings
- `.bot/runtime2-test-module/security/v1/verdict.json` — `pass`

## Next
Suggest handing back to **auditor** for an independent cross-check on the same surface; no coder fixes are required to ship.
