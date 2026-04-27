# Code Analysis v1 — system-goals-architecture

## Scope

Analyzed the core architecture files of the App namespace rewrite — the most impactful files on this branch. Focus: OBP compliance, simplification, behavioral reasoning, deletion test.

Files reviewed:
- `PLang/App/this.cs` — App root
- `PLang/App/Actor/this.cs` — Actor
- `PLang/App/Actor/Context/this.cs` — Context
- `PLang/App/Goals/Goal/this.cs` — Goal entity
- `PLang/App/Goals/Goal/GoalCall.cs` — Goal resolution
- `PLang/App/Goals/Goal/Steps/this.cs` — Steps collection
- `PLang/App/Goals/Goal/Steps/Step/this.cs` — Step entity
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — Action entity
- `PLang/App/Data/this.cs` — Data core
- `PLang/App/Data/this.Result.cs` — Data result concern
- `PLang/App/Data/this.Navigation.cs` — Data navigation
- `PLang/App/Variables/this.cs` — Variables (MemoryStack replacement)
- `PLang/App/Modules/this.cs` — Module registry
- `PLang/App/FileSystem/Path.cs` — Path Data subclass
- `PLang/App/modules/identity/types.cs` — Identity Data subclass
- `PLang/App/modules/variable/set.cs` — Example handler
- `PLang/App/modules/goal/call.cs` — GoalCall handler
- `PLang/App/modules/goal/return.cs` — Return handler
- `PLang/App/Settings/SettingsVariable.cs` — Settings resolver
- `PLang/App/Utils/CommandLineParser.cs` — CLI parsing
- Cross-cutting scans: bare catches, sync-over-async, System.IO, Newtonsoft

---

## PLang/App/this.cs (App root)

### OBP Violations
None. App owns its state, delegates to subsystems, context flows as parameter.

### Simplifications
1. **Line 247: Sync-over-async in RemoveKeepAlive** — `ad.DisposeAsync().AsTask().GetAwaiter().GetResult()` blocks the calling thread. This is a disposal path, so it may be called from sync context, but if called from async context (which is the common PLang path), this is a deadlock risk.
   - Fix: Make `RemoveKeepAlive` async, or fire-and-forget the disposal.

### Readability
1. **Line 322: System.IO.Path usage** — `global::System.IO.Path.GetDirectoryName(path)` in Save(). The rule says use `fileSystem.Path`, but this operates on the result of `FileSystem.ValidatePath()` which returns an absolute OS path. The FileSystem abstraction wraps System.IO.Path anyway, so this is acceptable — but inconsistent with the convention stated in CLAUDE.md.

### Behavioral Reasoning
1. **Line 308: Empty catch on corrupt app.pr** — `catch (global::System.Text.Json.JsonException) { }` silently eats parse errors. If `app.pr` is partially written (crash during Save), the app starts with a new random Id. This is intentional per the comment but there's no warning logged. The user has no signal that their app identity changed.
   - Severity: **Low** — startup recovers, but identity rotation is invisible.

### Verdict: NEEDS WORK
RemoveKeepAlive sync-over-async is the only actionable finding. Rest are low-severity observations.

---

## PLang/App/Actor/this.cs

### OBP Violations
None.

### Simplifications
None.

### Behavioral Reasoning
1. **Line 105: Sync-over-async in constructor** — `provider.Value!.GetOrCreateDefaultAsync(new Get { Context = app.Context }).GetAwaiter().GetResult()` inside a `DynamicData` lambda. This runs every time `%MyIdentity%` is resolved. If the identity provider's GetOrCreateDefaultAsync touches the network or async DB, this deadlocks in SynchronizationContext environments (e.g., UI threads).
   - Current reality: SQLite is sync under the hood, and PLang runs in console context (no SyncContext). **Safe today, fragile tomorrow** if a remote identity provider is added.
   - Fix: Change `MyIdentity` to an async-resolved pattern (the AsyncData marker in todos).

### Verdict: CLEAN
The sync-over-async is documented and safe in current context. No code change needed now.

---

## PLang/App/Actor/Context/this.cs

### OBP Violations
None. Context owns its own lifecycle events, cancellation stack, and variable registration.

### Clone Family Audit
`Clone()` at line 224 copies: App, Variables (cloned), Parent, IsAsync, Setup, ConfigScope.
Missing from clone: **Goal**, **Step**, **Event**, **Test**, **CallStack**, **EventOverride**.
- Goal/Step: per-execution, set during RunAsync — correct to not clone.
- Test: propagates via the test runner, not via clone — OK.
- CallStack: each context builds its own — OK.
- Event/EventOverride: per-handler-invocation — OK.
The clone is intentionally selective. **No bug here.**

### Readability
1. **Lines 262-323: Three LifecycleFor overloads** — significant duplication between Goal/Step/Action lifecycle resolution. Each collects bindings from Events into a Lifecycle. The only difference is the EventType pairs and filter parameters.
   - Not urgent — it's readable and each is ~15 lines. But if a fourth entity gets lifecycle support, extract a helper.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/this.cs

### OBP Violations
None. Goal owns Parse, MergeFrom, RunAsync, ToText. Behavior belongs to the owner.

### Simplifications
1. **Lines 276-457: Parse method is 180 lines** — The .goal file parser handles block comments, line comments, step continuation, escape characters, goal headers, sub-goal nesting. All in one method. This is the longest method in the reviewed files.
   - This is a parser (even though PLang "has no parser" for step text, it needs one for .goal file structure). The method is sequential and each branch is clear. Extracting sub-methods would fragment the state machine.
   - **No change recommended** — the method is long but not complex. Each branch is a distinct case with clear comments.

### Behavioral Reasoning
1. **Line 50: Goals getter mutates** — `get { foreach (var g in _goals) g.Parent ??= this; return _goals; }` modifies Parent during enumeration. If called from multiple threads, this is a race. But Goals are loaded from .pr files sequentially, so this is safe in practice.

2. **Line 84: PrPath uses backslash fallback** — `Path[sepIndex].ToString() : "\\"` — if Path has no separators, defaults to `\`. On Linux, this creates `dir.build\name.pr` instead of `dir.build/name.pr`.
   - Fix: Use `/` as the default separator (PLang paths are always forward-slash internally).

### Verdict: NEEDS WORK
PrPath backslash fallback is a real bug on Linux.

---

## PLang/App/Goals/Goal/GoalCall.cs

### OBP Violations
None. GoalCall owns goal resolution via GetGoalAsync — navigation through the object graph.

### Behavioral Reasoning
1. **Line 95-97: Parameters injected before file load** — `LoadFromFile` puts parameters on context.Variables BEFORE loading the .pr file. If the .pr load fails, the parameters remain on the Variables, polluting the caller's scope.
   - Severity: **Medium** — Parameters with common names could shadow existing variables in the caller's scope after a failed goal load.
   - Fix: Use `Variables.Save()`/`Variables.Restore()` around the injection, or only inject after successful load.

2. **Line 112-120: Back-reference wiring is incomplete for deep nesting** — Wires `step.Goal` for root and direct sub-goals, but if sub-goals have their own sub-goals (3+ levels deep), those nested goals' steps don't get `Goal` wired.
   - Check: Does the .pr format support 3+ levels? If yes, this is a bug. If .pr files are always flat (root + 1 level of sub-goals), this is fine.

### Verdict: NEEDS WORK
Parameter pollution on failed goal load is the main finding.

---

## PLang/App/Goals/Goal/Steps/this.cs (Steps collection)

### OBP Violations
None. Steps owns its iteration and RunAsync — OBP rule 5.

### Behavioral Reasoning
1. **Lines 103-109: Condition skip logic checks Value type and module name** — `result.Value is bool conditionResult && !conditionResult && step.Actions[0].Module == "condition"`. This hardcodes the module name "condition" as a string comparison.
   - If the condition module is renamed or registered under a different name, sub-step skipping breaks silently.
   - Severity: **Low** — the module name is stable and convention-based.

2. **Lines 46-63: Custom enumerator mutates steps** — `GetEnumerator` sets `step.Goal` and `step.Context`, and clears `step.Disabled`. This means enumerating Steps has side effects. If someone enumerates Steps for read-only purposes (serialization, debugging), they'll mutate step state.
   - However: `RunAsync` doesn't use the enumerator — it uses direct index access (`_items[i]`). So the enumerator side effects only fire via foreach loops.
   - **The enumerator sets Context but RunAsync doesn't** — this is a potential inconsistency. Steps enumerated via foreach get Context stamped; steps accessed via `_items[i]` in RunAsync don't.

### Verdict: NEEDS WORK
The enumerator vs RunAsync inconsistency in context stamping could cause subtle bugs.

---

## PLang/App/Goals/Goal/Steps/Step/this.cs

### OBP Violations
None. Step owns its error handling, timeout, and action execution.

### Simplifications
1. **Line 134: Bare catch(Exception)** — `catch (Exception ex)` in RunAsync. This is the top-level step error boundary. It catches everything including NullReferenceException, which should propagate as programming errors.
   - Fix: `catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))`

### Clone Family Audit
Step.Clone() at line 245 creates new Action instances but shallow-copies:
- `Parameters = new List<Data.@this>(a.Parameters)` — copies the list but shares Data objects. If the original Action's parameter Data is mutated, the clone sees it too.
- **This is acceptable** — Action parameters are read during execution via the source generator, not mutated at runtime. The clone is for the builder's MergeFrom, which replaces whole steps.

### Verdict: NEEDS WORK
Bare catch needs a negative filter.

---

## PLang/App/Data/this.cs + this.Result.cs + this.Navigation.cs

### OBP Violations
None. Data owns its navigation, cloning, and result semantics.

### Simplifications
1. **Line 317 (Data.cs): UnwrapJsonNumber prefers int64 then decimal then double** — `TryGetInt64` first, then `TryGetDecimal`, then `GetDouble`. This means JSON `42` becomes `long`, not `int`. This is consistent with the JSON boxing pattern documented in CLAUDE.md. No issue.

### Behavioral Reasoning
1. **Clone completeness** — Base `Data.Clone()` copies: Name, Value, Type, Error, Handled, Returned, ReturnDepth, Warnings, Signature, Properties, Context. **All fields covered.** Good.

2. **Path.Clone() and Identity.Clone() both miss Returned and ReturnDepth.** These fields are from Data.Result.cs. If a Path or Identity is stored on Variables with Returned=true and then Variables.Clone() is called, the clone loses the return signal. In practice, Returned is only set by `goal.return` on the immediate result flowing up the call chain — it's never stored on Variables. **Low risk but technically incomplete.**

3. **Line 135 (Data.cs): Value setter clears _type** — Setting Value resets the type to null (re-derived on next access). If code sets Value to a different type than what was originally constructed, this is correct. But if code sets Value to the same type, the cached Type object is unnecessarily garbage-collected.
   - **No fix needed** — this is a correctness-over-performance tradeoff.

### Verdict: CLEAN
The clone gap for Returned/ReturnDepth is theoretical. No practical impact.

---

## PLang/App/Variables/this.cs

### OBP Violations
None.

### Simplifications
1. **Line 414: ThreadStatic for circular reference detection** — `[ThreadStatic] private static HashSet<string>? _resolvingVars`. ThreadStatic is correct here since variable resolution is synchronous within a single call. But `[ThreadStatic]` doesn't work with `async`/`await` (thread can change between awaits). Since `ResolveVariablesInPath` is synchronous and called from `Get`/`Set` which are synchronous, this is **safe**.

### Behavioral Reasoning
1. **Line 55: Bracket index resolution in Set** — `name = ResolveVariablesInPath(name)` resolves `%var%` inside brackets before the Set. If the resolved value contains dots or brackets, the subsequent path parsing in Set could misinterpret the resolved value.
   - Example: `Set("items[key].name", value)` where key resolves to "a.b" → becomes `"items[a.b].name"` → GetRootName sees dot at position 6, returns "items[a" as root.
   - **This is a real edge case.** Bracket content should be treated opaquely after resolution. The current regex only resolves `\[([^\]\d][^\]]*)\]` (non-digit, non-bracket content), which would match `[key]` but after resolution the bracket content is already an index. The edge case only fires if a variable value itself contains a dot AND is used as a bracket index. **Low probability but real.**

### Verdict: CLEAN
The variable-in-bracket edge case is noted but unlikely to hit in practice.

---

## PLang/App/Utils/CommandLineParser.cs

### OBP Violations
None.

### Simplifications
1. **Uses Newtonsoft.Json** — Lines 1-2 import `Newtonsoft.Json` and `Newtonsoft.Json.Linq`. CLAUDE.md says "Use System.Text.Json, not Newtonsoft — suggest migration when you see Newtonsoft in Runtime2 code." The App namespace IS the Runtime2 successor.
   - **Migrate to System.Text.Json.** `JToken.Parse` → `JsonDocument.Parse`, `JToken.Type` switch → `JsonElement.ValueKind` switch. The Data class already has `UnwrapJsonElement` which does exactly this pattern.
   - Severity: **Medium** — consistency issue, not a bug.

2. **Lines 99-103: Bare catch in IsValidJson** — `catch { return false; }`. This is a try-parse pattern, so the bare catch is intentional. However, it catches OOM/StackOverflow. Use `catch (JsonException) { return false; }` for Newtonsoft, or the negative filter pattern.

3. **Lines 143-148: Bare catch in ParseValue** — Same pattern. Use `catch (JsonException)`.

### Verdict: NEEDS WORK
Newtonsoft dependency should be migrated. Bare catches should be narrowed.

---

## PLang/App/Settings/SettingsVariable.cs

### OBP Violations
None. SettingsVariable intercepts navigation — behavior belongs to the owner.

### Behavioral Reasoning
1. **Line 67: Sync-over-async** — `.GetAwaiter().GetResult()` on SQLite store. Comment says SQLite is sync under the hood. **Safe for SQLite.** If the settings store is swapped to a remote provider (Redis, API), this deadlocks. The comment is good documentation of the assumption.

### Verdict: CLEAN

---

## Cross-cutting Findings

### Bare catches (5 found in App/)
1. `CommandLineParser.cs:102` — `catch { return false; }` — narrow to `catch (Exception) when (...)`
2. `CommandLineParser.cs:145` — `catch { return rawValue; }` — narrow to `catch (Exception) when (...)`
3. `DefaultGrepProvider.cs:59` — `catch { }` on invalid regex — narrow to `catch (RegexMatchTimeoutException)` or similar
4. `TypeMapping.cs:337` — `catch { }` — needs investigation
5. `Modules/this.cs:272` — `catch { break; }` on no parameterless constructor — narrow to `catch (MissingMethodException)`

### Sync-over-async (3 found in App/)
1. `SettingsVariable.cs:67` — documented, SQLite-safe
2. `App/this.cs:247` — RemoveKeepAlive disposal — should be async
3. `Actor/this.cs:105` — MyIdentity resolution — documented, safe in console context

### Newtonsoft usage in App/
1. `CommandLineParser.cs` — full dependency on JToken/JValue
2. `PLangFileSystem.cs` — [Newtonsoft.Json.JsonIgnore] attributes (dual-serializer compat)
3. `Action/this.cs` — [Newtonsoft.Json.JsonProperty] attributes (dual-serializer compat)
4. `Data/this.cs` — UnwrapNewtonsoftToken (v1 compat shim, namespace-based detection)

Items 2-4 are dual-serializer compatibility and the v1 shim. Only item 1 is an active dependency that should be migrated.

### System.IO in App/
All uses are either:
- Inside `PLang/App/FileSystem/Default/` (the abstraction layer itself) — **correct**
- `global::System.IO.Path` calls for path manipulation where FileSystem isn't available yet — **acceptable**
- One use in `App/this.cs:436` for fallback directory — **acceptable**

No violations.

---

## Summary of Findings

| # | File | Severity | Finding |
|---|------|----------|---------|
| 1 | App/this.cs:247 | Minor | Sync-over-async in RemoveKeepAlive |
| 2 | Goal/this.cs:84 | **Medium** | PrPath backslash fallback on Linux |
| 3 | GoalCall.cs:95 | **Medium** | Parameters pollute Variables on failed goal load |
| 4 | GoalCall.cs:112 | Low | Back-reference wiring incomplete for 3+ levels |
| 5 | Steps/this.cs:46 | Low | Enumerator side effects vs RunAsync inconsistency |
| 6 | Step/this.cs:134 | Minor | Bare catch(Exception) needs negative filter |
| 7 | CommandLineParser.cs | **Medium** | Newtonsoft dependency — should use System.Text.Json |
| 8 | 5 locations | Minor | Bare catches need narrowing |
| 9 | Path.Clone()/Identity.Clone() | Low | Missing Returned/ReturnDepth (theoretical) |

**Critical: 0 | Medium: 3 | Minor: 4 | Low: 3**
