# docs v1 — runtime2-data-share-state

## What this is

The first docs pass on `runtime2-data-share-state`, after auditor/v2 PASS on coder/v3. The branch landed phases 1–4 of architect/v1's data identity-preservation plan plus three review-response rounds. My job: fill documentation gaps, decide on the CLAUDE.md proposal, gate the merge.

The branch is a **structural change with thin user-visible surface**. The internal contract changed substantially (cross-type `As<T>` views share state; `Variables.Set` aliases events on replacement; `variable.set` is the sole user-visible binding-mint site; nested-`%var%` resolution unified between plain Data and `Data<T>`; JsonNode/JsonArray dispatch added to `TypeConverter`). The user-visible API got two small but real behavioural notes (snapshot-clone semantics for `set %x% = %y%`, atomic strings in `foreach`).

## What was done

### CLAUDE.md proposal: applied (1)

Coder/v2's "Running plang Tests" proposal targeted root `/CLAUDE.md` and is genuinely canonical (any future bot running tests from project root trips the same `tests/` lowercase + `.bot/` discovery noise). Folded in as a new `## Running plang Tests` subsection between `## Build` and `## Debugging`.

### Documentation gaps filled

1. **`Documentation/v0.2/data-generic-design.md`** — appended a new section *"Identity preservation — `As<T>` wrap rules + `AsCanonical`"*. The pre-existing "always allocate" sketch in section 7 was the v4 design; the new appended section is the shipped contract.

2. **`Documentation/v0.2/variables.md`** — replaced the API-surface block (had `Put(Data)` which doesn't exist in code), rewrote the behaviour rules to reflect the events-follow-name / Properties-stay-with-Data contract, fixed the stale `*__Generated` reference at line 152.

3. **`Documentation/v0.2/architecture.md`** — corrected the Variables snippet that listed `Variables.Put(data)`; added a one-paragraph pointer to the new identity contract.

4. **`Documentation/v0.2/debug.md`** — rewrote "Events are copied when a variable is replaced" to "Events are aliased" with the precise contract (subscribers added later are visible from any prior alias holding the shared list ref).

5. **`Documentation/v0.2/good_to_know.md`** — added six cross-cutting entries:
   - Data identity preservation (the four `As<T>` wrap rules)
   - `AsCanonical` (plain Data slots)
   - `Variables.Set` (events follow name, Properties stay with Data)
   - `variable.set` is the sole binding-mint site
   - String-not-iterable rule (`IsPlangIterable` / `IsPlangAssignable`)
   - JsonNode / JsonArray dispatch in `TypeConverter`

6. **`docs/modules/loop.md`** — *"Strings are atomic"* subsection. User-visible: `foreach %s%` over a string runs once.

7. **`docs/modules/variable.md`** — *"Lists and dictionaries are independent copies"* note under `set default`. User-visible: `set %x% = %y%` for List/Dict snapshot-clones.

### What I did NOT touch

- **Stale `[VariableName]` references** anywhere — Phases 5b/5c/6 are deferred. The 25 handlers under `App/modules/list/`, `App/modules/loop/`, `App/modules/variable/` still use `[VariableName]`; the existing doc references at `architecture.md:265`, `good_to_know.md:614,616,618,620`, `action-catalog.md:66,171,174`, root `CLAUDE.md:25` are all still accurate. Editing them would be premature.
- **`Documentation/Runtime2/`** — deprecated per user's auto-memory note; canonical home is `Documentation/v0.2/`.
- **PLang `.goal` examples** — tester's responsibility; deferred-stub `Modules/Loop/Foreach/StringNotIterable.test.goal2` already exists.
- **Auditor F2/F3/F4 carryovers** — code cleanups, not docs work.

## Code example — the load-bearing aliasing

This is what the new `data-generic-design.md` and `good_to_know.md` entries make visible. Future devs touching `Data.WrapAs<T>` or `Variables.Set` need to see this contract or they'll silently break debug watches.

```csharp
// Source: Data<List<int>> with Properties + subscribers under the name "nums".
var source = new Data.@this<List<int>>("nums", new List<int> { 1, 2, 3 });
source.Properties.Set("annot", "labeled");
source.OnChange.Add((o, n) => Log(n));
ctx.Variables.Set(source);

// As<IEnumerable> — variance fast path. New wrapper instance, but state aliased.
var wrapped = source.As<IEnumerable>(ctx);
ReferenceEquals(wrapped.Value,      source.Value)      == true;  // .Value ref shared
ReferenceEquals(wrapped.Properties, source.Properties) == true;  // metadata ref shared
ReferenceEquals(wrapped.OnChange,   source.OnChange)   == true;  // subscribers ref shared

// Same-type fast path — returns source as-is, no allocation.
ReferenceEquals(source, source.As<List<int>>(ctx)) == true;

// Variables.Set replacement — events alias from prev onto new binding.
var replacement = new Data.@this<List<int>>("nums", new List<int> { 4, 5 });
ctx.Variables.Set(replacement);
ReferenceEquals(replacement.OnChange, source.OnChange) == true;
// → handlers fire on every re-binding of %nums%; debug watch sees them all.
```

Removing any of the four state-alias assignments in `Data.ConstructWrap<T>` (or the three on `Variables.Set` replacement) is a silent regression.

## Verdict

**PASS.** Build clean post-edits (0 errors, 364 pre-existing nullability warnings). Ready to merge.

## Hand-off

- **Security review** still not on file for this branch — auditor v1 and v2 both flagged it as the recommended next gate. JSON-roundtrip expansions (TypeConverter JsonNode dispatch, list.add SnapshotClone, variable.set MintTyped's deep-clone of List/Dict) are the new attack surface to consider.
- **Auditor F2/F3/F4** — minor code cleanups, can ride a future PR.
