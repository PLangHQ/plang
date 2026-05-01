# v5 — Variable as a First-Class Class, [VariableName] Replaced by Data&lt;Variable&gt;

## What this is

The `runtime2-variablename-migration` branch attempted to delete `[VariableName]`
by relying on `Data.As<T>(ctx).Name` propagating the canonical variable name out
of slot resolution. Coder verified the mechanism works for `value="%x%"` but
discovered a robustness regression for the bare LLM emission `value="x"` — the
slot key "Name" silently leaks. Decision: keep `[VariableName]` permanent.

But the asymmetry between `Data<T>` (a class with `.Value`, `.Name`, `.Properties`,
`.Error`, future `.Signature`) and `[VariableName] string` (a primitive marked by
an attribute) is unsatisfying — and it forces a third API shape next to `Data<T>`
and `[Provider] T`, plus the corresponding source-gen branch.

v5 dissolves the asymmetry without reverting to the variablename-migration's
brittle path. **Introduce `Variable` as a small AppResolvable record. Replace
every `[VariableName] string Foo` with `Data<Variable> Foo`.** No new parameter
shape; uses the existing `Data<T>` machinery. Provenance (signing) lives on the
`Data<Variable>` wrapper, exactly as it would for any other `Data<T>` — Variable
itself is just a value, no different from `string` in that regard. The 22
affected handlers shift their property declarations and use sites; everything in
source gen that exists for the `[VariableName]` carve-out is deleted.

This is the v4-stated goal ("delete `[VariableName]`, every property is `Data<T>`")
landing through a different mechanism than v4 imagined — instead of leaning on
`As<T>.Name` propagation, we make the variable name a typed payload that
`As<T>` resolves via the type's own static method.

## Why this approach doesn't repeat the variablename-migration regression

The previous attempt's failure mode: `Data<string>` for a `value="x"` slot resolves
through `As<string>` → no `%`, no `TryFullVarMatch`, `.Name` stays as the slot key
"Name". Silent miswrite when handler does `Variables.Set(Name.Name, ...)`.

In v5, `Data<Variable>` for a `value="x"` slot resolves through `As<Variable>` →
the existing `As<T>` dispatch at `/PLang/App/Data/this.cs:612-624` finds
`Variable.Resolve(string, Context.@this)` and calls it with the raw string `"x"`.
`Variable.Resolve` strips `%` if present (or accepts the bare form unchanged) and
returns `Variable { Name = "x", RawValue = "x", WasPercentWrapped = false }`. Both
`%x%` and `x` produce `Name = "x"` — exactly the symmetry today's `__StripPercent`
provides.

The bare-name footgun isn't deleted, it's surfaced: `WasPercentWrapped = false`
makes the LLM emission visible to handlers (or to a future build-time validator)
rather than being silently normalized away.

## The Variable class

Location: `/PLang/App/Variables/Variable.cs` (sibling to the existing
`Variables/this.cs` collection class).

```csharp
namespace App.Variables;

public sealed record Variable(
    string Name,                // canonical: "x"
    string RawValue,            // what the .pr held: "%x%" or "x" or ""
    bool WasPercentWrapped)     // false when LLM emitted bare name; future validator hook
{
    public static implicit operator string(Variable v) => v.Name;

    public static Variable Resolve(string raw, Actor.Context.@this ctx)
    {
        if (string.IsNullOrEmpty(raw))
            return new Variable("", raw ?? "", false);

        // Both forms collapse to the canonical name, matching today's __StripPercent.
        var trimmed = raw.Trim('%');
        var wasPercentWrapped = raw.Length >= 2 && raw[0] == '%' && raw[^1] == '%';
        return new Variable(trimmed, raw, wasPercentWrapped);
    }
}
```

Notes on the shape:

- **Record, not class** — small value-shape, equality by member is the right
  default. Subscribers/Properties on `Data<Variable>` are carried by the wrapper.
- **Implicit string conversion** — handler code reads `Name.Value` (returns a
  `Variable`) and at any `string`-expecting boundary the conversion fires. The
  one gotcha (`var foo = Name.Value` infers `Variable` not `string`) is
  documented in `good_to_know.md` and is uncommon in handler code.
- **No `Signature` field on Variable.** Provenance lives on the wrapper —
  `Data<Variable>.Signature` when signing lands, just as `Data<string>.Signature`
  would carry the signature for a string value. `Variable` is a value, not a
  wrapper; it doesn't carry provenance any more than `string` does.
- **No Context reference** — Variable doesn't expose "the live value of the
  variable I name." Handlers that want that still call `Context.Variables.Get(Name)`
  themselves. Keeps Variable a value-shape rather than a smart object with hidden
  resolution behavior.
- **Slot `Type` is informational, not load-bearing.** The .pr `Parameter` has a
  `Type` field; for variable-name slots it's always `"variable"` or `"string"`.
  Variable doesn't capture it — the slot Data still has it for any debugging /
  introspection that wants it. No new plumbing.

## Source generator: what collapses

The current source gen has three property emitters (`Data/`, `Provider/`, `Legacy/`)
plus several auxiliary mechanisms that only exist because `[VariableName]` exists.

| Item | Today | After v5 |
|---|---|---|
| `Emission/Property/Legacy/this.cs` | Emits raw scalar properties incl. `[VariableName]` strings | **Deleted.** No consumers. |
| `Emission/Property/Data/this.cs` | Emits `Data<T>` and plain `Data` | Unchanged. Handles `Data<Variable>` automatically via existing `As<T>` Resolve dispatch (line 612 of `Data/this.cs`). |
| `Emission/Property/Provider/this.cs` | Emits `[Provider] T` | Unchanged. |
| `Discovery/this.cs` `IsAppResolvable` detection (lines 178-185) | Routes types with static `Resolve(string, ctx)` to Legacy | **Deleted.** Was already redundant — `As<T>` does the dispatch at runtime; the discovery flag had no path to trigger after `Data<Path>` etc. became universal. |
| `Discovery/this.cs` `IsVariableName` flag | Routes `[VariableName]` to Legacy | **Deleted** along with the attribute. |
| `Discovery/this.cs` `ScanRawScalarValidations` (lines 254-282) | Generates "missing parameter" check for `[VariableName]` strings | **Deleted.** Handlers that need presence-check use the existing `[IsNotNull]` attribute. |
| `Emission/Action/this.cs` `__StripPercent` helper (lines 290-298) | Used only by `[VariableName]` legacy emit | **Deleted.** Logic moves into `Variable.Resolve`. |
| `Emission/Action/this.cs` `__Resolve<T>` helper (lines 261-274) | Used only by Legacy non-`[VariableName]` paths | **Deleted.** No consumers — Legacy is gone. |
| `Emission/Action/this.cs` `__HasParam` helper (lines 284-288) | Used only by Legacy default-value path | **Deleted.** No consumers. |
| `Emission/Action/this.cs` `__ResolveData` helper (lines 276-282) | Used by Data emit | **Kept.** Still load-bearing. |
| `Emission/Action/this.cs` `RawScalarValidations` block (lines 192-211) | Emits the missing-param ServiceError before Run | **Deleted.** `[IsNotNull]` covers it; bare-empty is now handled by `Variable.Resolve` returning a Variable with `Name=""` that handlers (or `[IsNotNull]`) inspect. |
| `Discovery/this.cs` `ActionClassInfo.RawScalarValidations` field | Carries the validation list | **Deleted** with the scan. |
| `App/modules/Attributes.cs` `[VariableName]` | The attribute itself | **Deleted.** |
| `Discovery/this.cs` `IsValidActionProperty` | PLNG001 gate, currently allows `Data<T>`, `[Provider]`, `[VariableName]` | Drop the `[VariableName]` allow-arm — gate is now `Data<T>` or `[Provider] T` only. |
| `Discovery/this.cs` PLNG001 message | "must be Data<T>, [Provider], or [VariableName] string" | "must be Data<T> or [Provider] T" |

After this, `Emission/Property/` is two peers (`Data/`, `Provider/`) — a cleaner OBP
shape than three, and PLNG001 is a two-rule gate.

## Handler migration: the 22 sites

Mechanical pattern, applied to each:

```csharp
// before
public partial class Set : ...
{
    [VariableName]
    public partial string Name { get; init; }
    ...
    public async Task<Data.@this> Run()
    {
        Context.Variables.Set(Name, value, ...);  // Name is string "x"
        ...
    }
}

// after
public partial class Set : ...
{
    public partial Data.@this<Variable> Name { get; init; }
    ...
    public async Task<Data.@this> Run()
    {
        Context.Variables.Set(Name.Value, value, ...);  // Name.Value is Variable, → "x" via implicit op
        ...
    }
}
```

Sites (verified by `grep [VariableName] /workspace/plang/PLang/App/modules/`):

**Write-target slots (Pattern A — handler does `Variables.Set(Name, ...)` or mutates the live Data):**
- `list/add.cs`, `list/remove.cs`, `list/reverse.cs`, `list/set.cs`, `list/sort.cs`
- `variable/clear.cs`, `variable/remove.cs`, `variable/set.cs`

**Read-only slots (Pattern B — handler does `Variables.Get(Name).Value`):**
- `list/any.cs`, `list/contains.cs`, `list/count.cs`, `list/first.cs`, `list/flatten.cs`,
  `list/get.cs`, `list/group.cs`, `list/indexof.cs`, `list/join.cs`, `list/last.cs`,
  `list/range.cs` (if applicable), `list/unique.cs`
- `loop/foreach.cs` (`ItemName`, `KeyName` — both)
- `variable/exists.cs`, `variable/get.cs`

Every site uses the same shape — `partial Data.@this<Variable> Foo` with
`Foo.Value` at use sites. No per-handler design decisions needed.

The previous architect/v1 plan distinguished read-site migration (Phase 1, kept)
from write-site migration (Phase 2, killed). v5 unifies them: both shapes go to
`Data<Variable>` because the regression vector that killed the v1 write-site
migration (silent slot-key writeback) does not apply here — `Variable.Resolve`
collapses `%x%` and `x` to the same `.Name`, so neither read nor write paths
diverge from today's `__StripPercent` behavior.

## Phasing

Three commits, ordered for bisectability:

1. **Add Variable + lock the contract.** Create
   `/PLang/App/Variables/Variable.cs`. Add tests in
   `/PLang.Tests/App/VariablesTests/VariableResolveTests.cs` covering:
   - `Resolve("%x%", ctx)` → `{ Name="x", RawValue="%x%", WasPercentWrapped=true }`
   - `Resolve("x", ctx)` → `{ Name="x", RawValue="x", WasPercentWrapped=false }`
   - `Resolve("", ctx)` → `{ Name="", RawValue="", WasPercentWrapped=false }`
   - `Data<Variable>.As<Variable>(ctx)` for slot `{name="Name", value="%x%"}` →
     `.Value.Name == "x"`
   - `Data<Variable>.As<Variable>(ctx)` for slot `{name="Name", value="x"}` →
     `.Value.Name == "x"`
   - Implicit conversion: `string s = new Variable("x", "%x%", true);` → `s == "x"`

   Don't touch any handler. Don't touch source gen. The Variable class stands
   alone and its contract is verified independently. Reuses existing AppResolvable
   dispatch in `Data.As<T>` — no new wiring needed.

2. **Migrate the 22 handler sites.** Each site:
   `[VariableName] partial string X` → `partial Data.@this<Variable> X`,
   plus `X` → `X.Value` at use sites.

   `dotnet build` must pass after this commit. PLang test matrix
   (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`) must pass.
   Critical regression target: `Tests/App/StepResult/StepResult.test.goal`
   exercises `set %x% = "hello"` end-to-end — same case the variablename-migration
   branch verified.

   Do NOT delete Legacy yet. The `[VariableName]` attribute is still defined.
   PLNG001 still allows it. This commit is purely the handler shape change; the
   source gen is unchanged.

3. **Delete the dead code.**
   - `Emission/Property/Legacy/this.cs`
   - `[VariableName]` attribute from `Attributes.cs`
   - `IsVariableName` flag from `Discovery/this.cs`
   - `IsAppResolvable` detection from `Discovery/this.cs` (was already vestigial)
   - `ScanRawScalarValidations` + `RawScalarValidation` record from `Discovery/this.cs`
   - `RawScalarValidations` field from `ActionClassInfo`
   - `RawScalarValidations` block from `Emission/Action/this.cs`
   - `__StripPercent`, `__Resolve<T>`, `__HasParam` helpers from `Emission/Action/this.cs`
   - PLNG001 message and gate updated to two rules

   Build + run both test suites. If anything breaks, do NOT add a fallback. The
   point is removing the carve-out machinery; a regression here means a missing
   handler migration in step 2.

Each commit lands independently. Reviewers can bisect: commit 1 isolated proves
Variable's contract; commit 2 isolated proves handler migration works on top of
the *unchanged* source gen; commit 3 proves the source-gen cleanup is purely
removal.

## Documentation updates (in commit 3 or a follow-up)

- `/PLang/App/CLAUDE.md` — replace the `[VariableName]` carve-out paragraph
  ("the carve-out for handlers that need the variable's *name* not its value —
  folded into `Data<T>` once a `VarRef<T>` design lands") with: "Action handler
  properties are `Data<T>` or `[Provider] T`. For parameters that semantically
  identify a variable (write targets, read-by-name lookups), use `Data<Variable>`
  — `Variable.Value` returns the canonical name string via implicit conversion."

- `/Documentation/v0.2/good_to_know.md` — add an entry on the Variable shape:
  why it exists (shape symmetry, signing-readiness, surfaces the LLM bare-name
  case via `WasPercentWrapped`), the implicit-conversion gotcha (`var foo`
  infers `Variable` not `string`), and the rule that any "I'm naming a
  variable" parameter goes through `Data<Variable>`.

- `/Documentation/Runtime2/todos.md` — close the `2026-04-30` entry that
  scheduled the `[VariableName]` migration. Note it landed via Variable rather
  than direct deletion.

CLAUDE.md propose-don't-edit rule applies — write the proposed change to
`.bot/runtime2-generator-obp/claude-md-proposals.md` for the docs bot to apply
at merge.

## Decisions documented

1. **Variable equality is technically loose.** `Variable("x", "%x%", true)` ≠
   `Variable("x", "x", false)` under default record equality, even though both
   semantically refer to "variable x". Decided per Ingi: leave as default
   equality — handlers don't compare Variables, so the looseness doesn't matter
   in practice. Documented for future readers; not changed.

2. **`IsAppResolvable` detection deletion — accept the build-failure feedback
   loop.** Plan asserts the detection has no consumer after `[VariableName]` is
   gone (today's grep shows zero `partial Path` / `partial Actor` declarations).
   Coder deletes it in commit 3; if some new handler between branch fork and
   merge introduced a raw `partial Path` declaration, the build will fail and
   coder migrates that handler to `Data<Path>` as part of the same commit. No
   pre-flight verification needed — fail fast, fix forward.

## Handoff order

Skip test-designer for this branch — test surface is small (six `Variable.Resolve`
unit tests enumerated in commit 1) and the regression contract for the rest is
"the existing PLang test matrix under `Tests/` passes." Test-designer earns its
keep when the test surface is large and matrix-shaped (v4 had 139 stubs across
handler × parameter-shape × error-case combinations); v5 doesn't have that
shape.

1. **Coder** runs the three-phase migration. Commit 1 includes the six
   `Variable.Resolve` unit tests inline. After each commit:
   `dotnet run --project PLang.Tests` AND
   `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` must pass.
   The bare-name regression target is `Tests/App/StepResult/StepResult.test.goal`
   and any list/* PLang test that exercises `value="x"` without `%` (coder
   inspects existing test goals during commit 2 to confirm coverage; if a gap is
   visible, add a small PLang test goal that explicitly exercises the bare form).
2. **codeanalyzer / tester / security / auditor** review.
3. **docs** applies the CLAUDE.md proposal and updates good_to_know /
   todos.md.

## Architectural note

This v5 plan vindicates the v4 stated goal ("every action property is `Data<T>`,
delete `[VariableName]`") via a path v4 didn't anticipate. The v4 plan implicitly
assumed `[VariableName]` would dissolve into `Data<string>` with `As<T>.Name`
propagation. The variablename-migration branch proved that path brittle. v5
takes the alternate route: keep the *requirement* (variable name as Data) but
introduce a *type* (Variable) that carries the resolution rule explicitly,
reusing the AppResolvable dispatch already wired into `Data.As<T>`.

The win compounds: PLNG001 collapses to two rules, source gen drops a whole
emission branch and three helpers, the `[VariableName]` attribute disappears,
and the future signing work lands at the wrapper level — `Data<Variable>.Signature`
is the same shape as `Data<string>.Signature`, no carve-out.

The cost: 22 handler sites change, plus one indirection at use sites
(`Name.Value` instead of `Name`). The implicit string conversion absorbs the
indirection at the boundary; handler authors never write `Name.Value.Name`.
