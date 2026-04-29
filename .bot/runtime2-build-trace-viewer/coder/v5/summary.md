# v5 — Build-trace viewer, self-rebuild investigation

## What this is

Session focused on finishing the build-trace viewer (`system/builder/web/`)
and debugging why the builder cannot self-rebuild cleanly — specifically,
why `ApplyBuiltStep` step 0 was saving with zero actions.

## What was done

### Build-trace viewer
- Sidebar tree grouped by file / goal / trace
- File click shows raw `.goal` source
- Per-goal and per-file issue lists with drill-down
- Build-failure block renders raw response when trace has `buildError`
- Server `ROOT` adjusted to `../..` so it serves `/workspace/plang/system/`
  (the viewer is its own app now)

### Supporting builder changes (17 items — see handoff file)
Most important:
- `Step.PriorText` (transient) + `@known:` prompt marker — lets the LLM
  respond with `{index, keep:true}` for unchanged steps instead of
  re-emitting the mapping.
- `Goal.MergeFrom` now recurses into sub-goals (matched by `Name`).
- `Goal.GroupModifiersRecursive` applies modifier grouping to sub-goals.
- New `builder.enrichResponse` action backfills prior actions/formal
  into the trace response when `keep:true` and tags each step with
  `source: new | known | hint`.
- Failure-capturing `HandleBuildGoalFailure` saves a trace before
  re-throwing, so the viewer surfaces LLM errors.
- `OpenAiProvider` detects `finish_reason: length` / `content_filter`
  before JSON parsing and returns dedicated `ResponseTruncated` /
  `ResponseFiltered` errors with raw response attached.
- `llm.query` `MaxTokens` default raised to 16000.
- `FluidProvider` `formal` filter — renders `%var%` bare, strings quoted,
  dicts/lists JSON for goalFormatForLlm template.
- Debug formatter masks `[Sensitive]` properties; dicts/lists
  JSON-serialized instead of truncated.
- Build filter (`--build={"files":[...]}`) matches path-qualified
  entries against `Relative` path only, not bare `FileName` — stops
  picking up same-named goals in unrelated app trees.

### Self-rebuild investigation (partial — open for next session)

**Symptom.** `system/builder/.build/applystep.pr` saves with step 0 of
`ApplyBuiltStep` having `"actions": []`, even though the LLM trace
proves the model returned 2 actions for that step.

**What the original hypothesis said.** The merge pipeline was losing
actions when a parameter value was `%variable%` typed `list<action>`.

**What the investigation found.** That hypothesis is wrong. Added
three tests in `PLang.Tests/App/Modules/builder/StepFromDictConversionTests.cs`
that reproduce the exact dict → Step conversion path (including one
using the literal JSON from an actual trace file). **All three pass.**
`Actions.Count == 2` end-to-end through `TypeMapping.TryConvertTo`.

The real problem is upstream of the merge entirely: the current
`applystep.pr` has Fluid-wrapper strings where the goal.call `Name`
should be, e.g.
```
"value": {"name": "Fluid.Values.ObjectDictionaryFluidIndexable`1[System.Object]"}
```
instead of `"ApplyKeptStep"` / `"ApplyBuiltStep"`. Every
`condition.if ... call ApplyKeptStep` in the outer `ApplyStep.goal`
therefore resolves to an unknown goal name and silently no-ops.
`ApplyStep`, `ApplyKeptStep`, and `ApplyBuiltStep` runtime goals
never fire during a rebuild. The merge "dropping" actions is a
secondary effect — the merge is never called because the chain
that would call it is broken.

Full details + suggested next path in
`handoff-applybuiltstep-step0.md` (next-session update section).

## Code example — the dict → Step test that disproved the original hypothesis

```csharp
// PLang.Tests/App/Modules/builder/StepFromDictConversionTests.cs
[Test]
public async Task RealTraceJson_ToStep_ShouldPreserveActions()
{
    const string json = """ ...actual trace step 0 JSON... """;
    using var doc = JsonDocument.Parse(json);
    var stepData = Data.Ok(doc.RootElement.Clone());
    _ = stepData.Value;                 // forces UnwrapJsonElement
    var dataStep = stepData.As<Step>(); // exact merge.cs path

    await Assert.That(dataStep.Error).IsNull();
    await Assert.That(dataStep.Value!.Actions.Count).IsEqualTo(2);
}
```
