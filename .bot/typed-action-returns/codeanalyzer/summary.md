# codeanalyzer summary — typed-action-returns

## Version

**v3** (third pass).

## What this is

Branch ships typed action `Run()` return signatures (`Task<Data<T>>`), per-action
`Build()` compile hook, multi-segment HTTP body dispatch via Content-Type, and
the supporting renames (`tester/File → tester/Test`, `MockHandle →
app.mock.Mock.@this`, `Schema → Types`). v3 follows up on v2's 11 findings
after the coder addressed F1-F7 and F9-F11 (F8 closed as won't-fix).

## What was done

- Re-scanned every v2 finding against the diff `117b754f3..HEAD` (4 commits,
  13 production files).
- All actionable v2 findings closed cleanly:
  - **F1** `Run.File → Run.Test` propagated through `test/report.cs` (8 sites).
  - **F2** `mock/action.cs → mock/intercept.cs`, class renamed; owner-side
    `Mock.ActionPattern → Pattern` for symmetry.
  - **F3, F4** HTTP leaf helpers return `Data<T>`; size-cap/slow-loris in one
    spot (`ReadLimitedStringAsync` now a UTF-8 wrapper).
  - **F5** bare `catch` narrowed to the project-standard exclusion pattern.
  - **F6** application/plang bypass documented (signature inflow rationale).
  - **F7** `Ask.ToString()` answer-leakage warning in docstring.
  - **F9** `(handler, _)` discard.
  - **F10, F11** stale `test.File.Path` / `File.Goal` comments fixed.
- New observations (info, not findings):
  - **N1** `goal/getTypes.cs` has 3 lingering `%__data__%` comments after the
    code switched to `%!data%` (trivial readability).
  - **N2** `file.read.Build()`'s registered-types gate also silences the
    missing-file warning for unregistered extensions; csv/txt/xml/yaml/yml are
    registered string-aliases in `app/types/this.cs` so the warning still fires
    for the common case.
- `dotnet build PlangConsole` clean (0 errors).

## Code example — the shape that defined v3

Before (v2 F3 leaf throw):

```csharp
if (totalRead > maxBytes)
    throw new InvalidOperationException(
        $"Response body exceeds maximum size of {FormatBytes(maxBytes)}");
```

After:

```csharp
if (totalRead > maxBytes)
    return data.@this<byte[]>.FromError(new ServiceError(
        $"Response body exceeds maximum size of {FormatBytes(maxBytes)}",
        "ResponseTooLarge", 413));
```

Outer `ExecuteHttpAsync` catch list shrank by removing
`InvalidOperationException` — size-cap and slow-loris errors now ride their own
structured key instead of being laundered through the outer catch.

## For v3 after review

- F2 fix went one property-rename further than the strict ask
  (`Mock.ActionPattern → Pattern`); reads cleaner because the intercept action's
  own parameter is `Pattern`.
- F3/F4 fix kept the `<byte[]>` cap path as the source of truth and made the
  string variant a thin UTF-8 wrapper — the leaf-Data rule's "discipline lives
  where the value is produced" applied correctly.

## Verdict

**PASS.** Branch is in shape to merge.
