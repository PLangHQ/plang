# Codeanalyzer v1 — Findings

Branch: `runtime2-builder-bootstrap`
Scope: 14 highest-risk C# files, ~2k lines deep + ~12 lighter scans.
Date: 2026-04-27

## TL;DR

The new builder/diagnostics work is solid in **architecture** — the Catalog/PlangType system, the per-build trace, the ParamSnapshot wiring, the typed BuildResponse, the Examples-via-reflection renderer all reduce drift. But several pieces violate the team's stated rules:

- **3 places throw from `Try*` methods or swallow `Exception` silently** (TypeConverter, Variables.Set dict-clone path).
- **A live diagnostic probe still walks every save** in DefaultBuilderProvider (DIAG block, lines 174–198).
- **Two key-name heuristics** stand on names that user code can collide with (`IsDeferredActionTemplate`, `IsClrTypeName`).
- **Clone/CreateChild family** in `Actor.Context.@this` diverges, with new properties not propagated.
- The Reset() path on `PlangTypeIndex` is incomplete.

Verdict: **NEEDS WORK** — none are show-stoppers, but several stack on each other in central code paths.

---

## PLang/App/Utils/PlangTypeIndex.cs (NEW, 196 lines)

### OBP
1. **Line 24: Static class registry — acceptable.** Type identity is genuinely cross-type. The class doesn't violate OBP, but it's a centralizing static that the rest of the system depends on; document the threading rules clearly (see #2 / #3 below).

### Simplifications
1. **Lines 116–125: `Reset()` is incomplete.** It clears `_nameToType` and `_typeToName` but not `_runtimeNameToType` and not `_clrTypeFullNames`/`_clrTypeFullNamesInitialized`. Tests calling `Reset()` get a half-clean state. Per memory's *Clone/Copy family* note: when fields multiply (this class went from one cache to three), every reset method must cover all of them.
   - Fix: Reset all four fields; or reduce to a single named cache with a single reset.

### Behavioral reasoning
1. **Lines 36–38, 55–70: `_clrTypeFullNames` is a `HashSet<string>` accessed under classic DCL.** `_clrTypeFullNamesInitialized` is a plain `bool`, not `volatile`. The double-checked lock at line 57 reads the flag without a lock and assumes the HashSet is fully populated when the flag flips. On x86 with strong memory ordering this works in practice; on ARM (M1, ARM64 servers) the JIT can hoist the flag read past the HashSet read. Same pattern in `EnsureInitialized` for the `ConcurrentDictionary` paths — those at least use thread-safe types.
   - Fix: `volatile bool _clrTypeFullNamesInitialized` (or `Interlocked.MemoryBarrier()` after the population) or switch to `ConcurrentDictionary<string, byte>` so reads are safe regardless of the flag.
2. **Line 34: `public static List<Assembly> Assemblies { get; }` — mutable, no thread safety, no doc on lifecycle.** A caller can mutate this any time, but `EnsureInitialized` only reads it inside the init lock — additions made after first init are silently ignored until `Reset()` (which doesn't reset CLR-name cache). Easy footgun.
   - Fix: document "extend only before first use, or call Reset() after"; or make it a method `RegisterAssembly(asm)` that invalidates init.
3. **Lines 36–53: `IsClrTypeName` snapshot is taken on first call to AppDomain.GetAssemblies().** Plugins or test harnesses that load assemblies after first call will not be checked. Acceptable for the current usage (build-time guard) but worth a comment.

### Verdict: NEEDS WORK
The DCL pattern + incomplete Reset are real bugs that would only surface intermittently (cache state, ARM hardware). The class is otherwise well-scoped.

---

## PLang/App/Utils/TypeConverter.cs (NEW, 394 lines)

### OBP
1. **Line 14: Static helper class.** Conversion is cross-type — `Data.@this`, primitives, JsonElement, dicts, IObject, GoalCall, enums all flow through this one method. A static helper is the only viable shape. Acceptable.
2. **Line 35: `Populate(target, dict)` reflects into the target's properties.** This is a "navigate, don't pass" violation in spirit — the caller hands over ownership of property writes to a util. Acceptable at JSON boundaries; concerning if called on domain objects (Goal, Step, …). Recommend grepping callers and confirming usage stays at the deserialization edge.

### Simplifications
1. **Line 174–238: IObject path and "string ctor" path overlap.** Both find a single-string constructor and call it; IObject does so on a marker interface, the second on any type. Could merge — the IObject branch's behavior (validValues hint on failure) could be conditional on the marker, with one ctor lookup.

### Readability
1. **Lines 145–151: List conversion returns `(partialList, error)` when *some* elements failed.** Confusing return shape — caller has to check both. Currently `LazyParamsGenerator`'s resolve treats `error != null` as failure, so the partial list is dead weight. Either always return `(null, error)` or document "partial result for diagnostics".

### Behavioral reasoning (the meat)
1. **Lines 83–104: BARE `catch { }` swallows every exception.** Catches anything from `JsonSerializer.Deserialize` — JsonException, OutOfMemoryException, ThreadAbortException — without recording the cause. Then attempts an array-fallback parse with another silent `catch (JsonException) { }` at line 101. If both fail, control falls through to other rules (List handling, etc.) with no record of WHY the parse failed. The user sees a confusing downstream error far from the root cause.
   - Per memory `feedback_silent_error_critical.md`: silent error swallowing is **CRITICAL**, never medium.
   - Fix: `catch (JsonException) { ... }` for the first try (specific); the inner array-fallback should also surface its failure. Carry the original JsonException's message into a `(null, Error)` if BOTH parses fail; only fall through to other rules when the input demonstrably isn't JSON (e.g., doesn't start with `{` or `[`).
2. **Line 190: `catch (System.Exception ex)` on `ctor.Invoke([strVal])`.** Catches everything including OOM/StackOverflow/ThreadAbort. Other catches in this file consistently use the `when (ex is not (NullReference | OOM | StackOverflow))` filter — this site forgot it.
   - Fix: add the same filter pattern.
3. **Lines 261–313: `TryConvertTo` THROWS `InvalidOperationException`** at lines 266 and 296 when a CLR type name leaks into a `GoalCall.Name`. The method's contract is `(value, Error)` — it should never throw. Two consequences:
   - Callers wrap `TryConvertTo` in `try { Resolve<T>() }` patterns expecting Errors, not exceptions. The throw bypasses the framework's structured error handling — the user sees a stack trace with `at TypeConverter.TryConvertTo` instead of a PLang-formatted error with step/goal/call-frames.
   - Per memory `feedback_catch_scope.md`: "Methods returning Data must return Data on every code path — never throw."
   - The intent (a defensive tripwire for a known leak path) is sound. Implement as `return (null, new Errors.Error(message, "ClrTypeNameInGoalSlot", 500) { FixSuggestion = "..." });`. Caller's error-handling pipeline shows it like any other validation failure.
4. **Line 56: `targetType.IsValueType ? Activator.CreateInstance(targetType) : null` for null input.** Silently returns the default value for a value type when given null — `null → int` becomes `0`. No error, no flag. If a caller's parameter is non-nullable `int` but the .pr value resolves to `null`, the handler runs with `0` and no warning. Document this as intent, or return an error and let the caller decide.
5. **Lines 91–102: "If target is a single object but JSON is an array, take first."** Silently discards the rest of the array. A user passing `[{a:1},{b:2}]` to a `Goal` parameter loses item 2 with no record. This is a silent data loss — surface it as a warning at minimum, or fail with "expected single object, got array of N".

### Deletion test
- Line 332–361 complex-types path is reachable only after all earlier branches fall through. Verify with a trace whether the IList overlap with the list-handling branch above (line 126) can ever leave coverage gaps.

### Verdict: MAJOR ISSUES
The bare `catch`, the throw-from-Try, and the silent first-element-of-array are systemic anti-patterns that the rest of the codebase actively avoids. Three independent fixes needed.

---

## PLang/App/Utils/TypeMapping.cs (554 lines, heavily reworked)

### OBP
1. **Lines 314–328: TypeMapping forwards 4 methods to TypeConverter.** The split docs say "TypeMapping = identity, TypeConverter = conversion" but TypeConverter calls back into `TypeMapping.IsPrimitive` (line 317) and `TypeMapping.GetValidValues` (line 193). Circular dependency between siblings — and the forwarders are dead noise after the migration. Either delete the forwarders and update callers, or fold one into the other.

### Simplifications
1. **Line 264: stray blank line inside `ConvertToDictionary`** (the method is in this file's diff but actually lives in `Variables/this.cs`). Trivial — drop it.
2. **Lines 165–184: Mega-list of generic-type type names** (`List<>`, `IList<>`, `IEnumerable<>`, `ICollection<>`, `IReadOnlyCollection<>`, `IReadOnlyList<>`, `HashSet<>`, plus FullName.StartsWith string comparisons for ImmutableList`, ISet`, ConcurrentDictionary, ReadOnlyDictionary, SortedDictionary, ImmutableDictionary). This is duplicated logic across the list-rendering and dict-rendering branches.
   - Per memory `feedback_fix_at_right_level.md`: "hand type dispatch / attribute checks in app code = reimplementing framework machinery." The right call is one helper: `IsListLike(type, out elementType)` and `IsDictLike(type, out k, out v)` that checks `IList<>` / `IDictionary<>` interfaces.
3. **Lines 218–223: Two GetProperty("ValidValues") lookups** — once at line 218, once at line 257. Same convention check. Pull into a single helper.

### Behavioral reasoning
1. **Lines 159–185: Generic-type matching uses `type.IsGenericType && genericDef == typeof(...)`.** The `FullName.StartsWith` checks for ImmutableList/ImmutableDictionary etc. are string-based — the safer form is `typeof(IImmutableList<>).IsAssignableFrom(generic)` or interface check on `IList<>` / `IDictionary<>` (which is what lines 206–209 already do for the IList<T> case). Two ways of doing the same thing in the same file.
2. **Lines 276–300 `IsScalarPlangType`:** the fallback at lines 294–298 says "a [PlangType] type with no [LlmBuilder] properties is a wrapped primitive". Reasonable convention but it's another rehydration heuristic — adding/removing one [LlmBuilder] silently flips a type from Scalar to Record in the catalog. Comment is good; consider making it explicit by requiring `[PlangType(Shape="...")]` for scalars rather than inferring.

### Verdict: NEEDS WORK
Mostly cleanup. Forwarders should go; the generic-type detection should consolidate.

---

## PLang/App/Catalog/this.cs (NEW, 127 lines)

### OBP — clean
The catalog owns its serialization (`ToJson`), its derived views (`TypeNames`, `TypeSchemas`), and its construction (`Build`). Good "owner of behavior" pattern. Comment at line 17 ("OBP: the catalog is a real object…") is exactly right.

### Simplifications
1. **Lines 42–89: `TypeSchemas` getter is 47 lines of StringBuilder rendering inside a property accessor.** Computed every access (no caching). Move to a `Render()` method named for what it is (the property name suggests a field, the implementation is a builder); cache the result if hot. If only the prompt template calls it, leave it but rename.
2. **Lines 50–86: branch tree on `t.Kind` is duplicated knowledge.** Each TypeKind (Enum/Record/Scalar) is rendered in its own branch. If a fourth kind is added, this and `TypeEntry` and `BuildTypeEntries` all need updating. Acceptable today, but consider an instance method `t.RenderInline(sb)` so a new kind is one place not three.

### Verdict: CLEAN
Minor structural tightening. No bugs.

---

## PLang/App/Catalog/ExampleRenderer.cs (NEW, 184 lines)

### OBP
1. **Static class — could be `ExampleSpec.Render(modules)`.** Right now callers write `ExampleRenderer.Render(spec, modules)` — extracted from the data. Per OBP rule 1, "behavior belongs to the owner": the spec owns its rendering. As an instance method on `ExampleSpec`, it'd be `spec.Render(modules)`. Same code, better OBP.
   - That said, `ExampleSpec` is a `record` (immutable shape) and `ActionSpec` is also a record — they may want to stay pure-data DTOs. Static renderer is a reasonable compromise. Flag for awareness.

### Simplifications
1. **Lines 70–106 vs 113–145 vs 147–159: three distinct value-formatting paths** with overlapping logic for ActionSpec, IEnumerable, ActionSpec[]. Could unify around a single `RenderValue(value, format)` helper where `format ∈ {Formal, JsonRecord}`.

### Behavioral reasoning
1. **Lines 91–101: `value is IEnumerable enumerable && value is not string` then enumerate** — but a `Dictionary<string, object>` is also `IEnumerable<KeyValuePair<…>>`. If a Dict<string, ActionSpec> is passed, this code will iterate KVPs and check `items.All(x => x is ActionSpec)` — KVP isn't ActionSpec, so falls through to JSON. OK but worth a `value is not IDictionary` guard for clarity.
2. **Line 105: `if (value is System.IConvertible) sb.Append(value)`** — IConvertible includes string, but string was handled at line 74. So this path is non-string IConvertible: numbers, bools, dates. `sb.Append(value)` calls `value.ToString()` with the current culture — locale-sensitive. For numbers in a JSON-adjacent format, you want `InvariantCulture`. Probable bug under non-en-US locale.
   - Fix: `sb.Append(System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))`.

### Verdict: NEEDS WORK
The culture issue at line 105 is real. The OBP nudge is style.

---

## PLang/App/modules/builder/validateResponse.cs (188 lines, 192 changed)

### OBP — clean
The action handles its own validation, owns its `ValidateGoalState` static (used by SaveGoal), and uses typed `BuildResponse` end-to-end. No JsonElement forks. Good.

### Simplifications / Readability
1. **Lines 35–60: defensive null-branches on `[IsNotNull]` parameters.** If the framework's `[IsNotNull]` validator runs before `Run()`, this branch is dead. If it doesn't, the attribute name is misleading. Worth deciding which: either delete this block (and trust the validator), or remove `[IsNotNull]` (and document the param can be null, requiring this defensive code).
2. **Line 187: `StepValidationExt.With<T>` extension method** is defined at file scope but used only inside `Validate`. Should be a private static helper inside the class — extension methods leak into other consumers' IntelliSense.
3. **Lines 82–88 `CopyActionsIfAny`** is a 3-liner that exists only to be called by `With(target => CopyActionsIfAny(...))`. Inline it: `new Step{...}.With(t => { foreach(var a in s.Actions) t.Actions.Add(a); })` — or drop the `With` pattern entirely and use a simple object initializer + loop.

### Behavioral reasoning
1. **Lines 91–104: auto-fills missing indexes with `keep:true` placeholders BEFORE validation.** The auto-fill mutates `response.Steps`. If validation later fails, the response sent back to the LLM (via LlmFixer) has been silently mutated — the LLM's "fix this" prompt now contains synthesized placeholders that were never in the original LLM output. This may confuse the fixer.
   - Fix: either auto-fill on a copy, or document explicitly in the fixer prompt that placeholders may be present.
2. **Lines 132–146: index-gap check** runs only when `indexes.Count == response.Steps.Count` (always true at this point because indexes was just filled from response.Steps). The guard is dead.

### Verdict: NEEDS WORK
Small tightening. No critical issues.

---

## PLang/App/modules/error/handle.cs (201 lines, 157 changed)

### OBP — mostly clean
The handler owns its examples (via `ExamplesForLlm()`), its filter logic, and its retry. Good.

### Behavioral reasoning
1. **Lines 99–112: RetryFirst path discards recovery's value but GoalFirst keeps it.** Compare:
   - Line 96 (GoalFirst): `if (recoveryResult.Success) return recoveryResult;` — keeps the recovery's data.
   - Line 109 (RetryFirst): `if (recoveryResult.Success) return global::App.Data.@this.Ok();` — drops the recovery's value, returns bare `Ok()`.
   This is asymmetric. Either intentional ("when retry is the headline, recovery is just suppression") and worth a comment, or a copy-paste bug.
2. **Lines 184–199 `Retry`:** `count` is checked at line 187 (`if (count == null || count <= 0) return null;`); but line 190 uses `RetryOverMs.Value / count.Value` — count could still be 0 if it equals exactly 0 there? No, the guard rules out 0. OK. But the redundant `count > 0` check at line 189 (`RetryOverMs?.Value != null && count > 0`) is dead — `count > 0` is already guaranteed.
3. **Lines 173–179 `MatchesError` filter logic:** if `StatusCode?.Value` is `0`, `is int sc` matches with `sc=0`, and check fires. Is StatusCode=0 ever meaningful? Probably not. Treat 0 as "no filter" for parity with empty Key/Message handling.

### Simplifications
1. **Line 88: `var actions = Actions?.Value;`** then null-coalesce at line 89, then `actions!` on lines 95 and 108. The `!` is forced because `hasRecovery` is in a separate variable — combine into `if (Actions?.Value is { Count: > 0 } actions)` and the `!` go away.
2. **Line 115: `if (IgnoreError.Value) return …Ok();`** — IgnoreError defaults to false (line 74 attribute). Property defaults work because of source generator. Fine.

### Verdict: NEEDS WORK
The asymmetric return at line 109 is the one to investigate.

---

## PLang/App/Actor/Context/Trace/this.cs (NEW, 32 lines)

### Verdict: CLEAN
Two readonly properties initialized in the ctor, no methods. Minimal footprint, accurately documented. Per memory `feedback_disposal_lifecycle.md` — Trace has no resources to dispose, and Context.Dispose doesn't need to touch it. OK.

---

## PLang/App/Errors/ParamSnapshot.cs (NEW, 27 lines)

### Verdict: CLEAN
Pure data record, well-documented. The `WasAccessed` distinction (vs FinalValue==null) is the kind of subtlety that earns its place — it answers "was the lazy backing field ever set?" without adding ambiguity.

### One minor note
- `PrValue`, `PrType`, `FinalValue` are all `object?`. JSON-serializing this record with the rest of an Error may explode if `PrValue` holds a non-serializable runtime object (e.g., a closure, a Stream). Consider an explicit `ToString()` projection at snapshot time, or rely on `Json.PrWrite` to skip cycles.

---

## PLang/App/Attributes/PlangTypeAttribute.cs (NEW, 65 lines)

### Verdict: CLEAN
Standard attribute. `AllowMultiple = true` plus `[AttributeUsage(Class | Struct | Enum)]`. Properties cleanly separated (Name canonical vs. aliases, Shape/Example/Description for catalog teaching). Documentation block at the file level is one of the most useful in the diff.

---

## PLang/App/modules/builder/{BuildResponse,enrichResponse,ActionSpec,ExampleSpec,TypeEntry,ExampleHelpers,MimeTypes}.cs

### Verdict: CLEAN
DTOs and small static helpers. `ExampleHelpers.Action(...)` correctly throws `ArgumentException` for malformed `module.action` input — consistent with builder authoring convention. `MimeTypes` is a pure switch table. `BuildResponse` minimal mirror of the LLM JSON schema. No findings.

---

## PLang.Generators/LazyParamsGenerator.cs (49 changed)

### OBP — clean
The generator emits per-handler code; no behavior decisions live in it.

### Simplifications
1. The emitted `__SnapshotParams` method (lines 685–705 of the new code) hard-codes the `prop.Name` literal twice via `Name = "{prop.Name}"`. Consider building a `nameof`-equivalent at gen time.

### Behavioral reasoning
1. **Generated `data.ResetResolution()` after `data.Context = Context; data.NeedsResolution = true;`** — needed because the .pr Parameter Data is shared across action executions. Good fix per the comment. But this means `ResetResolution` is on a hot path (every action call). Verify it's O(1) — looking at Data.cs, it sets `_value = _rawValue; _resolved = false;` — yes O(1). OK.
2. The catch site at lines 540–547 of the gen output catches `System.Exception` — same Exception-filter concern as TypeConverter line 190. The generator emits the base catch with no filter for `OOM | StackOverflow | ThreadAbort`. Per memory: this is pervasive enough that the generator should emit the same `catch when (...)` shape used by hand-written code.

### Verdict: NEEDS WORK
The emitted catch shape should match the rest of the codebase's exception filters.

---

## PLang/App/Variables/this.cs (106 changed)

### Behavioral reasoning
1. **Lines 152–170 in the diff (snapshot-clone path for dict/list rawValue):**
   ```csharp
   try { /* serialize→deserialize round-trip */ }
   catch { /* fall through with alias risk */ }
   ```
   - **Bare `catch`** on a `JsonSerializer.Serialize/Deserialize` round trip. The comment acknowledges "alias risk but better than throwing" — but this is exactly the bug that the round-trip was added to fix (per the comment "this bit the builder trace: trace.pass1 aliased %currentPass%._value"). Silent fallback to the broken behavior reproduces the original bug if serialization fails for any reason (cycle, non-serializable type).
   - Fix: catch `JsonException | NotSupportedException` specifically; on failure, log and continue with alias risk OR throw — but at minimum surface the failure so the bug repro path is debuggable.
   - Per memory `feedback_silent_error_critical.md`: silent error swallow is critical, and per `feedback_catch_scope.md`: never use generic catch in wrapper methods.
2. **Lines 152–170 (continued): JSON round-trip on every dot-path Set with dict/list value** is expensive. For a deeply nested set (`%foo.bar.baz% = bigDict`), this serializes and deserializes the entire dict every set. Consider only cloning when the source is shared (heuristic: same object referenced elsewhere) — or document the perf trade-off.
3. **Line 478 (in the existing method, modified): builder-mode short-circuit on `_context?.App?.Building?.IsEnabled == true return value;`.** Reaches deep through the object graph (`_context.App.Building.IsEnabled`). Each level is `?.` — null-safe. But three levels of indirection just to read a flag is fragile to navigation changes. Consider a `_context.IsBuilding` shortcut on Context.
   - More importantly, the comment is excellent — explains *why* (source code in step.Text would be resolved as runtime expressions and break the build). This is exactly the right context for a comment to carry.

### OBP
1. **Lines 195+ (snapshot-clone path) reach into the value's structure** — IDictionary, IList type checks. This is unavoidable for the use case but worth flagging.

### Verdict: NEEDS WORK
The bare catch in the snapshot-clone path is a critical issue per project memory.

---

## PLang/App/Modules/this.cs (105 changed)

### OBP — clean
Module describes itself, owns its action discovery, owns the prior `Examples` extraction. Capability-interface filtering (lines 173–186) is a tidy improvement over hard-coding "Context" and "EqualityContract".

### Simplifications
1. **Lines 172–183 `CapabilityInterfaces` array** — five hardcoded interface types (`IContext`, `IStep`, `IChannel`, `IEvent`, `IStatic`). If a sixth capability interface is added, this array must be updated. Consider: scan for any interface in the `App.modules` namespace that ends with the pattern, or use a marker `[CapabilityInterface]` attribute. The current form works but is one more thing to remember when adding a capability.

### Behavioral reasoning
1. **Lines 240–262 `examplesForLlm` lookup via reflection** on every action of every module on every Describe() call. Describe() is called once per build (cached at builder startup) — fine. But verify it's not called per-step.
2. **Line 276 `result.Add(new Action.@this { …, IsModifier = isModifier })`**: a new property on Action.@this. Per memory's *Clone/Copy family* — does Action.@this have a Clone? Yes, look at any existing copy methods and verify IsModifier is propagated.

### Verdict: CLEAN
Minor improvements. The clone audit is a "verify and confirm".

---

## PLang/App/modules/llm/providers/OpenAiProvider.cs (79 changed)

### Behavioral reasoning
1. **Line 138 (was `t.Description ?? ""`, now hardcoded `""`):** OpenAI tool descriptions are now always empty. Either:
   - The tool description was unused in practice and `t.Description` should be removed from the type → the change is fine but the upstream property should also be cleaned up;
   - OR this is a regression and tool descriptions should still be sent.
   No comment explains why. Investigate.
2. **Lines 224–252 `finishReason` check.** New, correct, and well-shaped — surfaces the root cause as a structured Error with rich Details. Good. The `ResponseTruncated` key with explicit "Raise MaxTokens or shorten the prompt" suggestion is the kind of message that saves debugging time.

### Simplifications
1. **`SerializeSchema` local function** (lines ~73–82) — defined inside `Run`, used three lines later. Consider top-level static.

### Verdict: CLEAN
The truncation handling is a clear improvement. Investigate the empty description.

---

## PLang/App/modules/ui/providers/FluidProvider.cs (66 changed)

### Behavioral reasoning
1. **Lines 121–141 `FormatFormalValue` and `UnwrapFluid`:** both static, both single-purpose. UnwrapFluid recurses through Fluid wrappers — correct. FormatFormalValue mirrors `ExampleRenderer.RenderValueFormal` (in Catalog/) which mirrors `DefaultBuilderProvider.FormatValue` (in builder/providers). **Three places implementing the same formal-syntax format.**
   - Per memory `feedback_fix_at_right_level.md`: "reflection walkers / hand type dispatch in app code = reimplementing framework machinery." Three separately-maintained renderers will drift.
   - Fix: extract into one shared `FormalSyntaxWriter` (probably in App.Catalog or App.Utils), call it from all three sites.
2. **Line 138 (FormatFormalValue): `if (v is IConvertible) return v.ToString() ?? "";`** — same culture-sensitive `ToString` issue as `ExampleRenderer.cs` line 105. Use `InvariantCulture`.
3. **Line 140 `try { JsonSerializer.Serialize(v) } catch { return v.ToString() ?? ""; }`** — bare catch, silent fallback to ToString. Same anti-pattern as the others. Catch `JsonException | NotSupportedException` specifically.

### Verdict: NEEDS WORK
The triple-implementation of formal syntax is a maintenance hazard now and a guaranteed drift later.

---

## PLang/App/Actor/Context/this.cs (49 changed)

### Behavioral reasoning — Clone/Copy family audit
1. **Lines 263–286: Clone() vs CreateChild()** propagate different state.
   | Property        | Constructor | CreateChild | Clone |
   |-----------------|:-----------:|:-----------:|:-----:|
   | App             | yes         | yes         | yes   |
   | Variables       | yes         | yes (new)   | yes (new) |
   | Parent          | yes         | yes (this)  | yes (Parent) |
   | IsAsync         | no          | NO          | yes   |
   | Setup           | no          | NO          | yes   |
   | ConfigScope     | no          | NO          | yes   |
   | _data           | no          | NO          | yes   |
   | Trace           | new         | new         | new   |
   | Error           | no          | no          | no    |
   | Test            | no          | NO          | NO    |
   | EventOverride   | no          | no          | no    |
   | Goal/Step/Actor | no          | no          | no    |

   - **Clone copies `IsAsync, Setup, ConfigScope, _data` but CreateChild does not.** Both create a "new context based on this one." If the divergence is intentional, document it. Per memory `feedback_disposal_lifecycle.md` and the broader Clone-family pattern: when properties get added (Trace, Error, Test in this diff), every copy method has to be re-evaluated.
   - Particularly suspicious: **`Test` is set on neither Clone nor CreateChild**. The doc on line 122 says "Set when --test flag is active." If a sub-goal runs in a child context, it loses `%!test%`. Verify this works as intended via `TestFile.cs`.
2. **Line 297: `_wrapperCache.GetOrAdd(key, _ => factory())` + cast** — if a caller asks for `Data<Goal>` for a key that's already cached as `Data<Step>`, the cast at line 299 throws `InvalidCastException`. The cache uses the source object as key (line 298), so reusing the same domain object as different `T` is the only way to trip this — unlikely but possible.
   - Per memory: such silent assumptions break under user data. Either type-check before cast and recreate, or use `(T, Type)` keys.

### Simplifications
1. **Lines 165–189 `RegisterContextVariables`:** 13 `vars.Set(new DynamicData(...))` lines. Consider an array of `(name, factory)` tuples and a single loop. Cosmetic.

### Verdict: NEEDS WORK
The Clone/CreateChild divergence is a Clone/Copy family hazard per project memory. The wrapper-cache cast is a latent footgun.

---

## PLang/App/Errors/Error.cs (49 changed)

### OBP — clean
Behavior-on-owner. Error owns its formatting (Format/FormatError/FormatVerboseValue). Subclasses extend via FormatExtra.

### Behavioral reasoning
1. **Lines 281–296 `FormatVerboseValue`:** has a `try { JsonSerialize } catch { return value.ToString() ?? "?"; }` (line 287–292). Specific to Dict/List path, but the catch is bare. Consistent anti-pattern with the rest of the diff.
2. **Lines 207, 234: nested casts `error is Error` checks.** Since `IError` is the interface and `Error` is the only known impl, this works. But adding a second IError implementation will silently miss the Params/Context blocks. Consider promoting `Params` and `Context` to `IError` (read-only) so polymorphism handles the path.

### Simplifications
1. **Lines 113–125 `Format()` builds string then trims at end.** `sb.AppendLine` adds CR/LF; trim is fine but a single `string.Join("\n\n", ...)` between sections would be clearer for readers.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs (52 changed)

### OBP — clean

### Behavioral reasoning
1. **Lines 134–143 (override path):** when `beforeResult.Handled` is true, the code uses beforeResult as the result and clears `Handled = false`. Comment is excellent. The mutation of `result.Handled` MAY affect callers that expect Data values to be immutable. Confirm whether Data.@this.Handled is intended to be mutable through the action chain.
2. **Line 154–156: `context.Variables.Set("__data__", result);`** — direct alias, no clone. Comment explains "Same object is reachable via both %__data__% and whatever name the producing handler owns." Good. But: the next action in the chain reads `__data__`; if it modifies the contained value, the producing handler's stored Data sees the modification. If that's intentional (a pipeline pattern), it works. If actions are expected to receive immutable inputs, this is shared-mutable-state.

### Verdict: CLEAN
The override-path comment is exactly the kind of WHY-comment that earns its place.

---

## PLang/App/Data/this.cs (39 changed)

### OBP — clean

### Behavioral reasoning
1. **Lines 519–531 `IsDeferredActionTemplate`:** key-name detection on the PLang type name string ("action" / "list<action>"). Two failure modes:
   - User module declares `[PlangType("action")]` on its own type → false positive, eager resolution skipped where it shouldn't be.
   - The canonical name changes (e.g., "Actions" plural for the collection alias) → false negative, ResolveDeep destroys the template.
   - Per memory: "Key-name-based detection breaks with user data."
   - Fix: a marker attribute on the parameter property (`[DeferredResolution]`) or a structural check (target CLR type IS `Action.@this` or `List<Action.@this>`). The structural form catches the case at the source-generator level — type identity, not string identity.

### Simplifications
1. **Lines 217–225: triple negation: `if (NeedsResolution && !_resolved && _value != null && _context?.Variables != null && (...) && !IsDeferredActionTemplate(_type))`.** Six conditions in one if. Extract `bool ShouldResolve(out container)` for readability.

### Verdict: NEEDS WORK
The key-name heuristic is fragile. Replace with structural detection.

---

## PLang/App/modules/builder/providers/DefaultBuilderProvider.cs (339 changed)

### Behavioral reasoning — the biggest single finding
1. **Lines 174–198 (DiagGoal): a live diagnostic probe.**
   ```csharp
   void DiagGoal(App.Goals.Goal.@this g)
   {
       foreach (var step in g.Steps)
           foreach (var act in step.Actions)
               foreach (var p in act.Parameters)
                   if (p.Name == "GoalName")
                   {
                       var v = p.Value;
                       string keys = "";
                       if (v is System.Collections.IDictionary d) { ... }
                       string directJson = "<n/a>";
                       try { directJson = JsonSerializer.Serialize(v, Json.PrWrite); } catch (Exception ex) { directJson = "<err: "+ex.Message+">"; }
                       string dataJson = "<n/a>";
                       try { dataJson = JsonSerializer.Serialize(p, Json.PrWrite); } catch (Exception ex) { dataJson = "<err: "+ex.Message+">"; }
                       _ = context.App.Debug.Write($"[DIAG] {g.Name}.s{step.Index}.{act.Module}.{act.ActionName}: ...");
                       _ = context.App.Debug.Write($"[DIAG] direct(value)={directJson}");
                       _ = context.App.Debug.Write($"[DIAG] direct(Data param)={dataJson}");
                   }
       foreach (var sub in g.Goals) DiagGoal(sub);
   }
   DiagGoal(goal);
   ```
   - This was the leak-hunt instrumentation from commit `ada1901a`/`50351d8b`. Per commit `711c2107` ("Builder rebuild produced clean .pr files — no CLR-type-name leaks tripped the new guard"), the bug it was hunting has been fixed.
   - It walks every goal recursively, every step, every action, every parameter on every save.
   - It has hardcoded knowledge of one specific parameter name `"GoalName"`.
   - It has bare `catch (Exception ex)` blocks (twice) returning `<err: …>` placeholders.
   - It writes unconditionally via `_ = context.App.Debug.Write(...)`.
   - **Deletion test: if I delete lines 171–198, would any test fail?** No. The probe is logging only; the actual save path continues at line 200+.
   - Per character spec: "If I deleted lines X-Y, would any test fail? If no, that's a finding."
   - **Action: delete the entire DiagGoal block and its invocation.** It served its diagnostic purpose; the comment in the recent commits already authorized removal of related guards once the leak path is proven extinct. This block can go now.

### Simplifications
1. **Line 201 `validateResponse.ValidateGoalState(goal)` re-runs validation.** Comment says "final safety net before persisting." Acceptable belt-and-suspenders but worth confirming it doesn't double-charge for the LLM-validation pass when the .pr is already known-good (e.g., trace replay).

### Verdict: NEEDS WORK
The DIAG block is the single most concrete deletion-test win in the diff.

---

## PLang/App/Debug/this.cs (282 changed)

I scanned but did not deep-review. The class grew significantly (the `--debug={...}` JSON shorthand support, the per-trace LLM output, the verbose flag plumbing). Recommend a separate focused pass on this file by a future codeanalyzer session — it sits in the diagnostic critical path and the diff is too large to fold into v1 responsibly.

### One spot-check finding
- The `--debug={"variables":["foo"]}` string-shorthand fix from `ada1901a` is a positive change — better to fix the parser than to special-case strings.

### Verdict: DEFERRED — recommend dedicated review pass

---

## Cross-cutting findings

### 1. Bare-catch anti-pattern is pervasive in this diff
At least 5 sites swallow `Exception` without filter:
- `TypeConverter.cs:88, 190` (JSON deserialize, ctor invoke)
- `Variables/this.cs:170` (snapshot-clone JSON round-trip)
- `FluidProvider.cs:140` (formal-syntax JSON serialize)
- `Errors/Error.cs:292` (verbose value JSON)
- `LazyParamsGenerator.cs` emits unfiltered catch in the generated `ExecuteAsync`

Per memory: silent error swallowing is **always critical**. The codebase has a clean filter pattern (`catch when (ex is not (NullReference | OOM | StackOverflow))`) that some sites use and others don't. **Either lint-enforce the filter or wrap into a single `SafeJsonSerialize` helper**.

### 2. Three places implement "formal syntax" rendering
- `ExampleRenderer.RenderValueFormal` (Catalog/)
- `FluidProvider.FormatFormalValue` (modules/ui/providers/)
- `DefaultBuilderProvider.FormatValue` (modules/builder/providers/)

Comment in `FluidProvider` says "Mirrors `DefaultBuilderProvider.FormatValue` for the trace-backfill path." When the catalog format evolves, all three must update in lockstep. Extract to one place.

### 3. Two places do key-name heuristics on PLang type names
- `Data.@this.IsDeferredActionTemplate` matches `"action"` / `"list<action>"`
- `PlangTypeIndex.IsClrTypeName` matches a string against ALL loaded type FullNames

The first is structurally fragile; the second is broad but contained. Per memory `feedback_fix_at_right_level.md`: heuristic key-name detection is reimplementing framework machinery. Both should be revisited — replace with structural checks (CLR type identity, marker attribute) where possible.

### 4. Clone-family audit needed
Per the standing pattern: every new property must be propagated through every copy method. New properties this branch:
- `Step.PriorText, Guidance, Level, Confidence, Formal, Source, Keep` — does Step.Merge/Clone handle all? (Step.Merge is updated for some — line 244.)
- `Action.Description, ModuleDescription, IsModifier` — does Action have a clone?
- `Context.Trace, Context.Error` — Clone leaves new instances, which may or may not be what's wanted.
- `Error.Params, Error.Details` — does Error have any copy methods?

This is a **systematic audit** the coder should do, not a single-line fix.

---

## Summary of what to send back to the coder

Priority order:

1. **Delete `DefaultBuilderProvider.cs:171–198 (DiagGoal block)`** — straight deletion-test win.
2. **Fix `TypeConverter.cs` to never throw from `Try*`** — convert `InvalidOperationException` throws at lines 266 and 296 into `(null, Errors.Error)` returns.
3. **Replace bare `catch` with specific exception types** at all 5 sites listed in cross-cutting #1.
4. **Replace `Data.IsDeferredActionTemplate` key-name heuristic** with structural detection (CLR type identity or marker attribute).
5. **Fix `Variables.Set` snapshot-clone catch** — specific `JsonException | NotSupportedException`, surface failure rather than silently aliasing.
6. **Audit `Actor.Context.@this` Clone vs CreateChild divergence.** Decide intent and document, or align them. Same audit on Step/Action/Error after recent property additions.
7. **Consolidate the three "formal syntax" renderers** into one shared writer.
8. **Investigate the asymmetric return in `error.handle.Wrap`** lines 96 vs 109 (RetryFirst drops recovery value, GoalFirst keeps it).
9. **Fix `PlangTypeIndex.Reset()`** to clear all four caches; mark `_clrTypeFullNamesInitialized` volatile or switch to ConcurrentDictionary.
10. **Fix culture-sensitive `ToString`** in ExampleRenderer:105 and FluidProvider:138 — use `InvariantCulture`.

Lower-priority cleanups documented per-file above.
