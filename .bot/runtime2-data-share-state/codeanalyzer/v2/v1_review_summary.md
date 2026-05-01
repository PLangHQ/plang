# Summary of v1 review on coder/v1

This is what v1 said about coder/v1 — context for v2's response review.

## v1 verdict: NEEDS WORK (4 findings, none blocking)

The architecture was clean: lifecycle audit confirmed Data is created at
the right places, `.Value` reads happen at the right places, no Data-in-Data,
copies were minimal. But four concrete simplifications/cleanups slipped past
the rewrite:

1. **`PLang/App/Data/this.cs:471–473`** — dead conditional in `AsCanonical`
   full-match branch:
   ```csharp
   if (!resolved.Success)
       return resolved;
   return resolved;
   ```
   Both branches return the same thing.

2. **`PLang/App/Data/this.cs:642–647`** — redundant transient Data
   allocation in `WrapAs<T>` for `T == IEnumerable`:
   ```csharp
   var transient = new @this("", value, _type) { Context = ctx };
   object? convertedEnum = transient.AsEnumerable();
   ```
   We construct a throwaway Data just to call its `AsEnumerable()` method.
   Defeats the zero-overhead goal exactly where it should shine — the
   cross-type enumerable wrap.

3. **`PLang/App/Variables/this.cs:71`** — dead-but-side-effecting:
   ```csharp
   if (type != null) dv.Type = type;
   ```
   `Variables.Set` is supposed to be dumb storage, but this branch mutates
   the caller's input Data. No caller in the codebase passes `(name, dv,
   type)` (verified by grep), so this is dead code that violates Rule 7
   ("relay, don't repackage").

4. **JSON-roundtrip clone duplicated three times**:
   - `Variables/this.cs:150–172` (dot-path snapshot)
   - `modules/list/add.cs:63–69` (list-entry snapshot)
   - `modules/variable/set.cs:158–168` (`SnapshotClone` private helper)

   Three identical-looking implementations of "deep-clone via JSON
   roundtrip" — extract one helper.

## Sub-findings v1 marked as minor / nit

The v1 verdicts on individual files were:

- `Data/this.cs` — NEEDS WORK (1 dead code, 1 redundant alloc, 1 nit)
- `Variables/this.cs` — NEEDS WORK (1 OBP violation, 1 duplication)
- `modules/variable/set.cs` — **CLEAN** (with one sub-finding noted via
  Variables.Set #2). Sub-findings: defensive `?? new List<>()` /
  `?? new Dict<>()` on SnapshotClone result (cast can never produce null);
  `[VariableName]` Pattern A pointer comment.
- `Debug/this.cs` — CLEAN
- `Generators/Emission/Property/Data/this.cs` — CLEAN

## Pre-existing items noted, not in scope

- `modules/list/any.cs:23` — `Value.Value` rewrap (pre-existing, not this branch).
- `Goals/Goal/Steps/this.cs:161` — `result.Value is bool` (engine-touches-.Value, pre-existing).
- `Generators/Emission/Action/this.cs:270, :296` — `__Resolve<T>` and
  `__StripPercent` Legacy helpers read `.Value`. Architect's Phase 6
  deletes them. Deferred per coder/v1.

## What v1 suggested next

> Suggested next step: back to **coder** for the four findings above, then
> to **tester** to confirm no behavioural drift.

Coder went back; the result is the commit being reviewed in v2.
