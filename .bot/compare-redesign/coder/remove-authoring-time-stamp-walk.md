# Remove the authoring-time template-stamp walk — thread `template` into `FromWire`

**Branch:** compare-redesign
**Author:** codeanalyzer (interactive, with Ingi)
**Goal:** Delete the `Authored()` / `StampTemplates()` machinery. Template stamping is
the *item's* responsibility at *read* time; the post-read tree-walk is redundant.

---

## Why

`.pr` files load as `application/plang-goal` → `GoalReadOptions` (Conversion.cs:79)
→ a `Wire` converter constructed with `template:"plang"` (Conversion.cs:82-83). That
`Wire` threads the flag as `ReadContext.Template` into every typed `Read` (Wire.cs:384),
and each item stamps **itself**: `text` ctor does `if (template != null && HasHoles)
Template = template` (text/this.cs:112). So when a `.pr` loads, step params with `%ref%`
holes are **already born with `Template="plang"`**.

That makes every post-read `StampTemplates()` / `Authored()` walk pure redundancy — it
re-derives a stamp the reader already applied. "The item itself contains `Template='plang'`
if it should be rendered." (Ingi)

The principle: **an authored read is one whose serializer (`Wire`) carries `template="plang"`.**
The item does the rest. Stamping must never be a separate tree-walk bolted on afterward.

## The one gap

Three of the four `StampTemplates()` call sites read through `GoalReadOptions`, so they're
already covered — pure deletes:
- `goal/list/this.cs:48`
- `GoalCall.cs:315`
- `GoalCall.cs:325`

The fourth — **`FromWire` (action/this.FromWire.cs:49)** — is NOT covered. It rebuilds
on-error recovery chains and compile-response actions via `FromWireShape` →
`type.Deserialize`/`Lift`, which build a bare `ReadContext(ctx)` with **no template**
(type/this.cs:411). Read-time stamping never fires there. Deleting its `StampTemplates()`
outright would silently stop `%ref%` rendering in recovery chains / rebuilt compile actions.

`FromWire` *is* an authored seam — so the fix is to thread `template:"plang"` into its read,
exactly like `GoalReadOptions` does. No item-side change; the item already knows what to do.

---

## Work

### Part A — thread `template` through the `FromWire` read (do this FIRST)

1. **`type.Deserialize`** (PLang/app/type/this.cs:396)
   Add `string? template = null`; pass it into the `ReadContext`:
   ```csharp
   internal item.@this Deserialize(object? raw, actor.context.@this? context = null, string? template = null)
   ...
   if (reader != null && reader(raw, Kind, new global::app.type.reader.ReadContext(ctx, template)) is { } read)
   ```
   (The `variable.IRawNameResolvable` branch and the no-reader `Lift` fallback stay as-is —
   a Variable is never templated, and the `Lift` fallback is for untyped/object where there's
   no reader to carry the flag. Text always has a reader, so the `%ref%`-text path is covered.)

2. **`FromWireShape`** (PLang/app/data/this.cs:790)
   Add `string? template = null`; forward it in the self-recursion (line 794) and into the
   `Deserialize` call (line 799-801). Default `null` keeps `Conversion.cs:173`'s reconstruct
   **literal** — that is correct, it's not an authored seam.

3. **`FromWire`** (PLang/app/goal/steps/step/actions/action/this.FromWire.cs:31)
   Pass `template:"plang"` into the `FromWireShape` call. Then **delete** the
   `act.StampTemplates();` at line 49.

Verify (test, not assumption): build a recovery-chain / compile-response action through
`FromWire` whose param is `%ref%` and confirm it still renders at the door. There should be
an existing recovery-chain test; if not, write one — this is the whole reason Part A exists.

### Part B — delete the now-orphaned machinery

After Part A, nothing in production reaches the stamp walk. Delete:

**PLang/app/data/this.cs**
- `Authored()` (~463)
- `StampedForm()` (~474)
- `StampEntry()` (~561)
- `RawGraphHasRef()` (~541)
- `HasTemplateRef()` (~570)
- `IsStampedTemplate` (~650) — already dead (zero callers), goes regardless

**PLang/app/type/text/this.cs**
- `Authored()` (~188) — only non-test caller was `StampedForm`. Keep `Kinded()` (used by type.cs:444).

**PLang/app/goal/steps/step/actions/action/this.Templates.cs**
- `StampTemplates()` — delete the method and the file if it's the only member.

**Call sites** (the 3 redundant ones — FromWire's was removed in Part A):
- `goal/list/this.cs:48`
- `GoalCall.cs:315`
- `GoalCall.cs:325`

### Part C — test migration (the bulk of the effort)

~40 test sites author fixtures via `.Authored()` and several via `.StampTemplates()`. These
must migrate to authoring through a **template-mode read** instead of the deleted post-walk.
Affected files (from grep):
- `PLang.Tests/Data/App/DataTests/{DataResolutionTests, AsTIdentityTests, NamePropagationTests, DataAsTResolutionTests}.cs`
- `PLang.Tests/Generator/Generator/Matrix/Resolution/ResolutionTests.cs`
- `PLang.Tests/Runtime/App/CompareRedesign/Stage2_ValueDoorTests.cs`
- `PLang.Tests/Modules/App/Modules/ui/RenderTests.cs`
- `PLang.Tests/Shared/TestAction.cs`, `PLang.Tests/Shared/Fixtures/MatrixRunner.cs`

Recommended: a single test helper that mints an authored value through a template-mode read
(the same `Wire`/`ReadContext(ctx, "plang")` path production uses), so fixtures author exactly
the way `.pr` load does — no test-only stamping shortcut. `TestAction`/`MatrixRunner` are the
natural homes; the per-test `.Authored()` calls then collapse to constructing the fixture
through that helper. **Do not** keep a test-only `Authored()` alive — that would re-introduce
the very shortcut we're deleting and let tests pass on a path production no longer has.

---

## Acceptance

- No production reference to `Authored`, `StampTemplates`, `StampedForm`, `StampEntry`,
  `RawGraphHasRef`, `HasTemplateRef`, `IsStampedTemplate` remains (grep clean).
- A `%ref%` param in a `FromWire`-rebuilt recovery chain / compile-response action renders
  at the door (new or existing test, green).
- `.pr`-loaded goal still renders `%ref%` params (existing tests green).
- A runtime-ingest `"%secret%"` (non-authored read) still prints **literally** — the
  template flag must NOT leak onto any runtime read path.
- Full C# suite green; `plang --test` from `Tests/` green (rebuild clean first — stale-binary trap).

## Notes / risk

- The security boundary here is real: stamping only on authored reads is what keeps a forged
  `%secret%` in user data literal. Part A must thread `"plang"` **only** through `FromWire`
  (authored) and leave `Conversion.cs:173`'s reconstruct at `null`. Double-check no other
  `FromWireShape`/`Deserialize` caller gets `"plang"` by accident.
- Part A and Part B must land together (or A then B same PR) — B alone breaks `FromWire`.
