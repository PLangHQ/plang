# codeanalyzer v4 — findings on coder/v7

Five-pass review of the `Variable` + `IRawNameResolvable` migration. The
changes are clean, additive, and well-tested. Findings are limited to
minor cleanups and a few latent contract questions — no production
defects, no OBP violations.

---

## PLang/App/Variables/Variable.cs

### OBP Compliance
**CLEAN.** Variable is a peer record alongside `App.Variables.@this` (the
store) — consistent with how `App/Errors/` carries multiple peer types
(IError, ServiceError, ValidationError, …). The folder still has its
`@this` in `this.cs`. Variable.cs is a value-object peer, not a competing
primary class.

### Simplifications
None. The record + helper-ctor + `Resolve` + `ToString` shape is minimal.

### Readability
1. **Line 26 — single-arg ctor**. `Variable(string name) : this(name ?? "", name ?? "", false)`
   stores `name` in BOTH `Name` and `RawValue`. Tests use this; the comment
   explains it's "equivalent to a bare-name slot." Worth a single-line
   warning that `RawValue` is **not** the wire form here — the field goes
   to `name`, not `%name%` — so a producer that constructs Variables
   directly may see `WasPercentWrapped=false` and `RawValue=name` rather
   than the original wire string. **NIT** — taste.

### Behavioral
2. **Lines 49-50 — WasPercentWrapped guard**. `raw.Length >= 2 && raw[0] == '%' && raw[^1] == '%'`
   would be true for `"%%"` (length 2, both ends `%`) and produce
   `Name = ""`, `WasPercentWrapped = true`. No consumer cares today
   (Name="" Variable just leaks into Variables.Get/Set as the empty key,
   which gets `CleanName`'d). Not worth fixing until a use case appears.
   **No finding.**

### Verdict: CLEAN

---

## PLang/App/Variables/IRawNameResolvable.cs

### OBP Compliance
**CLEAN.** Empty marker interface, peer to Variable.

### Verdict: CLEAN

---

## PLang/App/Data/this.cs

### Behavioral Reasoning
3. **Lines 549-562 — `IRawNameResolvable` carve-out, silent fallthrough on
   missing `Resolve`**. The carve-out's contract is "T : IRawNameResolvable
   means bypass `%var%` substitution and dispatch to `T.Resolve(string,
   Context.@this)`." But if a future T implements the marker WITHOUT
   declaring the static `Resolve` method, `resolveMethod == null`, the
   block silently exits, and the next branch (`%var%` substitution) runs —
   exactly what the marker was supposed to prevent.

   Today only Variable implements the marker, and it has Resolve, so this
   can't bite. But it's a latent contract trap for future contributors.
   Two fixes possible:
   - **Throw on null** — if `T : IRawNameResolvable` but no `Resolve`, the
     contract is broken; a `MissingMethodException` would surface it.
   - **Expose Resolve as an interface method** instead of relying on
     reflection convention.

   **NIT** — defensive, only matters if a second IRawNameResolvable type
   ever lands.

### Simplifications
4. **Lines 549-562 vs 632-644 — duplicated reflection block**. The new
   carve-out and the existing Path-style static-Resolve branch are
   structurally near-identical:
   - Same `ResolveMethodCache.GetOrAdd` lookup with `BindingFlags.Public |
     Static`, `(string, Context.@this)` signature.
   - Same `Invoke(null, new object[] { raw, ctx })`.
   - Same `is T result` cast → `ConstructWrap<T>(result, ctx)`.

   Could collapse into a private helper:
   ```csharp
   private @this<T>? TryStaticResolve<T>(string raw, Actor.Context.@this ctx)
   {
       var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
           t.GetMethod("Resolve",
               BindingFlags.Public | BindingFlags.Static,
               null, new[] { typeof(string), typeof(Actor.Context.@this) }, null));
       if (resolveMethod == null) return null;
       var resolvedObj = resolveMethod.Invoke(null, new object[] { raw, ctx });
       return resolvedObj is T result ? ConstructWrap<T>(result, ctx) : null;
   }
   ```

   Each call site collapses to:
   ```csharp
   // Carve-out
   if (raw is string raw1 && ctx != null
       && typeof(IRawNameResolvable).IsAssignableFrom(typeof(T)))
   {
       var r = TryStaticResolve<T>(raw1, ctx);
       if (r != null) return r;
   }
   // Path-style
   if (raw is string raw2 && ctx != null && raw is not T)
   {
       var r = TryStaticResolve<T>(raw2, ctx);
       if (r != null) return r;
   }
   ```

   Saves ~12 lines, makes the two branches' shape symmetry explicit, and
   keeps the cache shared by construction. **MINOR.**

### Readability
5. **Carve-out comment doesn't reference the existing static-Resolve path**.
   A reader of lines 544-562 sees an isolated reflection block; only by
   reading line 632-644 do they realize there's a sibling path doing
   nearly the same thing for non-marker Ts. A one-line cross-reference
   ("mirror of line 632 path; runs first because IRawNameResolvable Ts must
   bypass `%var%` substitution") would help. **NIT.**

### Pass 5 — Deletion
- Delete carve-out lines 544-562 → `SlotData_PercentWrapped_AsVariable_*`
  tests fail (NRE on `.Value!.Name` because TryFullVarMatch returns
  uninitialized Data<Variable> with null Variable Value). **Earns its
  place.**

### Verdict: CLEAN (one MINOR DRY, two NITs)

---

## PLang/App/Modules/this.cs (catalog builder)

### Simplifications
6. **Lines 425-432 vs ExampleRenderer.cs lines 166-177 — duplicated
   "is this a variable-name slot?" predicate**. Two implementations of the
   same logic:
   - `IsVariableNameSlot(Type propType)` — explicit method, used by
     `Describe()`.
   - `LookupParamTypeName` (ExampleRenderer) — inline check via
     `UnwrapDataAndNullable` + `IRawNameResolvable.IsAssignableFrom`.

   Both unwrap `Nullable<>` + `Data<T>` to get inner T, then test the
   marker. A single helper next to `IRawNameResolvable.cs`
   (`public static bool IsSlotShape(System.Type)`) would eliminate the
   drift risk if the predicate ever needs to evolve (e.g., to also accept
   `Data<List<Variable>>`). **MINOR DRY.**

### Verdict: CLEAN (one MINOR DRY)

---

## PLang/App/Catalog/ExampleRenderer.cs

### Verdict: CLEAN — see Finding 6 above (shared with App/Modules/this.cs)

---

## PLang/App/modules/loop/foreach.cs

### Readability
7. **Line 28 vs line 42 — inconsistent nullable-Variable handling**.
   - Line 28: `var variableName = ItemName?.Value?.Name ?? "item";` — full
     nullable chain with fallback.
   - Line 42: `Context.Variables.Set(KeyName.Value, key);` (after
     `KeyName != null` check) — relies on the `Variable→string` implicit
     operator without a `Name` traversal or fallback.

   In practice both work today: when KeyName is set, `As<Variable>(ctx)`
   always produces a non-null Variable (Variable.Resolve never returns
   null). But the two patterns disagree on whether `.Value` can be null
   after a non-null wrapper check. Pick one. The line-28 pattern is
   safer; the line-42 pattern is shorter. Either is fine, but the
   asymmetry suggests the author was uncertain. **NIT.**

### Verdict: CLEAN (one NIT)

---

## PLang/App/modules/list/*.cs (12 handlers) + variable/*.cs (4 handlers)

All 16 migrated handlers follow one of two patterns:
- **Pattern A (write/mutate):** `Context.Variables.Set(X.Value, …)` —
  list/add, list/remove, list/reverse, list/set, list/sort, list/get
  (read), variable/set, variable/remove, variable/exists.
- **Pattern B (read by name):** `Context.Variables.Get(X.Value)` — same
  list/* handlers, variable/get.

The implicit `Variable→string` conversion fires uniformly. Use sites are
short, readable, and consistent.

### Verdict: CLEAN

---

## PLang.Generators/Discovery/this.cs

### OBP Compliance
**CLEAN.** Two-rule gate (Data<T> / Provider) is enforced by
`IsValidActionProperty` (line 125-138). The PLNG001 message format
correctly reflects the post-v5 contract.

### Behavioral
8. **Line 198-200 — emit-nothing guard for unmatched property type**.
   `BuildProperty` returns `(null, implementsIEvent)` when the property is
   neither `Provider`-attributed nor `App.Data.@this[<T>]`. The comment
   says "PLNG001 has already flagged this property; emit nothing so the
   build error surfaces without a follow-on NRE elsewhere." Good
   defensive design — caught by the diagnostics loop in
   `GetActionClassInfo` line 90-101 (which runs the SAME predicate via
   `IsValidActionProperty`). The two predicates are kept in sync today.

   **No finding** — but a reader has to follow two predicates to verify
   this. A comment cross-reference at line 137 ("must agree with
   `BuildProperty`'s leaf check") would help. **NIT.**

### Verdict: CLEAN

---

## PLang.Generators/Emission/Action/this.cs

### Readability
9. **Line 237-240 — comment refers to deleted machinery accurately**.
   Comment correctly states "Legacy scalar/[VariableName] emission is
   gone — Variable-name slots are Data<Variable> and route through the
   Data emitter's __ResolveData." Clean.

### Verdict: CLEAN

---

## PLang.Generators/Emission/Property/this.cs

### Simplifications
10. **Line 7 — stale comment**. The doc-block on the abstract base
    declares: "Concrete leaves (DataProperty, ProviderProperty,
    LegacyScalarProperty) carry the per-property metadata they need…".
    The Legacy folder is deleted; only DataProperty and ProviderProperty
    remain. Update to: "Concrete leaves (DataProperty, ProviderProperty)
    carry the per-property metadata…". **MINOR.**

### Verdict: NEEDS WORK (one stale comment — trivial fix)

---

## PLang.Generators/Emission/Property/Data/this.cs

### Verdict: CLEAN

---

## PLang.Generators/Emission/Property/Provider/this.cs

### Verdict: CLEAN

---

## Cross-file: orphaned references

11. **`PLang/App/Utils/ReservedKeywords.cs:53` — `public static readonly string VariableName = "!VariableName"`** is unused
    after the migration (no callers found in `PLang/`, `PLang.Tests/`).
    This was likely orphaned before v7 (the migration didn't introduce
    it), so it's not strictly a v7 finding — but the migration is the
    reason it has no consumers anymore. **NIT** — out of v7 scope, worth
    a follow-up cleanup pass.

12. **`PLang.Tests/App/Modules/builder/GetActionsTests.cs:72`** — comment
    says "variable.set has a Name property with `[VariableName]`". The
    attribute is gone; the property is `Data<Variable>`. Test still
    passes because it asserts the `%var%` advertisement. Update comment
    to: "variable.set has a Name property with `Data<Variable>` (renders
    as `%var% string`)". **NIT.**

13. **`PLang.Tests/App/DataTests/VariableSetNameResolutionTests.cs`**
    documents the bare-name regression that made the original migration
    declined; the file is now describing history rather than a current
    contract. The header comment correctly notes Ingi declined the
    migration (2026-05-01) and that the tests are retained as
    documentation. Could be deleted or merged into VariableResolveTests.cs.
    **NIT** — not breaking; could be revisited at the next cleanup pass.

---

## Pass 5 — Deletion Test summary

| Code | Delete impact | Verdict |
|---|---|---|
| `Data.AsT_Impl` carve-out (lines 544-562) | `SlotData_PercentWrapped_AsVariable_*` tests NRE | **Earns place.** |
| `Variable.Resolve` static method | Carve-out reflection returns null, fallthrough → NRE | **Earns place.** |
| `Variable(string name)` ctor | Test composition `new Variable("myList")` breaks | **Earns place.** |
| `IRawNameResolvable` interface | Discriminator gone, all Variables go through %var% substitution → migration breaks | **Earns place.** |
| `WasPercentWrapped` field | No consumer today; documented as "future build-time validators" | **Borderline NIT** — keep for now (cheap, captures info that's expensive to recover later) |
| `Property/this.cs:7` LegacyScalarProperty mention | Comment-only, nothing breaks | **Delete (Finding 10).** |

---

## Summary by severity

- **MAJOR:** 0
- **MINOR:** 3 — Findings 4 (DRY reflection), 6 (DRY predicate), 10 (stale comment).
- **NIT:** 7 — Findings 1, 3, 5, 7, 8, 11, 12, 13.

Net: the v7 migration is clean. The two `MINOR` DRY findings are
optional simplifications; Finding 10 is a one-line comment update. None
block.

## Recommendation

**PASS** — Ready for tester. The DRY simplifications are nice-to-have
and could fold into a future pass.
