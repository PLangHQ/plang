# codeanalyzer v3 — review of coder/v2

Commit reviewed: `24cba238 coder v2: nested %var% resolution in plain Data +
JsonNode conversion`. 4 files / 70 + / 22 −.

User asked for **extra weight on Pass 4 — the variable-resolution walk that
unblocks the `set %messages% = [...], type=json` builder path**. Regular
five-pass review applies on top.

---

## Summary verdict

**CLEAN**. The two-bug fix is minimal, the fixes are root-cause (not
work-arounds), and the symmetry claim — "plain `Data` and `Data<T>` resolve
nested vars by the same rule" — holds end to end. Test coverage on the
new surfaces is solid (4 + 2 tests directly exercise the bug shapes).

Three cosmetic / informational items below; none block.

---

## The var-walk path, end to end (Pass 4 deep dive)

User-traced flow: `set %messages% = [{"Role":"system","Content":"%buildGoalPrompt%"}, ...], type=json`.

| Step | What happens | What the fix touched |
|---|---|---|
| 1. `.pr` parse | `UnwrapJsonElement` / `UnwrapJsonArray` produces `List<object?>` of `Dictionary<string, object?>`. Inner `Content` is the literal string `"%buildGoalPrompt%"`. | unchanged |
| 2. Generator-emitted property reads | `Property/Data/this.cs:38` — plain `Data` slot calls `__ResolveData("Value").AsCanonical(Context)`. | unchanged |
| 3. AsCanonical sees container | `raw is List<object?>`, not string — string branch skipped. Falls into the **new** container branch: `IsWalkableContainer(raw) → WalkContainerVars(raw, ctx) → WalkList`. | **new** (`Data/this.cs:487-496`) |
| 4. WalkList iterates | Each item is a `Dictionary<string, object?>` (each `kvp.Value` is a string). `SubstitutePrimitive` recurses through the dict, hitting strings via `Variables.Resolve` / `Variables.Get(name)?.Value`. | unchanged (helper logic existed) |
| 5. set.cs reads `Value.Value` | Now sees a fresh `List<object?>` of `Dictionary<string, object?>` with `Content` = the resolved prompt text. | unchanged |
| 6. `Type=json` ⇒ `targetType = JsonNode` | `TypeConverter.TryConvertTo(walked-list, typeof(JsonNode), ctx)`. | unchanged |
| 7. Dispatch | `value is IList` → JSON serialize roundtrip → `JsonArray` of `JsonObject`. | unchanged |
| 8. `MintTyped` ⇒ `Data<JsonNode>` | Stored in `Variables`. | unchanged |
| 9. Downstream LLM handler reads `%messages%` as `Data<List<LlmMessage>>` | `As<List<LlmMessage>>` ⇒ `WrapAs` ⇒ `TryConvertTo(JsonArray, List<LlmMessage>)`. | **new path used** |
| 10. JsonArray arm | `value is JsonArray` → enumerate JsonNode elements → recursive `TryConvertTo(jsonObj, LlmMessage)`. Each element hits the `value is JsonNode` arm at line 354 → JSON roundtrip → typed `LlmMessage`. | **new** (`TypeConverter.cs:129-138, 354`) |

Every step lines up. The walked container (step 4) produces the exact
shape the JSON dispatch (step 7) needs. The JsonArray storage (step 8)
produces exactly what the JsonArray arm (step 10) consumes.

### Symmetry check — AsCanonical vs AsT_Impl

For a list-of-dicts with nested `%vars%`:

| Aspect | `AsCanonical` | `AsT_Impl` |
|---|---|---|
| Walk path | `WalkContainerVars(raw, ctx)` | `WalkContainerVars(raw, ctx)` |
| Walk produces | Fresh `List<object?>` / `Dict<string,object?>` with substituted strings | Same |
| Wrapper | Fresh transient `@this`, state aliased | `WrapAs<T>(walked, ctx)` ⇒ Rule-2/3 wrap |
| String-with-`%var%` at element level | `SubstitutePrimitive` ⇒ `Variables.Get(name)?.Value` (single-level) | Same — also `SubstitutePrimitive` |
| Recursion on a top-level `%a%` whose value is `"%b%"` | N/A — top-level strings handled by the string branch above the container branch (which only single-level resolves; the partial-interpolation `Resolve` is also single-pass) | The string branch recurses via `resolved.AsT_Impl<T>(resolved.Value, ctx)` — multi-level under cycle/depth guards |

Symmetry holds for the container path (the surface this fix is about).
Top-level string resolution remains asymmetric (AsT_Impl recurses,
AsCanonical does not), which is **pre-existing** — not introduced or
worsened by this commit.

### JsonObject / JsonArray dispatch — edge audit

- **`targetType == JsonNode`, value is `List<object?>`** — line 354 catches
  via `value is IList` → roundtrip → `JsonArray`. ✓
- **`targetType == LlmMessage`, value is `JsonObject`** — line 354 catches
  via `value is JsonNode` → roundtrip → `LlmMessage`. ✓
- **`targetType == List<LlmMessage>`, value is `JsonArray`** — new arm at
  `TypeConverter.cs:129-138` enumerates → recursive convert. ✓
- **`targetType == List<LlmMessage>`, value is `JsonObject`** — falls
  through the JsonArray arm; reaches the recursive `TryConvertTo` at line
  176-181 → converts the single JsonObject to LlmMessage → wraps in
  one-element list. Edge but consistent with the existing single-element
  list-wrap pattern.
- **`targetType == LlmMessage`, value is `JsonArray`** — `GetListElementType`
  returns null (LlmMessage isn't a list), so the list block is skipped;
  hits line 354's roundtrip; STJ fails to deserialize an array into a
  scalar object → returns `DeserializationFailed` error. Correct.
- **JsonArray with null entries** — `TryConvertTo(null, T)` returns
  `(null, null)` for ref types and `(default(T), null)` for value types.
  The `if (convertedElem != null) targetList.Add(convertedElem);` guard
  silently drops nulls for ref types but adds default values for value
  types (e.g. `0` for int). Same behavior as the pre-existing
  JsonElement-array arm — pre-existing pattern, not v3 finding.

### Action-template carve-out

`AsT_Impl` skips the walk for `IsActionDestination(typeof(T))`. Plain
`Data` has no T to check — does AsCanonical need a parallel carve-out?

**No.** Action templates flow only through typed `Data<Action>` /
`Data<List<Action>>` slots (verified by grepping `partial Data.@this`
across `App/modules/`; every plain-`Data` slot holds a comparison operand,
storage value, or collection iterable — never an action template). And
`SubstitutePrimitive` has explicit guards (`Data/this.cs:734-736`) that
return Action / `IEnumerable<Action>` / `Data` values unchanged when
encountered inside a walked container. Action templates are protected at
the per-element level even on the AsCanonical path.

### Cycle-protection on container path

The container walk has no `_resolvingValues` HashSet — cycle protection
is string-only on `AsT_Impl`'s top-level branch. But `WalkList` /
`WalkDict` only single-level-resolve their string elements (via
`Variables.Resolve` / `Variables.Get(name)?.Value` — neither recurses).
A `%a%` element whose value is `"%b%"` becomes the literal string `"%b%"`
in the walked output; no recursion, no cycle risk.

Pre-existing. Not introduced by this commit.

---

## Per-file findings

### `PLang/App/Data/this.cs`

#### OBP Violations
*None.* `IsWalkableContainer` and `WalkContainerVars` are private statics
inside the partial class. The shape-check semantics belong to `Data`
(they decide what is and isn't a "container with possible nested vars"
per plang's value model).

#### Simplifications

1. **Lines 473-480 and 487-496: duplicate "build transient + alias state"
   pattern in `AsCanonical`.** Both the partial-interpolation branch and
   the new container-walk branch construct a fresh `@this` and re-alias
   `Properties` + the three event lists from `this`. Six lines duplicated
   verbatim except for the `value` argument. Worth a private helper
   parallel to `ConstructWrap<T>`:

   ```csharp
   private @this BuildTransient(object? value, Actor.Context.@this ctx)
   {
       var transient = new @this(Name, value, _type, Parent) { Context = ctx };
       transient.Properties = Properties;
       transient.OnCreate   = OnCreate;
       transient.OnChange   = OnChange;
       transient.OnDelete   = OnDelete;
       return transient;
   }
   ```

   Each callsite collapses to `return BuildTransient(interpolated, ctx);`
   / `return BuildTransient(walked, ctx);`. **Cosmetic — would not block.**

#### Readability

1. **Lines 505-506 and 513-518: `IsWalkableContainer` and the leading
   shape checks of `WalkContainerVars` mirror each other.** Either
   merge into `TryWalkContainerVars(raw, ctx, out walked) → bool`, or
   tighten the helper signatures to `IList<object?>` / `IDictionary<…>`
   overloads (then `WalkContainerVars(object?)` becomes unnecessary
   since callers already gate via `IsWalkableContainer`). **Cosmetic —
   the current shape is fine; mention only because the duplication is
   visible.**

#### Verdict: **CLEAN** (one simplification, one readability — both cosmetic)

---

### `PLang/App/Utils/TypeConverter.cs`

#### OBP Violations
*None.* Pure dispatch addition — `JsonNode` slotted alongside
`IDictionary<string, object?>`, `JsonElement`, `IList`. The new
`JsonArray` element-iteration arm sits next to its `JsonElement`-array
sibling (line 114) — placement is symmetric.

#### Simplifications
*None.* The `JsonArray` arm is parallel to the existing `JsonElement`
arm; combining them into a single iteration helper would require a
common adapter for `IEnumerable<JsonElement>` vs `IEnumerable<JsonNode?>`,
which doesn't exist. Two arms is the right shape.

#### Readability

1. **Line 129-138: `JsonArray` arm silently drops failed element
   conversions** — same as the pre-existing `JsonElement`-array arm at
   line 117-123. The regular `IList` arm at line 140-167 collects
   per-element errors and returns them in an `ErrorChain`. The two
   JSON-source arms diverge from the IList arm here — consistent within
   the JSON-source family but inconsistent across all three list paths.

   Not asking for a fix — making JSON arms collect errors would be a
   broader behavioral change (and the JSON-source values originate from
   a successful upstream serialize, so per-element failures point at
   target-shape mismatches that already surface elsewhere). **Flag for
   awareness.**

#### Verdict: **CLEAN**

---

### `PLang.Tests/App/DataTests/AsTIdentityTests.cs`

#### OBP / Simplifications / Readability
*None.* The four new tests (Rule 4c-4f) follow the existing pattern,
with descriptive comments explaining what each pins.

#### Test gap (Pass 5 deletion test)

1. **No test pins state-aliasing on the container-walk transient.** Rules
   4c, 4d, 4e, 4f assert on `ReferenceEquals(canonical, paramData)`
   (false) and on resolved value contents — they do **not** assert that
   `canonical.Properties`, `canonical.OnCreate`, `canonical.OnChange`,
   or `canonical.OnDelete` are aliased from `paramData`'s.

   If the four `transient.Properties = Properties; transient.OnCreate =
   ...` lines (`Data/this.cs:491-494`) were deleted, the existing tests
   would still pass green — yet the contract Rule 4c claims to pin
   ("AsCanonical… returning a fresh Data… with state aliased from `this`")
   would be silently broken.

   The partial-interpolation branch at line 474-480 has the same lines
   but for partial-interpolation; I don't see a test for that branch's
   aliasing either, but it's pre-existing.

   **Recommend: add a single test on the container branch that subscribes
   `paramData.OnChange` and reads `canonical.OnChange` (ref-equal), or
   sets `paramData.Properties["x"] = "y"` and reads it via `canonical`.**
   One test pins both branches by analogy. Cheap and durable.

#### Verdict: **CLEAN** (one test gap)

---

### `PLang.Tests/App/Engine/Utility/TypeMappingDictConversionTests.cs`

#### OBP / Simplifications / Readability
*None.* The two new tests pin the JsonObject and JsonArray arms with
clear comments.

#### Verdict: **CLEAN**

---

## Verdict summary

| File | v3 Verdict |
|---|---|
| `PLang/App/Data/this.cs` | CLEAN (1 simplification + 1 readability, both cosmetic) |
| `PLang/App/Utils/TypeConverter.cs` | CLEAN (1 informational on error handling) |
| `PLang.Tests/App/DataTests/AsTIdentityTests.cs` | CLEAN (1 test gap) |
| `PLang.Tests/App/Engine/Utility/TypeMappingDictConversionTests.cs` | CLEAN |

**Overall: CLEAN.**

The fix correctly identifies and closes both root causes (asymmetric
nested-var walking, JsonNode dispatch gap). Symmetry between
`AsCanonical` and `AsT_Impl` for the container path is achieved
through a single helper (`WalkContainerVars`), and the surrounding
cycle/Action-template carve-outs continue to behave correctly under
inspection.

The cosmetic items (transient-builder dedup, IsWalkableContainer +
WalkContainerVars merge, Pass 5 aliasing test) are 5-minute followups
that can ride the next touch of these files.

**Carryovers from prior versions still un-fixed** (out of scope for
this commit, noting for completeness):
- `set.cs:117-118` `?? new List<>()` / `?? new Dict<>()` defensive
  fallbacks (unreachable).
- `set.cs:117-118` and `list/add.cs:56` `global::App.Data.@this.SnapshotClone(...)`
  qualification (unnecessary `global::`).

---

## Suggested next step

**tester** — to (a) verify the 6 new tests still pass green, (b) consider
adding the one missing aliasing test on the container branch, and (c)
re-run the LLM-builder smoke path now that `Start.goal` builds past the
NRE (per coder/v2's verification note). If tester passes, this is ready
for **auditor** and merge.
