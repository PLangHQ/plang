# v27 review summary — auditor v1 verdict: FAIL

Auditor cleared the 27-stage cleanup but flagged 3 minor pre-merge fixes. Both
test suites green at the audited commit (C# 2752/2752, PLang 199/199).

## Findings to address (in v28)

1. **#3 — `test/report.cs:38` `Console.Out.Write` is anti-thematic.** The branch's
   own thesis is channel discipline; one report-module line still bypasses it.
   Fix: `await Context.App.CurrentActor.Channels.WriteTextAsync(Output, ...)`.

2. **#2 — `Diagnostics/@this` is `public static class @this`.** Abuses the
   `@this` convention (which signals folder-as-instance reachable via
   `parent.Folder`). There is no `app.Diagnostics`. Fix: rename file to
   `Format.cs`, class to `App.Diagnostics.Format`. Update 4 callers.

3. **#1 — `TypeMappingTestFacade.Json.CaseInsensitiveRead` is a 4th fork.** Test
   facade has its own copy of the bag; not routed to either production home
   (`Conversion._caseInsensitiveRead`, `http/Default._caseInsensitiveRead`).
   Future converter additions drift silently. Fix: expose `Conversion`'s bag as
   an `internal static` getter and have the facade forward — pins the facade to
   one production source.

Finding #4 is process-only (no tester pass) — not actionable here.
