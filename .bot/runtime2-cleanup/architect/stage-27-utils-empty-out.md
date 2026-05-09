# Stage 27: `utils-empty-out`

**Read first:**
- `plan/principles.md` — OBP discipline. Rule C (static fields are a missing `@this`).
- `plan/post-cleanup-tree.md` — destination tree section on `Utils/` ("nearly empty"; 4 files remain after Tier 5).
- Stage 26 brief — establishes `Types.@this` as a partial; this stage adds the Conversion partial.

**Goal:** Empty out `App/Utils/` to the 4 files originally planned. Two pieces:

1. **`Utils/TypeConverter.cs` → `Types/Conversion.cs` partial.** Mechanical now that Types is already a partial (stage 26). One consumer, clean move.
2. **`Utils/Json.cs` disperses.** Five `JsonSerializerOptions` static bags, two static helper methods, one extension class, two internal converters. Each piece moves to where its consumer is. Cross-cutting bags accept small duplication as the OBP-correct trade — the alternative (a consolidated `Json.@this` god-bag) reopens the same Rule C smell at instance scope.

After: `Utils/` contains exactly 4 files — `CommandLineParser.cs`, `PathExtension.cs`, `RegisterStartupParameters.cs`, `StringDistance.cs`. The Tier 5 cleanup completes; the destination tree from `plan/post-cleanup-tree.md` matches reality.

## Scope

**Included:**
- `Utils/TypeConverter.cs` body absorbs into `Types/Conversion.cs` (new partial of `Types.@this`); file deleted.
- The 4 public methods on `Types.@this` that today delegate to TypeConverter (lines around stage 26's landing) become the implementation directly.
- `Utils/Json.cs` disperses: 5 options bags + 2 helpers + 1 extension + 2 converters. Each piece relocates to its consumer's home. File deleted.
- Caller sweep: every `App.Utils.TypeConverter.X` → `app.Types.X` (already-renamed wrappers stay; the wrapper *bodies* get inlined). Every `App.Utils.Json.X` → `<consumer-home>.X`.
- Test facade follow-up: stage 26's `PLang.Tests/Support/TypeMappingTestFacade.cs` may need extension to cover the new TypeConverter routing — coder's call.

**Excluded:**
- Any other Utils/ file. The 4 remaining (`CommandLineParser`, `PathExtension`, `RegisterStartupParameters`, `StringDistance`) stay where they are; they don't belong to a subsystem with a natural home today, and they're each their own concern. Opening Utils/ fully empty is its own design pass for another branch.
- Any change to TypeConverter behaviour: the 4 public methods + 4 private helpers + the conversion logic table stay byte-identical.
- Any change to JsonSerializerOptions configuration: if a consumer absorbs `CaseInsensitiveRead`, the resulting field has the same converters list, the same NumberHandling, etc.
- Renaming methods (e.g. `TryConvertTo` → `Convert`).

## Deliverables

### Part 1: `Types/Conversion.cs` partial (mechanical)

New file `PLang/App/Types/Conversion.cs`:

```csharp
namespace App.Types;

public sealed partial class @this
{
    /// <summary>Converts value to T or returns null on failure.</summary>
    public T? ConvertTo<T>(object? value) => (T?)ConvertTo(value, typeof(T));

    /// <summary>Converts value to targetType or returns null on failure.</summary>
    public object? ConvertTo(object? value, System.Type targetType) { /* body from TypeConverter */ }

    /// <summary>Populates target's properties from a values dictionary.</summary>
    public void Populate(object target, IDictionary<string, object?> values) { /* body from TypeConverter */ }

    /// <summary>Conversion with structured error result.</summary>
    public (object? Value, Errors.Error? Error) TryConvertTo(
        object? value, System.Type targetType, Actor.Context.@this? context = null) { /* body from TypeConverter */ }

    // 4 private helpers — pure logic, stay static (Rule C exception):
    private static string FormatTypeMismatch(...) { ... }
    private static string TypeMismatchHint(...) { ... }
    private static string FormatValuePreview(...) { ... }
    private static System.Type? GetListElementType(...) { ... }
}
```

The 4 public methods replace the delegating wrappers added in stage 26 (`Types/this.cs:309-315` style): the body moves from `Utils/TypeConverter.cs` into the partial; the wrapper goes away.

The 4 private static helpers stay private static in the partial — pure logic, no state, Rule C exception #1 (static factory methods and helpers are behavior, not state, and stay).

`Utils/TypeConverter.cs` deleted entirely after the move.

### Part 2: `Utils/Json.cs` disperses

`Utils/Json.cs` contains 9 distinct things. Each moves separately. The principle: where exactly one consumer exists, move there as instance state. Where multiple consumers cross subsystems, accept per-consumer copies (allocation cost is negligible; OBP correctness is the priority — no shared "Json" god-bag).

#### 2a. `CaseInsensitiveRead` (`Json.cs:20-29`)

**Consumers:** 2 sites in `App/modules/http/code/Default.cs` (lines 391, 640).

**Move:** Instance field on `Default` (the HTTP code @this). After: Default has `_transportInOptions` (from stage 25) + `_caseInsensitiveRead` (this stage). Both private readonly. The 2 read sites already use the long form `App.Utils.Json.CaseInsensitiveRead` (per stage 25's consolidation) — switch to `_caseInsensitiveRead`.

**Internal converter dependency:** `EmptyStringToNullEnumConverterFactory` and `EmptyStringToNullEnumConverter<T>` ride along — they're listed in `CaseInsensitiveRead.Converters`. Move them as private nested types inside `Default.cs`, OR keep as separate file `App/modules/http/code/EmptyStringToNullEnumConverter.cs` if reuse is foreseen. Coder's call; nested types is cleaner if they truly only serve CaseInsensitiveRead.

#### 2b. `CamelCaseIndented` (`Json.cs:31-37`)

**Consumers:** 2 sites:
- `App/this.cs:391` — `App.Save()` writes `app.pr` metadata.
- `App/Data/this.Compare.cs:32` — Data comparison output.

Two different files, two different subsystems (App-level boot/save vs Data comparison). No single owner.

**Move:** Each consumer holds its own copy.
- `App.@this` gains a private readonly `_camelCaseIndented = new() { ... }`.
- `Data.@this` (the Compare partial) gains a private readonly `_camelCaseIndented = new() { ... }`.

Per-consumer copies cost ~1 KB each at construction. Allocation is one-shot at App boot (App.Save runs once per save; Data.@this allocates one Compare-partial per Data instance — but the field is on the partial declaration, so it allocates once per Data, which is many — hmm, see below).

**Note for coder:** Data.@this is allocated *frequently* (every value in the runtime is a Data instance). Per-Data allocation of a JsonSerializerOptions is wasteful. Two options for the Data.Compare site:

- **(a)** A private static readonly on the Compare partial — Rule C says no, but `static readonly` for pure-config bags with no instance variation is the case where Ingi has accepted exceptions (Channels.Serializers.Filters has same pattern).
- **(b)** Move to `Data.@this`'s **partial-shared static field** — if Data is partial across files, `private static readonly` declared in `this.Compare.cs` is per-Data-class, not per-Data-instance. That's the correct shape.

Option (b) is what's happening today (TypeConverter.cs has `private static readonly _options`); the eviction-to-instance rule applies *when the instance can hold it without waste*. For value types like Data that allocate frequently, the static-readonly-config shape is the pragmatic OBP exception. Same logic as why TypeMapping's pure-logic helpers stayed static in stage 26.

**Architect's lean:** for `Data.@this`'s site, keep as `private static readonly` on the Compare partial — pure config, no instance variation, allocation efficiency matters. Document the reasoning inline. For App.@this's site (one allocation per app, fine), use instance.

#### 2c. `SnapshotClone` (`Json.cs:44-49`)

**Consumers:** 2 sites:
- `App/Data/this.cs:917` — Data snapshot cloning.
- `App/Variables/this.cs:465` — Variables snapshot serialization.

Both are about snapshotting. Variables holds the snapshot machinery; Data is what gets snapshotted. Single conceptual owner: snapshot.

**Move:** Instance on `Variables.@this`. Read by Variables directly; Data navigates via `_context.App.User.Variables._snapshotClone` (or: same exception as 2b — keep as `private static readonly` on the Data partial since Data instances are frequent and the config doesn't vary).

Actually, looking at Data/this.cs:917 specifically — it's inside a method that has `_context` available. So `_context.App.<actor>.Variables._snapshotClone` works *if* `_snapshotClone` is internal/public. Or duplicate to a static-readonly on Data.

**Architect's lean:** instance on `Variables.@this` (one per actor, internally accessible); Data navigates if context available, otherwise duplicates as `private static readonly` on Data. Coder's call per-site.

#### 2d. `DiagnosticOutput` (`Json.cs:57-67`)

**Consumers:** 3 sites:
- `App/modules/test/report.cs:280` — test report serialization.
- `App/Errors/AssertionError.cs:42` — used via `FormatForDiagnostic` helper.
- `App/modules/assert/code/Default.cs:176` — also via `FormatForDiagnostic`.

Two of three actually call `FormatForDiagnostic(value)` rather than reaching the options directly. `FormatForDiagnostic` is the helper at `Json.cs:76-90` that uses `DiagnosticOutput` internally for non-scalar values.

**Move:** `DiagnosticOutput` and `FormatForDiagnostic` ride together. Two homes:
- **`Tester.@this`** — already exists; gains `DiagnosticOutput` as a property (or `FormatForDiagnostic` as a method that holds the options internally). `test/report.cs` reaches via `_context.App.Tester.FormatForDiagnostic(value)`.
- **`Errors.@this`** — gains `FormatForDiagnostic` as a method too. `AssertionError` and `assert/Default` reach via the error subsystem.

The trouble: `FormatForDiagnostic` is logically the same function in both places. Two implementations of the same helper.

**Architect's lean:** `App/Diagnostics/this.cs` new sub-`@this`, mounted as `app.Diagnostics`. Holds `Format(value)` (renamed from `FormatForDiagnostic` — drop the redundant suffix). Internally holds the `DiagnosticOutput` options. Three consumers reach `_context.App.Diagnostics.Format(value)`. One implementation.

This adds a small new subsystem rather than dispersing — but it's the OBP-correct shape because three distinct consumers genuinely need the same helper, and inventing per-consumer copies of the same logic is the worst of both worlds (duplicated code AND duplicated allocations).

**Alternative:** keep as a static method `App.Diagnostics.Format(value)` (static class) — pure logic with internal state-less options bag. Rule C says no for fields with state; but the field IS the const-ish JsonSerializerOptions, and "static config bag with no instance variation" is the exception case. **Architect's leans toward instance** but flags this as a coder/Ingi judgment call during implementation.

#### 2e. `PrWrite` (`Json.cs:93-104`) + `StoreOnlyModifier` (`Json.cs:106-129`)

**Consumers:** 1 production site (`App/modules/builder/code/Default.cs:181`), 1 test site (`PLang.Tests/App/Modules/builder/GetGoalsTests.cs:121`).

**Move:** `PrWrite` and its `StoreOnlyModifier` private helper move to `App/Builder/this.cs` (already exists from stage 17 rename + stage 12 absorb). Builder is where `.pr` writing concerns live. Mounted as `app.Builder.PrWrite` if public-facing, or as `_prWrite` private if the builder/code/Default reaches via `app.Builder._prWrite` — coder's call on visibility.

Test site: routes through the test fixture's `app.Builder.PrWrite` or the existing TestFacade pattern.

#### 2f. `JsonExtensions.ToJson()` (`Json.cs:155-178`) + `FixJsonStringValues` (`Json.cs:131-152`)

**Consumers:** 2 sites:
- `Utils/TypeConverter.cs:74` (becomes `Types/Conversion.cs:?` after part 1).
- `PLang.Tests/App/Modules/Schema/SchemaTests.cs:92`.

The extension parses a string as JSON with a forgiveness path (escape unescaped control chars and retry).

**Move:** This is a pure string→JsonNode utility. No subsystem owner — it's a parsing helper. **Architect's call: keep as a static extension class.** Relocate to `App/Data/JsonString.cs` — sits next to the JSON-related Data machinery. Or to `App/Channels/Serializers/JsonString.cs` if you prefer the serialization-machinery framing.

`FixJsonStringValues` is the private helper used by `ToJson` only. Moves with the extension.

**Architect's lean:** `App/Data/JsonString.cs` — Data is where parsing-related infrastructure lives (TString.cs is there too).

#### 2g. End state of `Utils/Json.cs`

Deleted entirely.

### Caller sweep

After all moves: `grep -rn "App\.Utils\.Json\b\|App\.Utils\.TypeConverter\b" PLang/ PLang.Tests/ --include='*.cs'` — zero hits.

Production callers (12 files):
- `App/this.cs` — switch CamelCaseIndented to local instance/static.
- `App/Data/this.Compare.cs` — switch CamelCaseIndented to per-Data static-readonly.
- `App/Data/this.cs` — switch SnapshotClone to navigate to Variables (or per-Data static).
- `App/Errors/AssertionError.cs` — switch FormatForDiagnostic to whatever home it lands at.
- `App/Variables/this.cs` — switch SnapshotClone to local instance.
- `App/modules/assert/code/Default.cs` — switch FormatForDiagnostic.
- `App/modules/builder/code/Default.cs` — switch PrWrite to Builder home.
- `App/modules/http/code/Default.cs` — switch CaseInsensitiveRead to instance field.
- `App/modules/test/report.cs` — switch DiagnosticOutput / FormatForDiagnostic.
- `App/Modules/Schema/Render.cs` — uses TypeConverter (already routes through Types after stage 26; just remove the lingering `Utils.TypeConverter` reference if any).
- `App/Types/this.cs` — wrapper bodies inline from Conversion partial.

Test callers (4 files):
- `PLang.Tests/App/Modules/builder/GetGoalsTests.cs` — `Json.PrWrite` reference.
- `PLang.Tests/App/Serializers/SensitivePropertyFilterTests.cs` — Json reference.
- `PLang.Tests/App/Testing/{DiscoverActionTests,RunActionTests}.cs` — Json references.
- `PLang.Tests/App/Utility/{TypeMappingTests,TypeMismatchExample,TypeMismatchMessageTests}.cs` — TypeConverter references.

Test facade (`PLang.Tests/Support/TypeMappingTestFacade.cs`) may need extension to cover the moved TypeConverter and Json bags. Coder's call on whether to extend the facade or migrate the 4 affected test files individually.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- `find PLang/App/Utils/TypeConverter.cs PLang/App/Utils/Json.cs` — both gone.
- `find PLang/App/Utils -type f -name '*.cs'` — exactly 4 files: `CommandLineParser.cs`, `PathExtension.cs`, `RegisterStartupParameters.cs`, `StringDistance.cs`.
- `find PLang/App/Types/Conversion.cs` — present.
- `grep -rn "App\.Utils\.TypeConverter\b\|App\.Utils\.Json\b" PLang/ PLang.Tests/ --include='*.cs'` — zero hits.
- `grep -n "static class TypeConverter\|static class Json\|static class JsonExtensions" PLang/` — zero hits.

**Dependencies:**
- **Stage 26 (Types keystone)** — must precede this stage. Types is partial; `Types/Conversion.cs` builds on the existing partial declaration.
- All other Tier 5 stages (23, 24, 25) — independent; don't block.

## Design

### The smell this closes

Two Rule C sites + the residual Utils/ home for type/JSON utilities. Both pieces are about ownership: TypeConverter and Json have no `@this` they belong to today. The fix is the same realignment as stage 26 — hand the data to types that own it.

The interesting part of stage 27 is *what to do when no single owner exists*. The Json options bags are cross-cutting: same configuration, multiple consumers, no natural single owner. Two solutions both fit OBP differently:

- **Per-consumer copies** — each consumer holds the options it needs as instance/static-readonly. Allocation duplicates; logic doesn't. OBP-clean per consumer.
- **A consolidated `Json.@this`** — one home for all options bags. One allocation. But the home has no semantic identity — it's just "the place where JSON config bags live." That's a god-bag at instance scope, which is the same Rule C smell at a different level.

This brief leans **per-consumer copies** for the bags with 2-3 distinct consumers, and **a small new sub-@this** (Diagnostics) only where the helper logic is genuinely shared (FormatForDiagnostic). Diagnostics has a real semantic identity — "the diagnostic-output subsystem" — even if today it has only one method.

### Why TypeConverter is the easy piece

TypeConverter has one consumer in the production code: `Types.@this.TryConvertTo` and friends. Stage 26 already added delegating wrappers; stage 27 just inlines the body. Tests reach TypeConverter directly today; after stage 27 they route through the test facade or a fixture's `app.Types`.

The 4 private static helpers stay private static — pure-logic helpers, Rule C exception. Same call as stage 26's `Types.@this` static helpers (`GetTypeNameStatic` etc.).

### Why Diagnostics as a new sub-@this

Three observations:
1. `FormatForDiagnostic` is genuinely the same logic in three places (test/report, AssertionError, modules/assert). Per-consumer copies duplicate code, not just allocation.
2. The function itself is small (15 lines) but it embeds policy: "scalars render directly, strings get quoted, anything else goes through DiagnosticOutput." Three copies = three places that can drift.
3. The DiagnosticOutput options have specific semantics — `Filters.Sensitive.Mask` modifier (not Strip; a different mode). This is part of the diagnostic policy, not a generic JSON configuration.

Mounting as `app.Diagnostics` is honest: there IS a diagnostics subsystem (assert + test + error reporting). It's small but real. Stage 27 names it.

If Ingi prefers to skip the new subsystem, fall back to per-consumer copies — flag in review.

### Why CamelCaseIndented and SnapshotClone aren't given a sub-@this

CamelCaseIndented has 2 consumers in different subsystems (App.Save, Data.Compare). SnapshotClone has 2 consumers in tightly-related subsystems (Data, Variables — both about snapshotting).

Neither has the "shared logic that would drift" smell that Diagnostics has — they're just options bags, no policy. Per-consumer copies are fine. Allocation overhead is negligible (App.Save runs once per save call; Data is allocated frequently, so use static-readonly there per the noted exception).

A smaller move I could see arguing for: a `App/Channels/Serializers/Options/this.cs` subsystem holding all the cross-cutting options. But Channels.Serializers is per-actor; these options are app-level. The shape doesn't fit. And they're not channel-specific anyway.

## Risk + dependencies

**Risk: low-medium.** The TypeConverter move is mechanical (one-to-one). The Json dispersal involves judgment per bag; missteps surface as build errors at the call sites.

Possible failure modes:
1. **A caller depending on a specific options-bag instance equality.** Tests that compare `JsonSerializerOptions` references (rare). Sweep for `== Json.X` or `ReferenceEquals(Json.X, ...)` — likely zero.
2. **The internal converters (`EmptyStringToNullEnumConverterFactory`, `EmptyStringToNullEnumConverter<T>`).** Used inside `CaseInsensitiveRead.Converters`. After moving CaseInsensitiveRead to Default, the converters need to come along. Confirm visibility in the new home.
3. **Builder PrWrite + StoreOnlyModifier.** `StoreOnlyModifier` is a private helper but the JsonTypeInfoModifier delegate is held in `PrWrite.TypeInfoResolver.Modifiers`. After move, the modifier method must be reachable from the new home. Coder confirms during implementation.
4. **The `CamelCaseIndented` per-Data instance shape.** Architect lean is `private static readonly` on the Data partial; if the brief is read literally as "each instance allocates," the per-Data allocation will surface as a measurable allocation regression. Coder reads carefully.
5. **Test facade may need extension.** `TypeMappingTestFacade.cs` was added in stage 26 to preserve ~150 test sites. Stage 27 changes `TypeConverter.X` → `app.Types.X`; the facade may need a corresponding routing for the 4 test files that reach TypeConverter directly today. Coder's call: extend facade vs migrate tests.

**Dependencies:**
- **Stage 26 (Types keystone)** — required upstream. Types must be partial. ✅ Already landed.
- **Stages 23, 24, 25** — independent. ✅ All landed.
- **Tier 5 closes after this stage.** Branch is then ready for review/merge to runtime2.

## Watch for (coder eyes-on)

- **The "where does this options bag belong" question.** Per-consumer copies, single-owner instance, or new sub-@this — three patterns. The brief proposes one per bag; if the proposed home reads wrong, surface during implementation rather than forcing the brief's call.
- **`Data.@this`'s frequent allocation.** Don't reflexively put options bags as instance fields on Data. Static-readonly on the Data partial is the correct OBP exception for pure-config bags with no instance variation. Same case as `Channels/Serializers/Filters/{Sensitive, Transport, View}.cs` keeping their modifier delegates static.
- **`JsonExtensions` and the `string.ToJson()` extension.** Public extension method; relocating the static class changes the using statement at every call site (currently 2 sites: TypeConverter and a test). Sweep with `grep`.
- **`AppendToBuilder` / namespace shifts.** When relocating the Json-extensions and EmptyStringToNullEnum types, the `namespace` line at the top of each file changes. Confirm tests still resolve the converter factory through the using statement (or via the Converters list inline).
- **Test facade extension.** If you keep the facade pattern (`TypeMappingTestFacade.cs`), add `TypeConverter` and the relevant `Json.X` bags to it. If you migrate tests individually, expect 4 test-file edits plus the facade staying untouched.

## Out of scope

- Any other Utils/ file (CommandLineParser, PathExtension, RegisterStartupParameters, StringDistance). They're each their own concern; some may eventually find homes (e.g. RegisterStartupParameters is App-boot-related), but that's a separate cleanup pass.
- Any rename of TypeConverter methods (ConvertTo → Convert, etc.).
- Any rename of options bags (CaseInsensitiveRead → ReadLenient, etc.). Names are stable as-is.
- Any rename of the Json-extension method (`ToJson` → `Parse`).
- Adding a new `app.Json` mount on App.@this. The dispersal is the goal; consolidating into a single Json @this would reopen Rule C at instance scope.

## Commit plan

```
runtime2-cleanup stage 27: utils-empty-out — TypeConverter + Json disperse

Final Tier 5 stage. Two pieces:

1. Utils/TypeConverter.cs absorbs into App/Types/Conversion.cs partial.
   Mechanical — Types is already partial (stage 26). The 4 public methods
   replace the delegating wrappers; the 4 private helpers stay static
   in the partial (pure-logic, Rule C exception).

2. Utils/Json.cs disperses to consumers. Five static options bags + 2
   helpers + 1 extension class + 2 internal converters move to where
   their consumers live:
   - CaseInsensitiveRead + EmptyStringToNullEnum converters →
     instance field on http/code/Default.cs (HTTP transport).
   - CamelCaseIndented → per-consumer copies (App.@this and
     Data.@this — Data uses static-readonly on the partial since Data
     is allocated frequently).
   - SnapshotClone → instance on Variables.@this; Data navigates to
     it (or static-readonly on Data partial).
   - DiagnosticOutput + FormatForDiagnostic → new App/Diagnostics/this.cs
     sub-@this, mounted as app.Diagnostics. Three consumers (Tester,
     Errors, modules/assert) reach via app.Diagnostics.Format(value).
   - PrWrite + StoreOnlyModifier → Builder.@this (.pr-file concern).
   - JsonExtensions.ToJson() + FixJsonStringValues → App/Data/JsonString.cs
     (parsing utility, pure logic).

Files deleted:
- App/Utils/TypeConverter.cs
- App/Utils/Json.cs

After: App/Utils/ contains exactly 4 files (CommandLineParser.cs,
PathExtension.cs, RegisterStartupParameters.cs, StringDistance.cs) —
the destination tree from plan/post-cleanup-tree.md matches reality.

C# 2752/2752 + PLang 199/199 baseline preserved.

Tier 5 stage 27 — final stage. Tier 5 closes here; runtime2-cleanup
branch is ready for review and merge to runtime2.
```
