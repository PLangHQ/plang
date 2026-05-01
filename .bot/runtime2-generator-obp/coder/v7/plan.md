# v7 plan — Variable as a first-class type, replacing [VariableName]

Response to architect/v5/plan.md. The architect's plan is complete and unambiguous —
this is an implementation plan, not a re-architecture.

## Context

`runtime2-variablename-migration` (062947bc) verified that the original `As<T>.Name`
propagation path works for `value="%x%"` but silently miswrites for `value="x"`.
Coder v1 there declined the migration. Architect v5 takes a different route: introduce
`Variable` as an `App.Variables.Variable` record with `static Resolve(string, Context.@this)`,
declare every former `[VariableName] string` as `Data.@this<Variable>`. Existing
`As<T>` dispatch at `Data/this.cs:612-624` already invokes static `Resolve` — no new wiring.

The plan's three commits are bisectable: add Variable + tests; migrate 22 handler sites;
delete the carve-out machinery. I'll execute in that order.

## Phase 0 — what I read

- `Data/this.cs:612-624` — confirmed the static `Resolve(string, Context.@this)`
  reflection dispatch that `Data<Variable>.As<Variable>(ctx)` will hit. Caches the
  MethodInfo per-T. Path uses this exact shape.
- `App/modules/Attributes.cs:36` — `[VariableName]` is a marker attribute, no payload.
- All 22 `[VariableName]` sites — verified by grep. Distribution matches the plan:
  16 in `list/`, 2 in `loop/foreach.cs`, 4 in `variable/`.
- `Discovery/this.cs` — three places mention `[VariableName]`: `IsValidActionProperty`
  (line 132), `BuildProperty` (line 168), `IsAppResolvable` discovery (line 178), and
  `ScanRawScalarValidations` (line 254). PLNG001 message at line 47.
- `Emission/Property/Legacy/this.cs` — emits raw scalar with `__Resolve<T>` /
  `__StripPercent`. `IsVariableName` branch at line 46.
- `Emission/Action/this.cs` — `RawScalarValidations` block at lines 192-211;
  `__Resolve<T>` / `__HasParam` / `__StripPercent` legacy helpers at 256-300.
- `Emission/Property/Data/this.cs` — `Data<T>` emit, unchanged. Will handle
  `Data<Variable>` automatically via the As<T> static-Resolve dispatch.
- Sample handlers (variable/set, variable/get, variable/exists, variable/remove,
  list/get, list/contains, list/sort, list/set, list/group, list/any, loop/foreach)
  — confirmed the use-site shape. Two access patterns:
  - `Context.Variables.Get(ListName)` / `Context.Variables.Set(Name, ...)` —
    string-expecting boundary; Variable→string implicit conversion fires.
  - `$"... '{ListName}' ..."` — string interpolation calls `ToString()`.
    Default record `ToString` produces `Variable { Name = x, ... }`, ugly. Solution
    below.

## One small departure from the plan: Variable.ToString()

The plan defines `implicit operator string` on Variable so handlers can write
`Variables.Get(Name.Value)` and have the conversion fire at the parameter boundary.
That covers method-call sites cleanly.

But existing handlers also do `$"Variable '{ListName}' is not a list"` (list/sort.cs:19,
list/get.cs:18, list/set.cs:19, list/set.cs:23). String interpolation calls `ToString()`,
not the implicit operator. With a plain record, that emits the synthesized
`Variable { Name = x, RawValue = %x%, WasPercentWrapped = True }` — broken UX in error
messages and logs.

Two options I considered:
1. Rewrite every interpolation site to `{ListName.Value.Name}` — touches more files,
   reader has to remember `.Name`.
2. Override `ToString() => Name` on Variable — interpolation produces the canonical
   name, matching the implicit-string semantics.

Going with **option 2**. It keeps the plan's "implicit conversion absorbs the
indirection at the boundary" promise — `{ListName.Value}` reads as the variable name,
same as `ListName.Value` when assigned to a string. Plan doesn't forbid this; it
strengthens the same principle. I'll note it in v7/summary.md.

`Equals` / `GetHashCode` stay record-default (the architect explicitly accepted the
loose equality "leave as default — handlers don't compare Variables"). Only `ToString`
is overridden.

## Phase 1 (Commit 1) — Add Variable + tests

Files:
- `PLang/App/Variables/Variable.cs` — new sealed record per plan, with the
  `ToString() => Name` override above.
- `PLang.Tests/App/VariablesTests/VariableResolveTests.cs` — new test file with the
  six cases the plan enumerates plus three for `ToString` / implicit conversion.

The `Data<Variable>.As<Variable>(ctx)` test cases need a real Context. I'll mirror
the fixture pattern used in existing `VariablesTests.cs` (build a `Variables` with
no app — `Data.As<T>` doesn't require Variables population for static-Resolve
dispatch; the slot's raw string flows through unchanged).

Build + tests green, commit. Source gen and handlers untouched in this commit.

## Phase 2 (Commit 2) — Migrate 22 handler sites

Mechanical replacement per file. The 22 sites:

**Pattern A — write target (Variables.Set, mutation by name):**
- `list/add.cs`, `list/remove.cs`, `list/reverse.cs`, `list/set.cs`, `list/sort.cs`
- `variable/clear.cs` ← does not exist; plan says clear but grep shows no
  `variable/clear.cs`. Plan list is illustrative not authoritative — actual
  variable/* sites are exists, get, remove, set (4). Will skip clear.
- `variable/remove.cs`, `variable/set.cs`

**Pattern B — read by name:**
- `list/any.cs`, `list/contains.cs`, `list/count.cs`, `list/first.cs`, `list/flatten.cs`,
  `list/get.cs`, `list/group.cs`, `list/indexof.cs`, `list/join.cs`, `list/last.cs`,
  `list/unique.cs`
- `loop/foreach.cs` (`ItemName`, `KeyName` — both, both nullable)
- `variable/exists.cs`, `variable/get.cs`

(plan listed `list/range` "if applicable" — grep shows no such file — skip.)

Per-site changes:
- `[VariableName] partial string X` → `partial Data.@this<Variable> X`
  (or `Data.@this<Variable>?` when previously `string?`).
- Drop the `[VariableName]` attribute line.
- Add `using App.Variables;` if not already present.
- At each use site, replace `X` with `X.Value` (or `X?.Value?.Name` for nullable
  cases — needs explicit `.Name` because `Variable?` doesn't implicit-convert).

For `loop/foreach.cs` specifically: `var variableName = ItemName ?? "item"` becomes
`var variableName = ItemName?.Value?.Name ?? "item"` (Variable?.Name string).
`if (KeyName != null) Variables.Set(KeyName, key)` becomes
`if (KeyName != null) Variables.Set(KeyName.Value, key)` (Value is non-null Variable
when wrapper is non-null; implicit-string fires at Set's parameter).

After all 22 sites land:
- `dotnet build` clean
- `dotnet run --project PLang.Tests` passes
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` passes

If a plang test regresses for `value="x"` (bare LLM emission), that's the design
contract proof — `Variable.Resolve("x", ctx)` produces `Name="x"` symmetrically with
`%x%`. Architect's design says this works; it should.

`[VariableName]` attribute and Legacy emitter still in place this commit. Commit 3
removes them.

## Phase 3 (Commit 3) — Delete dead code

Per plan §"Source generator: what collapses":

- Delete `PLang.Generators/Emission/Property/Legacy/this.cs` (file).
- Delete `[VariableName]` attribute from `App/modules/Attributes.cs`.
- `Discovery/this.cs`:
  - Drop the `[VariableName]` arm from `IsValidActionProperty` (line 132).
  - Drop `isVariableName` flag from `BuildProperty` (line 168).
  - Drop `isAppResolvable` detection (lines 178-185) — the plan calls this
    vestigial; coder verifies by grep that no handler still has `partial Path X`
    or similar declarations expecting Legacy emit. Today everything is
    `Data.@this<Path>`.
  - Drop `ScanRawScalarValidations` method (254-282) and `RawScalarValidation`
    record (307).
  - Update PLNG001 `messageFormat` to "must be Data&lt;T&gt; or [Provider] T".
  - Drop the `[VariableName]` doc-comment on the descriptor (line 41-43).
- `ActionClassInfo` record loses the `RawScalarValidations` field (line 304) and
  the constructor argument at line 118 in `GetActionClassInfo`.
- `Emission/Action/this.cs`:
  - Drop the `RawScalarValidations` emit block (lines 192-211).
  - Drop `__StripPercent` helper (290-298).
  - Drop `__Resolve<T>` helper (261-274) — used only by Legacy and the deleted
    block.
  - Drop `__HasParam` helper (284-288) — used only by Legacy default-value path.
  - `__ResolveData` stays — Data emission depends on it.
  - Update `EmitLegacyHelpers` doc-comment to reflect what remains (or delete the
    method if empty).
- Update `using LegacyProperty = ...` in Discovery/this.cs (line 9) — delete it.
- Any tests under `PLang.Tests/Generator/` referencing Legacy will need to be
  updated or deleted; will assess at build time.

After:
- `dotnet build` clean
- Both test suites pass

If anything breaks here that the plan didn't predict (e.g. a synthetic test
exercises Legacy emit directly), I stop and report — don't add a fallback.

## Documentation (separate sub-step inside Commit 3 or follow-up)

Per the plan, append to `.bot/runtime2-generator-obp/claude-md-proposals.md`:

- `/PLang/App/CLAUDE.md` — replace the `[VariableName]` carve-out paragraph with
  the new two-rule contract + Variable.
- `/Documentation/v0.2/good_to_know.md` — note Variable's `WasPercentWrapped`
  + `var foo = Name.Value` infers Variable not string.
- `/Documentation/Runtime2/todos.md` — close the `2026-04-30` `[VariableName]`
  migration entry, note it landed via Variable.

## Validation contract for each commit

| Commit | dotnet build | dotnet PLang.Tests | plang --test |
|---|---|---|---|
| 1 (Variable + tests) | green | green (+9 new) | unchanged |
| 2 (22 handlers) | green | green | green |
| 3 (delete) | green | green | green |

## Risks I'm watching for

1. **`Variable` namespace clash with the `Variables = App.Variables.@this` global
   alias.** `using App.Variables;` brings in members, including `Variable` (record).
   The alias `Variables` resolves to the collection `@this`. Different names; no
   clash. Verified mentally; will confirm at first build.

2. **`partial Data.@this<Variable>?` for nullable.** Source gen's Data emitter
   already handles `IsNullable` for `Data<T>`. Should work without modification.
   Will verify at Phase 2 build.

3. **String interpolation of nullable Variable.** `$"{ItemName?.Value}"` produces
   "" when null (with my `ToString = Name` override) — same as the current
   `$"{ItemName ?? ""}"` shape. Acceptable.

4. **A handler outside the 22 grep matches that does `partial Path X` (raw
   IsAppResolvable).** Plan accepts the build-failure feedback loop. I'll grep
   first to be safe; if found, migrate inline as part of commit 3.

## What I'll do if a plang test fails after commit 2

Per plan and per project rule: never edit `.test.goal` to make a failure go away.
Read the `.pr` file, confirm the action's parameters, then trace through
`Variable.Resolve` for the actual raw string. The bare-name regression target
(`Tests/App/StepResult/StepResult.test.goal` per the plan) should pass because
`Resolve("x", ctx)` produces `Name="x"`.
