# Auditor v1 Summary — runtime2-test-module

## What this is

Fresh-eyes audit of the PLang test module branch after codeanalyzer v3 (clean), tester v6 (approved), and security v1 (pass with 4 low findings). Security finding #3 was auto-patched in commit 9dc148f5 without re-review — my job was to catch what fell through those seams.

## What was done

Nine cross-cutting probes, no code changes:

1. **AfterAction payload widening** — all emit/subscribe sites conform. Clean.
2. **Step-propagation root-cause fix** — `SplitAtConditions` uses `this[i]` indexer; no other `_items[i]` call sites need Step. Clean.
3. **`Goal.LoadedFromPrPath` / `GetRuntimeDirectory` contract** — single consumer in `Path.cs`. No missing migration sites. Clean.
4. **`PushCancellation` / `PopCancellation` symmetry** — both call sites (`timeout/after.cs`, `test/run.cs`) use try/finally. Clean.
5. **Security fix 9dc148f5 (Strip/Mask split)** — unit test coverage good; gaps found below.
6. **Sensitive masking completeness across report paths** — **gap found** (finding #2): JUnit XML path unmasked.
7. **Coverage subscriber** — production subscriber in place, tester F2 concern resolved. Clean.
8. **`test.discover` / `test.tag` / `test.run` OBP compliance** — Steps/Actions collections own iteration; ChildAppCreated test hook is narrow (internal static event). Clean.
9. **F11 weak discriminator** — **gap found** (finding #1): rated minor by tester, I rate major.

## Findings

- **F1 (major)** — `TestReportWritesJunitXml` / `TestReportIncludesCoverageTables` assert on an input-echo scalar (`%report.format%`) instead of file content. Deleting the `case "junit":` branch still passes.
- **F2 (major)** — `BuildJUnit` emits `run.Error?.Message` with no Sensitive-property masking. Security finding #3 named both `results.json` and `junit.xml`; the fix only covered the first. `AssertionError.FormatValue` uses `value.ToString()`, so any test asserting on an object with `[Sensitive]` props can leak the raw payload into the JUnit CI artefact.
- **F3 (minor)** — No end-to-end test for the mask fix. If `report.cs:281` reverts from `DiagnosticOutput` to `CamelCaseIndented`, nothing fails.
- **F4 (nit)** — `SensitivePropertyFilter.Mask` silently strips non-string sensitive props — latent, violates `DiagnosticOutput`'s stated intent.

F1 and F2 compound: F1 means F2 has no regression detection. Route back to coder for both; single fix in `AssertionError.FormatValue` + one-line changes in each `.test.goal` closes it.

## Code example — the fix I'm recommending

The clean OBP form for F2 is to put formatting on the owner:

```csharp
// AssertionError.cs — today
private static string FormatValue(object? value)
{
    if (value == null) return "(null)";
    if (value is string s) return $"\"{s}\"";
    return value.ToString() ?? "(null)";
}

// FormatValue(someIdentity) → "Identity { PrivateKey=secret456, ... }" — leaks.
```

vs.

```csharp
private static string FormatValue(object? value)
{
    if (value == null) return "(null)";
    if (value is string s) return $"\"{s}\"";
    if (value.GetType().IsPrimitive || value is decimal || value is DateTime) return value.ToString() ?? "(null)";
    try { return JsonSerializer.Serialize(value, App.Utils.Json.DiagnosticOutput); }
    catch { return value.GetType().Name; }  // don't fall back to ToString — that's the vector
}
```

Console, BuildJson, and BuildJUnit all pull from `AssertionError.Message` via this path, so one edit fixes all three.

## Verdict

**fail** — two major findings. Send to coder, not to docs.
