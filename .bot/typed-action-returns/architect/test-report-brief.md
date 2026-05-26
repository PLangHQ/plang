# Brief — `test.report` should return a typed object, not a serialized string

**Branch:** `test-report-typed-object` (off `fix-stepvartypes-incremental`)
**Author:** Ingi (via Claude) — 2026-05-25
**For:** architect bot, then coder + tester

## The principle (Ingi)

> "Serialization (such as JSON) belongs at the channel level. This really
> should return an object, a strongly typed object, then it goes where it
> goes and when somebody needs serialization of it, that is a different
> concern."

In other words: a domain action's job is to **produce a strongly-typed
value**. JSON / XML / Markdown / binary encoding is decided downstream by
whoever writes that value to a channel — never by the producer.

## The offender — `PLang/app/modules/test/report.cs`

The action handler today does three things it shouldn't:

1. **Serializes JSON inline.** `BuildJson` builds an anonymous object
   (variable name `envelope` — already a name smell) and calls
   `JsonSerializer.Serialize`. The shape is locked to one renderer.
2. **Serializes JUnit XML inline.** `BuildJUnit` hand-builds an XML
   string via `StringBuilder`.
3. **Writes to disk via `System.IO`.** `Path.Combine` +
   `Directory.CreateDirectory` + `File.WriteAllTextAsync` bypass
   `FilePath.AuthGate`. Same shape the `purge-systemio-from-actions`
   branch is already addressing.

Triple violation: domain rendering, encoding, and persistence all mashed
into one action handler. Each belongs in a different layer.

## What "right" looks like

Three separate layers:

- **Domain (`app/tester/Report.cs` + siblings, new).** A strongly-typed
  `Report` record with `Summary`, `BuilderVersion`, `Runs`,
  `BranchCoverage`. `Runs` is a list of a typed `RunReport` carrying
  `Path`, `Status`, `DurationMs`, `Output`, `Timings`, `Error?` —
  the fields the current anonymous object inlines.
- **Encoding (`app/channels/serializers/`).** JSON serializer already
  exists. JUnit is out of scope (Ingi: "let it do its thing as today" —
  the hand-rolled XML stays in `BuildJUnit` until a follow-up).
- **Persistence (`app/types/path/file/…`).** `FilePath.Save(data)` for
  the file write; routes through `AuthGate`, picks the serializer by
  file extension.

`test.report` becomes a pure compute: build the typed `Report` and
return it as `Data<Report>`. No `JsonSerializer.Serialize`. No
`System.IO`. The caller (today `os/system/test.goal`) decides where the
Report lands.

## Calling-site change (today vs. after)

Today, in `os/system/test.goal:7`:

```plang
- test.report results %results%
```

…and `test.report` silently writes `.test/results.json`.

After:

```plang
- test.report results %results%, write to %report%
- file.save %report%, to '.test/results.json'
```

The file write is now **visible in the PLang source**, the path is
explicit, the format is implicit-from-extension via the channel
serializer registry. This is a contract change — call out for the
architect to confirm acceptable.

## Open questions for the architect

1. **Pure compute or internal persistence?** Two shapes:
   - **(a) Pure compute.** `test.report` returns `Data<Report>`. Caller
     persists via `file.save`. Cleanest; surfaces the persistence step
     in `.goal` source; matches "serialization is a channel concern".
     Contract change ripples to every `test.goal` that today relies on
     `test.report` writing the artifact.
   - **(b) Internal persistence via channel.** Action still writes the
     file, but via `FilePath.Save(data)` (gated, serializer-keyed by
     extension). No PLang-source ripple. Still does too much — the
     action knows both the report shape AND the artifact location.

   Ingi's principle ("return an object … then it goes where it goes")
   reads as (a). Confirm.

2. **Format selection.** Today `Testing.Format` ("json" or "junit")
   chooses the artefact. If (a), this collapses into "file extension
   chooses the serializer" via the `Serializers` registry — JUnit
   stays out of scope per Ingi, so for now the `Format` parameter is
   effectively unused by the typed-object path. Keep it as a no-op for
   backcompat? Drop it?

3. **Relationship to `purge-systemio-from-actions` branch.** That
   branch is also going to touch `report.cs` (the `System.IO` part).
   Two branches both rewriting the same file — sequence them, or fold
   this branch's `System.IO` removal into the purge branch?

## Tests we'll need

- **C# unit tests** in `PLang.Tests`:
  - Construct fake `Results` (with passes, fails carrying `Variables`,
    skips, timeouts). Call `test.report`. Assert `Data<Report>` has the
    expected shape: counts in `Summary` match, `Runs` in source order,
    `Timings` per run pass through, `Error.Message` populated for fails,
    cyclical-error snapshots survive (the `IgnoreCycles` fix from
    `e6b0e9ea1` stops being load-bearing once cycles are pruned at
    capture, but until then the unit test should still pass).
  - Snapshot test pinning the **JSON serialization shape** so the
    webui-facing JSON keys (`runs`, `output`, `timings`, `stepIndex`,
    `ms`, …) don't drift on refactor.
- **PLang `.test.goal`** under `Tests/TestModule/Report/`:
  - `test.report` returns Data with a `Report` shape (assert on
    `%report.summary.Pass%`, `%report.runs.0.path%`, etc.). Today the
    same module has goals asserting `%report.reportPath%` /
    `%report.content%` — those keys may need to change shape once the
    action stops doing the file write itself. Tester reviews.

## Status

- Branch created off `fix-stepvartypes-incremental`. No code yet.
- Awaiting architect call on (1), (2), (3) before coder picks up.

## Reading list

- `PLang/app/modules/test/report.cs` — the offender, in full.
- `PLang/app/modules/test/run.cs` — sibling, produces the `Results` that
  feed `test.report`. Touched in `e6b0e9ea1` for timings/output.
- `PLang/app/tester/{Results,Run,File,Coverage,Timing,Timings}.cs` — the
  domain types that the new `Report` projects over.
- `PLang/app/channels/serializers/` — where JSON encoding actually lives.
- `os/system/test.goal` — the calling goal, will change if we pick (a).
- `.bot/purge-systemio-from-actions/brief.md` — the sibling
  architectural concern. Same file, same kind of fix from a different
  angle.
