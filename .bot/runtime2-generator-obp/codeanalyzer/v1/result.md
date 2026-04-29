# codeanalyzer v1 — runtime2-generator-obp — findings

Reviewed v4 implementation (resolution → `Data.As<T>(context)`, generator restructure, `App.Run` scaffolding, PLNG001). All numbered findings reference file:line. Severity legend: **MAJOR** = correctness or design contract violation, **MINOR** = simplification/dead code, **NIT** = readability.

---

## PLang.Generators/Discovery/this.cs

### OBP Violations

1. **MAJOR — Line 281: `ActionClassInfo` is `sealed class`, not `record` (Rule 7 — relay, don't repackage).** This is the value carrier passed across `IIncrementalGenerator` pipeline stages from `CreateSyntaxProvider` → `RegisterSourceOutput`. Without value equality, two structurally identical instances from successive compilations are seen as different, and downstream stages re-execute every time the syntax tree is touched.
   - Compounding: every `List<T>` field on this class also lacks element-wise equality (`List<PropertyBase>`, `List<string>`, `List<RawScalarValidation>`, `List<DiagnosticInfo>`). Even if `ActionClassInfo` were a record, list reference equality would defeat the cache.
   - Architect's plan and the comment at line 280 ("Lists carry value-equal records only — so the IIncrementalGenerator cache treats two semantically identical inputs as equal") promise incremental safety that the structure does not deliver.
   - The `ActionPropertyRecord_NoSymbolLeaks_IncrementalSafe` test only verifies absence of `IPropertySymbol` references; it does **not** test that the cache actually hits — so the test passes despite the structural defect.
   - Fix: convert to `public sealed record ActionClassInfo(...)` and replace `List<T>` with a value-equal collection (`EquatableArray<T>` or `ImmutableArray<T>` + `SequenceEqual`).
   - Impact: build performance, not correctness. But the design promise is broken.

### Simplifications

2. **MINOR — Line 134, 192: `OriginalDefinition.Name == "@this"` branch is dead.** Roslyn's `IPropertySymbol.Name` returns the identifier without the `@` prefix. The `"this"` branch alone matches; the `"@this"` branch never fires. Drop the redundant disjunct.
   ```csharp
   // current
   && (dt.OriginalDefinition.Name == "this" || dt.OriginalDefinition.Name == "@this")
   // simpler
   && dt.OriginalDefinition.Name == "this"
   ```

3. **MINOR — Line 194–197: triple `prop.Type as INamedTypeSymbol` cast in `BuildProperty.isPlainData`.** Three sequential null-coalescing casts of the same expression. Extract a local once.

4. **MINOR — Lines 124–138 vs. 146–214: `IsValidActionProperty` and `BuildProperty` both classify the same property type via different code paths.** `IsValidActionProperty` accepts {Provider, VariableName, Data, Data<T>}; `BuildProperty` builds {ProviderProperty, DataProperty, LegacyProperty}. If a new attribute is added (or a kind is renamed), both must be updated and the failure mode is silent — the diagnostic might fire on a property that nonetheless emits successfully via Legacy. Consider one classifier returning `(PropertyKind, ActionProperty?)` from which both the diagnostic decision and the emission record derive.

### Readability

5. **NIT — `BuildProperty` (lines 146–214) is 70 lines doing 5 things:** Provider check → ReadDefault → VariableName check → AppResolvable check → IEvent detection → Data wrapper detection → fall-through. The conditional cascade is hard to read top-to-bottom; reordering or named local helpers (`DetectKind`, `ReadAttributes`) would help.

6. **NIT — `RawScalarPropertyDescriptor` is `public static readonly`** but it doesn't need to be public — only the orchestrator references it via `Discovery.@this.RawScalarPropertyDescriptor`. Mark `internal` to scope the diagnostic-emission contract.

### Behavioral

7. **MAJOR — Diagnostic location is fabricated from `(Line, Char) → (Line, Char + 1)` regardless of actual identifier length** (this.cs:35–36 in the orchestrator). A property name is almost always more than one character, so the squiggle in the IDE points at a single column. Could use `loc.SourceSpan` from the original `IPropertySymbol.Locations[0]` directly — Roslyn already has the correct span; the orchestrator throws it away in favor of a synthetic 1-character span.

### Verdict: NEEDS WORK
Strong shape on the property-record hierarchy, but the umbrella `ActionClassInfo` undermines the incremental contract.

---

## PLang.Generators/this.cs

### OBP Violations

(none — this is a 47-line orchestration shim and reads cleanly.)

### Simplifications

8. **MINOR — Line 31–37: ad-hoc Roslyn `Location.Create` from `(FilePath, Line, Char)`** when `IPropertySymbol.Locations[0]` already exists upstream. The current code reconstructs a synthetic location with a `(line, char)→(line, char+1)` span. This belongs in Discovery alongside the symbol; pass a `Location?` (or its `(SourceText, TextSpan)`) through `DiagnosticInfo` instead of `(file, line, char)`. (Tied to finding 7.)
   - Alternatively: hold a `LinePositionSpan` instead of `(Line, Character)` so the IDE shows the full identifier underlined.

### Readability

9. **NIT — `LazyParamsGenerator` class name is a leftover** from before the v4 restructure. The class no longer contains lazy-params generation; it dispatches to Discovery + Emission. Rename to `ActionGenerator` (or just `@this`, since the file is `this.cs`). Currently the generated source folder under `obj/.../generated/PLang.Generators.LazyParamsGenerator/` carries the old name — visible in test paths (`SnapshotParamsTests.cs:31`).

### Verdict: CLEAN

---

## PLang.Generators/Emission/Action/this.cs

### OBP Violations

10. **MINOR — `EmitDataAndErrorHelpers` (lines 99–114) emits per-class `Data()`/`Error()` helpers** that are short-form sugar for `App.Data.@this.Ok()` / `App.Data.@this.FromError(...)`. These don't violate OBP per se but add ~5 lines per generated class for handlers that may never invoke them. Either drop, or skip the emission unless the generator can prove the handler uses them (compile-time scan would require Roslyn lookup; not worth it). Leave for now.

### Simplifications

11. **MAJOR — Lines 79, 122: `__variables` field is dead.** Set in ExecuteAsync (`__variables = context.Variables;`), declared at line 79, never read in any emission or by any test. Delete the field and the assignment. (Verified across `PLang/`, `PLang.Tests/`, `os/` — no readers.)

12. **MAJOR — Lines 91–97: `EmitParamDataAccessor` emits `__paramData` dict + protected `ParamData(string)` accessor on every action partial.** `__paramData` is filled by `__Resolve<T>` (legacy helper, line 230); `ParamData(name)` accessor has zero callers across the entire repo (`PLang/`, `PLang.Tests/`, `os/`). Pure dead emission — the dictionary is filled but never read. Delete the dict, the writer, and the accessor.

13. **MINOR — Lines 81, 178, 204, 231: `__resolutionError` is set only by the legacy `__Resolve<T>` helper.** Handlers that have only `Data<T>`/`Provider` properties (i.e., the entire post-Phase-5 shape) have `__resolutionError = null` reset at the top, then never reassigned, so the two `if (__resolutionError != null) return __resolutionError;` guards always fall through. Keep through Phase 5 cleanup (gates removal of the legacy helper family); delete then.

14. **MINOR — Lines 80, 123–124: `__app = context.App;` then `var app = __app!;`** — two names for one value. The eager Provider init (line 159) uses `app`; `Provider/this.cs:19` uses `__app ?? Context?.App`. The fallback `Context?.App` exists for the case where the property is read before ExecuteAsync runs (direct C# composition via init). Could simplify by:
    - dropping `__app` altogether; eager Provider init reads `context.App` directly.
    - Provider property fallback reads `Context?.App` (every IContext implementer has Context set; non-IContext is rare).
    - This trims a field per generated handler and removes the "two names for one thing" oddity.

15. **MINOR — Lines 219–268: `EmitLegacyHelpers` emits `__Resolve<T>`, `__ResolveData`, `__HasParam`, `__StripPercent` unconditionally.** Only `__ResolveData` is used by every `DataProperty` emission. `__Resolve<T>`, `__HasParam`, `__StripPercent` are LegacyProperty's. If the handler has zero LegacyProperty entries, these three helpers are dead emission. Skip them when `info.Properties.OfType<LegacyProperty>()` is empty. The unconditional emission is what `RawScalarValidation` paths depend on too — check the analysis is end-to-end before pruning.

16. **MINOR — Lines 173–187 (legacy raw-scalar non-null validation block) and lines 190–202 (`[IsNotNull]` validation block)** do nearly the same thing twice with slightly different error keys ("MissingParameter" vs "ValueRequired") and slightly different messages. Both walk `__action.Parameters` looking up the same property names. Once Phase 5 lands and legacy goes, the raw-scalar block disappears, and this collapses to one validator. For now, transitional.

17. **MINOR — Lines 130–138: backing-field reset is wrapped `if (action != null)`** to skip when `action == null` (direct C# composition via init). But `__paramData = new(...)` (line 126) and `__resolutionError = null` (line 125) run unconditionally. If action is null, those are still reset, then immediately neither is used (no `__Resolve` calls happen because no action). Harmless but odd — either everything resets unconditionally (consistency) or the whole block is gated.

### Behavioral

18. **MAJOR — Lines 116–207: thin `ExecuteAsync` is still doing six things.** Architect's plan called this "thin" — and it is thinner than v3 — but it's not minimal:
    - `__action`, `__variables`, `__app`, `__resolutionError`, `__paramData` initialization (5 lines)
    - Per-property backing-field reset
    - `__step`, `__callFrames` capture (used only inside the legacy raw-scalar block — line 179, 180)
    - Marker assignments (Context, Channels, Action, Step, Static)
    - Eager Provider resolution
    - IEvent → context.Event wiring
    - Two validation blocks
    - Final `if (__resolutionError != null) return __resolutionError;` guard
    - `return await Run();`
    Each chunk is small but the line count tells. The "thin" promise from the plan suggests Phase 5 will trim several of these to nothing once legacy/raw-scalar paths are gone.

### Readability

19. **NIT — Multi-line `sb.AppendLine($"...")` cascades make it hard to see the emitted shape.** `EmitExecuteAsync` is 90 lines of `sb.AppendLine` building one method body. A raw verbatim string with `{placeholder}` substitution would read closer to the actual emitted output. Out of scope for this review; flag as future cleanup.

### Verdict: NEEDS WORK
Three concrete dead-emission targets (`__variables`, `__paramData`/`ParamData`, conditional legacy helpers).

---

## PLang.Generators/Emission/Property/this.cs

### Verdict: CLEAN
Abstract record base with two emission slots. 29 lines, no findings.

---

## PLang.Generators/Emission/Property/Data/this.cs

### Behavioral

20. **MAJOR — Line 31 (plain Data getter) reads `Backing` after assigning it but checks `SetFlag` instead of `Backing == null`** — diverges from line 35 (nullable) which checks `Backing == null && !SetFlag` and line 39 (default) which checks `Backing == null`. The plain-Data path:
    ```csharp
    get { if (!SetFlag) { Backing = __ResolveData(...).As<object>(Context); SetFlag = true; } return Backing!; }
    ```
    If a caller does `init { Backing = someValue; SetFlag = true; }` (direct C# composition), the `init` flag was set, so the getter returns the init value — correct. But there's no path that touches `Backing` without flipping `SetFlag`, so functionally it's fine. The inconsistency between branches is a readability bug, not a correctness one.

21. **MINOR — Line 39 default-value branch:**
    ```csharp
    {Backing} = __d.IsEmpty ? new global::App.Data.@this<{InnerType}>("{ParamName}", ({InnerType}){DefaultValue}) : __d.As<{InnerType}>(Context);
    ```
    The `({InnerType}){DefaultValue}` cast is correct for primitives but for enums the `DefaultValue` is already in the form `({Type}){enumNumeric}` (Discovery line 227). So the emitted code becomes `({InnerType})({InnerType}){enumNumeric}` — double cast, harmless but ugly.

### Readability

22. **NIT — Three nearly-identical getter shapes (plain / nullable / default / typed)** packed into a single method with `if/else if/else if/else`. Could split into per-shape emit methods, named after the kind, so the reader sees the four shapes side by side. Today the differences (which lines flip `SetFlag`, which check `Backing == null` vs `!SetFlag`) are easy to miss.

### Verdict: NEEDS WORK
The four-branch getter is the riskiest emission in the system.

---

## PLang.Generators/Emission/Property/Provider/this.cs

### Simplifications

23. **MINOR — Line 19: `engineExpr = ImplementsIContext ? "__app ?? Context?.App" : "__app";`** — see finding 14. With `__app` deleted, this becomes `Context?.App` for IContext implementers and just `Context?.App` (after a small refactor) for the rest. Provider properties are eagerly assigned in ExecuteAsync, so the lazy fallback is the cold path; reaching through `Context.App` is sufficient.

### Verdict: CLEAN
The eager + lazy-fallback pattern is sound. Only depends on finding 14.

---

## PLang.Generators/Emission/Property/Legacy/this.cs

### Verdict: CLEAN (transitional)
Contract-correct; explicitly transitional. Phase 5 deletes this whole file.

---

## PLang/App/Data/this.cs

### OBP Violations

24. **MINOR — Lines 503–510 (`SubstitutePrimitive`) hardcodes Action.@this knowledge into the Data walker (Rule 1 — behavior belongs to the owner).**
    ```csharp
    if (value is @this) return value;
    if (value is Goals.Goal.Steps.Step.Actions.Action.@this) return value;
    if (value is IEnumerable<Goals.Goal.Steps.Step.Actions.Action.@this>) return value;
    ```
    Data is now coupled downward to Action. The architect's v4 plan accepted this as the necessary action-destination carve-out. A cleaner OBP form is a marker interface — e.g., `IDeferredResolution` on `Action.@this` — that Data checks via `value is IDeferredResolution`. Then Action declares "I hold raw %var% for deferred resolution"; Data doesn't import Action.
    - Same finding applies to `IsActionDestination` (lines 512–517).
    - Out of scope for this review (existing pattern, not introduced by v4); log as architectural debt.

### Simplifications

25. **MINOR — `As<T>` line 432–437: typed-fast-path duplicates `ConvertAndWrap`'s fast path:**
    ```csharp
    if (this is @this<T> typed && typed._value is T) return typed;
    if (raw is T already) return new @this<T>(Name, already, _type, Parent) { Context = ctx };
    ...
    return ConvertAndWrap<T>(raw, ctx);  // line 458 — and ConvertAndWrap also does `if (value is T fast) return ...`
    ```
    Lines 432, 436, and the `if (value is T fast)` inside `ConvertAndWrap` (line 463) are three checks of the same predicate. Lines 432 (preserves identity) and 436 (preserves `Name`/`Type`/`Parent`) differ; consolidate into one check at the entry of `AsT_Impl` if possible.

26. **MINOR — `ToBoolean()` (lines 524–539) hand-rolls every numeric type.** `IConvertible`-based or `Convert.ToBoolean(val)` would handle this in one line, falling back to the `IsInitialized && val != null` head check. Less code, less risk of forgetting `BigInteger`/`uint`/`ulong`.

### Behavioral

27. **MAJOR — `AsT_Impl` recursion at line 412 has no cycle detection.** When `%a%` resolves to `"%b%"` and `%b%` resolves to `"%a%"`, `AsT_Impl<T>("%a%", ctx)` → `Variables.Get("a")` → returns Data with `Value="%b%"` → recurse → `Variables.Get("b")` → returns Data with `Value="%a%"` → infinite recursion → `StackOverflowException`.
    - `Variables.Resolve` (line 384–395 in Variables/this.cs) handles this case for partial interpolation via the thread-static `_resolvingVars` HashSet. But `As<T>`'s full-match path (line 405 `Variables.Get`) doesn't go through `Resolve`, so it doesn't get cycle protection.
    - Fix: a thread-static visited-set in `Data` mirroring `Variables._resolvingVars`, or route both paths through `Resolve` and let `Resolve` handle cycles uniformly.
    - Risk level: low in practice (cyclic %var% references are user-introduced and rare), but the architectural promise is "fresh resolution every call" — currently true only when no cycle exists.

28. **MAJOR — `SubstitutePrimitive` and `WalkList`/`WalkDict` only match `IList<object?>` / `IDictionary<string, object?>`** (the typed generic shapes). A non-generic `IList` (e.g. `ArrayList`, `JArray` post-unwrap) or `IDictionary` (e.g. `Hashtable`) silently passes through with no walk and no substitution.
    - `UnwrapJsonElement` (lines 591–620) and `UnwrapNewtonsoftToken` (lines 627–644) normalize most input to the typed forms. So in practice the typed-only check is OK for JSON-derived data.
    - Risk: a handler that constructs a Parameter Data from a non-generic `IList`/`IDictionary` will silently get raw `%var%` strings out the other side. Currently no such handler in the repo, but worth a comment in `SubstitutePrimitive` documenting the shape contract.

29. **MAJOR — `As<T>` ignores `_type`'s `Convert(string)` capability.** If `Data.Type` is "json" (or any type with custom `Convert`), `As<T>` doesn't call `_type.Convert(raw)` — it bypasses to `TypeMapping.TryConvertTo(value, typeof(T))`. The `ConvertValue()` method (lines 233–239) is called only from `Variables.Set`'s dot-path navigation. So a Parameter Data with `Type="json"` and `Value="""{"key":"%x%"}"""` won't honor the JSON conversion through `As<T>`.
    - Potential workflow: handler declares `partial Data<JsonObject> Body { get; init; }` for a property whose .pr value carries a JSON string. The `As<JsonObject>` resolution would need to first JSON-deserialize the string, then walk the deserialized structure for %var% substitution. Today's path skips the JSON step.
    - Status: pre-existing behavior; v4 didn't change this. Flag as Pass-4 fragility. Matrix doesn't cover JSON-typed parameters.

30. **MINOR — `ConvertAndWrap` (line 461–469) constructs a fresh `@this<T>(Name, ...)` with `_type` and `Parent`** carried over but **events** (`OnChange`/`OnCreate`/`OnDelete`) are NOT copied. This is intentional per `ShallowClone`/`Clone` precedent (line 545: "Events are intentionally not copied — clones that go through Variables.Set() get events wired at storage time"). But `As<T>` results don't typically go through `Variables.Set` — they go to the handler's backing field. Behavior is correct for v4 but deserves a one-line comment near `ConvertAndWrap` calling that out.

### Readability

31. **NIT — `Data.@this` partial class is split across multiple files** (`this.cs`, `Data.Result.cs`, `Data.Navigation.cs`, `Data.Envelope.cs` per the file header) but `this.cs` is still 729 lines. The header comment (line 82) lists the split; no other partial files visible in the diff. Either consolidate or follow through on the split.

### Verdict: NEEDS WORK
Cycle-detection gap and the `_type.Convert` skip are the load-bearing concerns.

---

## PLang/App/this.cs

### OBP Violations

(none — `App.Run` is a clean wrapper over the handler call.)

### Simplifications

32. **MINOR — Lines 396–399: `var previousStep = context.Step; var previousGoal = context.Goal; var previousEvent = context.Event;`** then `context.Step = action.Step; context.Goal = action.Step?.Goal;` — but `context.Step.Context = context;` is set conditionally (line 398) only for the new Step. The previous Step's Context isn't restored. If the previous Step's Context was something else, it stays mutated.
    - Practical risk: low — `step.Context = context` is called every time a step is entered, so any stale value gets overwritten on the next entry.
    - Cleaner: set only `context.Step`, `context.Goal`, `context.Event` — leave Step.Context handling to the Step itself or to the original setter.

### Behavioral

33. **MAJOR — `App.Run` catch (line 411) does NOT exclude `OperationCanceledException`** — translates it to `ServiceError`. `Step.RunAsync` catch (line 157 in `Steps/Step/this.cs`) DOES exclude OCE — lets cancellation propagate.
    - This is intentional and load-bearing per `App/modules/timeout/after.cs:39–40`: "Inner action's generated ExecuteAsync swallows OCE into a ServiceError result, so we detect the timeout via CTS state + failed result."
    - But a future maintainer who notices the inconsistency could "fix" App.Run to also exclude OCE, silently breaking timeout.after.
    - **Add a comment in App.Run** at line 411 documenting that the catch DELIBERATELY catches OperationCanceledException and that timeout.after depends on this behavior.

34. **MINOR — Line 415: `serviceErr.Params = handler!.SnapshotParams();` after `handler!`** — the null-forgiving operator hides that `handler` could in theory be null if `Modules.GetCodeGenerated` returned `(null, null)`. The early return at line 384 only fires if `error != null`; if both `handler` and `error` are null (possible per the `(handler, error)` tuple shape), the catch path NREs.
    - Fix: explicit null check after the early return, or change `Modules.GetCodeGenerated` to return non-null `handler` when `error` is null.

35. **MINOR — Line 414: `new Errors.ServiceError(ex.Message, step, callFrames, "ServiceError", 400) { Exception = ex }`** — the magic-string error key `"ServiceError"` is duplicated everywhere this kind of error is constructed. Could be a named constructor (`ServiceError.FromException(ex, step, callFrames)`).

### Readability

36. **NIT — `App.Run` doc-comment (line 372–379) summarizes the dispatch-cake well** but the "Return variable mapping is owned by Action.RunAsync, not here" sentence is a relief — it points readers to where return mapping lives. Keep.

37. **NIT — Lines 386–388: `var step = action.Step;` then `var callFrames = ...`** — inline both into the `catch`/`finally` if they're only needed there (callFrames is only used inside catch and the no-frames fallback in finally). Today both are bound at the top of try, used in catch — valid, but adds two locals for one error path.

### Verdict: CLEAN (with finding 33 marked as documentation gap)

---

## PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs

### Behavioral

38. **NIT — `GetParameter` (line 129) doesn't actually use the `context` parameter.** Doc comment (line 126) explicitly notes it's "kept as a hook even though today's lookup is context-free." Fine; symmetry with `As<T>(context)` justifies it. But Reslyn / IDE warnings about unused parameters may fire — suppress if needed.

### Verdict: CLEAN

---

## PLang/App/modules/ICodeGenerated.cs

### Verdict: CLEAN
Three-method interface with sensible default for `SnapshotParams`. No findings.

---

## PLang/App/Variables/this.cs

### Verdict: CLEAN
`ResolveDeep` and supporting state cleanly removed. No new findings introduced by v4 here.

---

## PLang/App/Debug/this.cs

### Verdict: CLEAN
`OnResolveTrace` reference cleanly removed. Mutation logging now uses `RawValue?.GetType()` instead of the deleted `NeedsResolution` flag — appropriate substitute.

---

## Summary table

| # | Severity | File | Finding |
|---|----------|------|---------|
| 1 | MAJOR | Discovery/this.cs:281 | `ActionClassInfo` is `class`, not `record` — incremental cache always misses |
| 7 | MAJOR | Discovery/this.cs:35-36 (in this.cs) | Diagnostic location synthesized as 1-char span instead of using `IPropertySymbol.Locations[0]` |
| 11 | MAJOR | Emission/Action/this.cs:79,122 | `__variables` field dead — declared, set, never read |
| 12 | MAJOR | Emission/Action/this.cs:91-97,230 | `__paramData` filled but never read; `ParamData()` accessor unused across repo |
| 18 | MAJOR | Emission/Action/this.cs:116-207 | "Thin" ExecuteAsync still does six things — not minimal until Phase 5 |
| 20 | MAJOR | Emission/Property/Data/this.cs:31,35,39 | Inconsistent `SetFlag`/`Backing == null` checks across the four getter shapes |
| 27 | MAJOR | Data/this.cs:412 | `AsT_Impl` recursion has no cycle detection — `%a%↔%b%` cycles stack-overflow |
| 28 | MAJOR | Data/this.cs:500-501 | `WalkList`/`WalkDict`/`SubstitutePrimitive` only match generic typed shapes — non-generic `IList`/`IDictionary` silently pass through |
| 29 | MAJOR | Data/this.cs:383-388 | `As<T>` ignores `_type.Convert` capability (e.g., JSON-typed Data) |
| 33 | MAJOR | App/this.cs:411 | `App.Run` catch does NOT exclude OCE — intentional (timeout.after depends on it) but undocumented |
| 2 | MINOR | Discovery/this.cs:134,192 | `OriginalDefinition.Name == "@this"` branch is dead |
| 3 | MINOR | Discovery/this.cs:194-197 | Triple `prop.Type as INamedTypeSymbol` cast — extract local |
| 4 | MINOR | Discovery/this.cs:124-138 vs 146-214 | Two parallel classifiers (`IsValidActionProperty` + `BuildProperty`) — fragile |
| 6 | MINOR | Discovery/this.cs:44 | `RawScalarPropertyDescriptor` should be `internal` |
| 8 | MINOR | this.cs:31-37 | Synthetic `Location.Create` from line/char instead of using upstream `Location` |
| 9 | NIT | this.cs:14 | Class still named `LazyParamsGenerator` post-restructure |
| 13 | MINOR | Emission/Action/this.cs:81,178,204,231 | `__resolutionError` dead for v4-shape handlers (transitional) |
| 14 | MINOR | Emission/Action/this.cs:80,123-124 | `__app` and `app` are two names for one thing |
| 15 | MINOR | Emission/Action/this.cs:219-268 | Legacy helpers emitted unconditionally even when no LegacyProperty |
| 16 | MINOR | Emission/Action/this.cs:173-187 + 190-202 | Two near-duplicate validation blocks |
| 17 | MINOR | Emission/Action/this.cs:130-138 | Backing-field reset gated by `if (action != null)` while siblings unconditional |
| 21 | MINOR | Emission/Property/Data/this.cs:39 | Double cast `({InnerType})({InnerType})...` for enum defaults |
| 23 | MINOR | Emission/Property/Provider/this.cs:19 | Lazy fallback expression depends on `__app` |
| 24 | MINOR | Data/this.cs:503-517 | `SubstitutePrimitive` couples Data to Action — marker interface preferable |
| 25 | MINOR | Data/this.cs:432-458,463 | Typed-fast-path duplicated three times |
| 26 | MINOR | Data/this.cs:524-539 | Hand-rolled numeric ToBoolean — `IConvertible` simpler |
| 30 | MINOR | Data/this.cs:461-469 | `ConvertAndWrap` doesn't copy events — intentional, undocumented |
| 32 | MINOR | App/this.cs:396-399 | Save/restore Step doesn't restore `Step.Context` |
| 34 | MINOR | App/this.cs:415 | `handler!` null-forgiving hides a real possible NRE |
| 35 | MINOR | App/this.cs:414 | Magic-string `"ServiceError"` repeated — factory method desirable |
| 5 | NIT | Discovery/this.cs:146-214 | `BuildProperty` — 70-line cascade |
| 19 | NIT | Emission/Action/this.cs (whole) | `sb.AppendLine` cascades for emission — verbatim string would be clearer |
| 22 | NIT | Emission/Property/Data/this.cs:20-49 | Four-branch getter — split into named emit methods |
| 31 | NIT | Data/this.cs (header) | Partial-class split documented but not realized |
| 36 | NIT | App/this.cs:372-379 | App.Run doc-comment is good — keep |
| 37 | NIT | App/this.cs:386-388 | Locals could move into catch/finally |
| 38 | NIT | Action/this.cs:129 | `GetParameter` doesn't use `context` parameter (intentional hook) |

**Counts:** 10 MAJOR, 19 MINOR, 9 NIT = 38 findings.

**Top three to fix before merge:**
- Finding 1 — `ActionClassInfo` should be a record with value-equal collections (incremental cache contract)
- Finding 11 — delete `__variables` field
- Finding 12 — delete `__paramData` + `ParamData()` accessor

**Top three behavioral concerns:**
- Finding 27 — cycle detection in `AsT_Impl`
- Finding 28 — non-generic `IList`/`IDictionary` silently pass without substitution (document the shape contract at minimum)
- Finding 33 — comment App.Run's deliberate OCE catch

**Verdict: NEEDS WORK** — the v4 design is sound and the move from `Data.Value` getter side-effects to `Data.As<T>(context)` is a real architectural improvement. But the IIncrementalGenerator value-equality contract is not delivered (finding 1), and there are two clearly dead emission slots (`__variables`, `__paramData`). Cycle-detection in resolution is a latent bug. None block merge if Phase 5 will land soon, but they should be addressed.
