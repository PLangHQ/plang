# auditor summary — typed-action-returns

## Version
v1 — first pass.

## What this is

Cross-cutting audit of `typed-action-returns` after codeanalyzer v3, tester v2,
and security v1 all returned PASS. The branch ships typed action `Run()`
returns (`Task<Data<T>>`), a compile-time `IClass.Build()` hook that stamps an
inferred PLang Type onto the terminal `variable.set`, HTTP Response with
Content-Type body dispatch, and supporting renames. ~944 files; four merge
commits since runtime2.

## What was done

- Read all three reviewer reports + verdicts + the coder handoff. Focused on
  the seams each one didn't naturally cover.
- Verified the four seams I flagged in `v1/plan.md`:
  - **Ask.ToString() migration** — both named consumers read `.Answer` directly; the
    `ToString() => Answer` ergonomic is for PLang interpolation only. Clean.
  - **ShouldExit value-side opt-out** — `data.ShouldExit` probes `IExitsGoal` before
    Type.Exit(); Ask's resolved branch flows through correctly.
  - **Build() pass safety** — `RunBuildPass` doesn't wrap, but no current
    Build() impl does IO outside file.read (which swallows its own broad
    exceptions). Forward-note worth adding to `good_to_know` docs.
  - **Channel("builder") discipline** — security F1 already covers it; the
    one production caller (`file.read.Build()`) is correct usage.
- Found a major cross-file gap: `HttpBuildHelpers.InferTypeFromUrl` is missing
  the registered-types gate that `file/read.cs:60-65` carries. Literal URLs
  with extensions like `.pdf`, `.html`, `.png`, `.docx` pass the MIME filter
  but not `Types.Get` → `variable.set` then rejects with "Unknown type 'X'".
  Confirmed by reading `variable/set.cs:64-68` and the registered-types table
  in `app/types/this.cs:55-77`.
- Cross-checked test coverage: `Stage4_BuildMethodImplsTests.cs:133-138` only
  exercises `https://api/x.json` (a registered alias), so the gap is invisible
  to the suite.

Output files:
- `v1/plan.md` — what I checked, what I deferred.
- `v1/result.md` — findings detail and reproducer.
- `v1/verdict.json` — `fail`.
- `.bot/typed-action-returns/auditor-report.json` — structured findings.

## Code example — the gap (one file)

```csharp
// HttpBuildHelpers.cs — current (regression):
var ext = clean[lastDot..];
var mime = app?.Formats.Mime(ext) ?? "application/octet-stream";
if (mime == "application/octet-stream") return Task.FromResult(data.@this.Ok());
return Task.FromResult(data.@this.Ok(ext.TrimStart('.').ToLowerInvariant()));
// → stamps "pdf" for https://x/report.pdf → runtime "Unknown type 'pdf'"

// file/read.cs — correct (mirror this):
var typeName = p.Extension.TrimStart('.').ToLowerInvariant();
if (Context.App.Types.Get(typeName) == null) return data.@this.Ok();
```

## Verdict

**FAIL.** One major cross-file finding. Fix is ~3 lines + 1 regression test in
`PLang.Tests/App/TypedReturnsTests/Stage4_BuildMethodImplsTests.cs`.

Next bot: **coder** (fix F1).
