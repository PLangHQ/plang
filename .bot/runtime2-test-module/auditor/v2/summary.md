# Auditor v2 — Summary

## What this is

Second audit pass on `runtime2-test-module` after the coder addressed my v1
fail verdict. v1 flagged four findings (2 major, 1 minor, 1 nit) all clustered
around security fix `9dc148f5` being scoped to the JSON path only — leaving
the JUnit path unmasked, and the tests that should have detected the leak
asserting on input-echo scalars instead of file content. Coder pushed
`152d2a8e` with fixes. v2 verifies each finding is discharged by both a code
fix *and* a test that would catch regressions.

## What was done

- Read all v1 findings from `.bot/runtime2-test-module/auditor-report.json`.
- Read coder's full diff `b7819961..152d2a8e` (17 files, +862 −93).
- Ran `PLang.Tests` filter for `SensitivePropertyFilterTests`: 9/9 pass.
- Ran `plang --test` in `Tests/TestModule`: 20/20 pass including both new
  masking tests and both edited format-routing tests.
- Inspected committed snapshot `junit_sensitive_masked.xml` — confirmed
  `privateKey: "******"` in the `<failure>` element, `publicKey` visible.
- Cross-checked `BuildJUnit`'s `run.Error?.Message` flows through
  `AssertionError.FormatMessage → FormatValue → Json.FormatForDiagnostic →
  DiagnosticOutput`.

**Verdict: pass.** All four findings discharged.

## Code example — the keystone fix

The v1 finding #2 suggestion: "route `AssertionError.FormatValue` through
`Json.DiagnosticOutput` — benefits console, JSON, and JUnit paths in one
place." Coder did exactly that, and went further by consolidating three
`FormatValue` copies behind one owner:

```csharp
// PLang/App/Utils/Json.cs
public static string FormatForDiagnostic(object? value)
{
    if (value == null) return "(null)";
    if (value is string s) return $"\"{s}\"";
    var type = value.GetType();
    if (type.IsPrimitive || value is decimal || value is DateTime
        || value is DateTimeOffset || value is TimeSpan || value is Guid
        || value is Enum)
        return value.ToString() ?? "(null)";
    try { return JsonSerializer.Serialize(value, DiagnosticOutput); }
    catch { return type.Name; }
}

// AssertionError.cs, DefaultAssertProvider.cs, report.cs — all now:
private static string FormatValue(object? value) => App.Utils.Json.FormatForDiagnostic(value);
```

OBP win: one owner for diagnostic formatting. Future Sensitive rules land in
one place.

## Files reviewed (no changes made)

- `PLang/App/Utils/Json.cs`
- `PLang/App/Errors/AssertionError.cs`
- `PLang/App/Channels/Serializers/SensitivePropertyFilter.cs`
- `PLang/App/modules/test/report.cs`
- `PLang/App/modules/assert/providers/DefaultAssertProvider.cs`
- `PLang.Tests/App/Serializers/SensitivePropertyFilterTests.cs`
- `Tests/TestModule/Report/*.test.goal` (4 files)
- `Tests/TestModule/Report/_fixtures_sensitive/sensitivefail.fixture.goal`
- `Tests/TestModule/Report/snapshots/junit_sensitive_masked.xml`
- All regenerated `.pr` files in `Report/.build/`

## Next

Suggest running the **docs** bot next.
