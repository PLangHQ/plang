# codeanalyzer v3 — review of coder/v2 (24cba238)

## What this is

Coder ran `plang --test`, found the LLM builder NRE on every build, traced
it to two bugs in the value-resolution path, and fixed both. This review
verifies the fix is sound, with extra emphasis on the variable-resolution
walk that unblocks the `set %messages% = [...], type=json` path through
to a strongly-typed LLM handler.

The two bugs:

1. `AsCanonical` (the resolution entry point for plain-`Data` slots) only
   walked **strings** for `%vars%`. Lists and dicts loaded from `.pr`
   passed through unchanged, so `%buildGoalPrompt%` strings inside a
   list-of-dicts stayed literal. `AsT_Impl` did walk them — silent
   asymmetry between the plain-`Data` and `Data<T>` paths.
2. `TypeConverter.TryConvertTo` had no `JsonNode` arm in the
   complex-source dispatch. `JsonObject` (which implements
   `IDictionary<string, JsonNode?>`, NOT `IDictionary<string, object?>`)
   slipped past every dispatch arm and landed at `TypeMismatch`. So
   `set ... type=json` minted a `Data<JsonNode>` that downstream
   strongly-typed handlers couldn't read.

## What was done in this review

**Verdict: CLEAN — pass.**

Five-pass review on 4 files (2 production + 2 test). Pass 4 (behavioral)
got the heavy weight per user request — full end-to-end trace of the
json→LlmMessage flow.

### Findings

1. **Simplification (cosmetic)** — `AsCanonical`'s partial-interpolation
   and container-walk branches duplicate "build transient + alias state"
   six lines each. Worth a private `BuildTransient(value, ctx)` parallel
   to `ConstructWrap<T>`.
2. **Readability (cosmetic)** — `IsWalkableContainer` and the leading
   shape-checks inside `WalkContainerVars` mirror each other. Either
   merge into `TryWalkContainerVars(out walked) → bool`, or tighten the
   helper to typed overloads. Current shape is fine.
3. **Test gap (durable)** — None of the four new `AsTIdentityTests`
   `Rule 4c-4f` assert state-aliasing (`Properties` / `OnCreate` /
   `OnChange` / `OnDelete`) on the container-walk transient. If the
   four aliasing lines (`Data/this.cs:491-494`) were deleted, the
   existing tests would still pass green. One additional `ReferenceEquals`
   assertion would pin the contract durably.
4. **Informational** — The new `JsonArray` arm at
   `TypeConverter.cs:129-138` silently drops failed element conversions,
   matching the pre-existing `JsonElement`-array arm at line 117. The
   regular `IList` arm collects errors. Consistent within the JSON-source
   family; flag only.

### What I verified end-to-end (Pass 4 deep-dive)

The user-requested trace, summarized:

| Stage | Verified |
|---|---|
| `.pr` load → typed CLR shapes | unchanged |
| Generator emits `AsCanonical(Context)` for plain `Data` slots | unchanged |
| `AsCanonical` container branch → walks → fresh transient with resolved strings | **fix verified** |
| `set.cs` reads `Value.Value` ⇒ resolved list | unchanged |
| `TryConvertTo(walked-list, JsonNode)` ⇒ `JsonArray` of `JsonObject` via line-354 dispatch | unchanged |
| Stored as `Data<JsonNode>` | unchanged |
| Downstream `As<List<LlmMessage>>` ⇒ `WrapAs` ⇒ `TryConvertTo(JsonArray, List<LlmMessage>)` | **fix verified** |
| New `JsonArray` arm enumerates → recursive `TryConvertTo(JsonNode-element, LlmMessage)` ⇒ line-354 JsonNode roundtrip ⇒ typed LlmMessage | **fix verified** |

Symmetry between `AsCanonical` and `AsT_Impl` for the container path
holds. Action-template carve-out works at the per-element level via
`SubstitutePrimitive`'s guards. Cycle protection is unaffected (container
walk is single-level; pre-existing behavior).

## Code example — the fix in one diff (paraphrased)

```csharp
// Data/this.cs — AsCanonical, AFTER the string branch, BEFORE the literal-return:
if (ctx != null && IsWalkableContainer(raw))
{
    var walked = WalkContainerVars(raw, ctx);
    var transient = new @this(Name, walked, _type, Parent) { Context = ctx };
    transient.Properties = Properties;
    transient.OnCreate   = OnCreate;
    transient.OnChange   = OnChange;
    transient.OnDelete   = OnDelete;
    return transient;
}

// TypeConverter.cs — JsonNode added to the complex-source dispatch:
if (value is IDictionary<string, object?>
         or System.Text.Json.JsonElement
         or JsonNode                          // <-- new
         or System.Collections.IList)
{
    /* serialize → deserialize to targetType */
}

// TypeConverter.cs — parallel JsonArray element-iteration arm:
if (value is JsonArray jArr)
{
    var targetList = (System.Collections.IList)Activator.CreateInstance(targetType)!;
    foreach (var elem in jArr)
    {
        var (convertedElem, _) = TryConvertTo(elem, listElementType, context);
        if (convertedElem != null) targetList.Add(convertedElem);
    }
    return (targetList, null);
}
```

Six new tests (4 in `AsTIdentityTests`, 2 in `TypeMappingDictConversionTests`)
pin both surfaces.

## Files

- `.bot/runtime2-data-share-state/codeanalyzer/v3/plan.md` — five-pass plan
  with extra weight on Pass 4 var-walk trace.
- `.bot/runtime2-data-share-state/codeanalyzer/v3/v2_review_summary.md` —
  one-page recap of the v2 PASS (what was already cleared coming in).
- `.bot/runtime2-data-share-state/codeanalyzer/v3/result.md` — full
  per-file findings + the end-to-end var-walk trace.
- `.bot/runtime2-data-share-state/codeanalyzer/v3/verdict.json` — pass.

## Suggested next step

**tester** — verify the 6 new tests pass green, optionally add the
one missing state-aliasing test on the container-walk transient, and
re-run the LLM-builder smoke path now that `Start.goal` builds past the
NRE. If tester passes → **auditor** → merge.
