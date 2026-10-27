# codeanalyzer summary — literal-typed-by-standard-form

**Version:** v1. Verdict: FAIL, coder next (one finding).

**What this is:** a quoted literal in an open `item` slot is typed by its strict standard form (datetime, date, time, ISO duration); anything else stays text. Each type owns `IsStandard`; the formal reader asks them.

**What was done:** read the reader, the four types, `iso.Read`, the tests, the teaching and the rebuilt `.pr`/golden. Clean apart from D1: `duration.IsStandard` rides `iso.Read`, which accepts `"PT"`, `"P1DT"` and repeated units, so a quoted `"PT"` becomes a duration; the other three are strict. Fix in `iso.Read` with two pins. Noted for Ingi: the reader names the four types (fork by type name).

**Example:**
```csharp
internal static bool IsStandard(string text) => iso.@this.Read(text) is not null;   // Read("PT") is a zero span, not null
```
