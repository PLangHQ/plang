# Security v1 Learnings — runtime2-action-modifiers

## 1. `Data<T>.Value` caches — treat the returned value as shared state

Partial property getter in `Data.cs` (line 197–215) stores the resolved
value in `_value` and returns the cached instance on subsequent calls.
If the Value is a reference type (e.g. `GoalCall`, a `List<>`, a user
class), every access returns the SAME instance. Any mutation persists
across every subsequent read by any caller.

**Rule:** when a modifier (or any handler) reaches `.Value` on a
`Data<ReferenceType>`, treat the returned object as shared-across-calls
and shared-across-threads. Never mutate it. If mutation is needed,
clone first.

This bit the error.handle module (Finding 1 in v1) — mutated
`goalCall.Parameters` on the cached GoalCall instance.

## 2. Source-generated handler props re-resolve each ExecuteAsync — but the underlying Data is still shared

`LazyParamsGenerator.cs` line 407-413 resets `__{prop}_set = false` at
the start of each ExecuteAsync, so the `get` for each param re-runs
`__ResolveData("Name").As<T>(Context)`. This means the backing
field repopulates per call — but `__ResolveData` fetches the SAME
`Data` object out of `__paramData` each time, and `Data.Value` is
cached within that Data. So param values feel per-call but reference
types inside them are shared.

## 3. `Data<T>.As<T>(Context)` returns `this` when already correctly typed

`Data.cs` line 389-398 short-circuits: if `this` is already `Data<T>`
and `Value is T`, returns `this` unchanged. No defensive cloning.
Another reason why handler props point at shared state.

## 4. Context's cancellation stack is `System.Collections.Generic.Stack<T>`

`Context.this.cs:55` — `Stack<CancellationTokenSource>`, not concurrent.
Serial context today, but the architect roadmap explicitly mentions
`async.fire` and `parallel.set`. Flag for migration before those land.

## 5. CallStack already has MaxDepth = 1000

`CallStack.this.cs:20,52-53` — `MaxDepth = 1000` with `CallStackOverflowException`
thrown on push. So recursive error-goal chains (error.handle calling a
goal whose error.handle calls a goal...) are bounded at 1000 frames,
not a .NET StackOverflowException. Good defense-in-depth; confirmed
it's still in place after the modifier refactor.

## 6. `MemoryCache` stores Data by reference — no serialization

`MemoryStepCache.cs:17,25` — `_cache.Set(key, value, policy)` stores
the Data object itself, not a serialized copy. Consequence: any
mutation to a cached Data's Value propagates to future cache hits.
Also means there's zero defense against huge cached values (no LRU cap,
no size accounting).

## 7. When-filter + captured parentToken pattern

`timeout/after.cs:25-36` captures the parent token in a local BEFORE
pushing a new CTS onto the context's stack. This is subtle: once
pushed, `context.CancellationToken` returns the new CTS's token, so
`parentToken.IsCancellationRequested` in the when-filter would always
be false (matching the inner CTS). Capturing parentToken first fixes
this. Worth remembering: if you have a cancellation hierarchy where
child cancellation shadows parent on a getter, capture-before-push is
the safe pattern.

## 8. `As<T>` path for context-resolvable types uses reflection + cache

`Data.cs:389-413` — looks up a static `Resolve(string, Context)`
method by reflection, caches in `ResolveMethodCache`. If a type has
such a method, strings in Value are auto-resolved. Potential attack
surface if a `Resolve` method does something unsafe with the input
string; worth knowing next time I audit parameter binding.

## 9. The "don't mutate inputs" rule is not just OBP — it's security

The OBP doc covers "navigate, don't pass" and "collections are smart
wrappers," but it doesn't explicitly forbid mutating input parameters.
The `error.handle.CallErrorGoal` finding is really a case of
"behavior method modified its input" — a functional-purity violation
that becomes a security issue because the input is shared state.
Adding this to the checklist: handler methods should treat `Data<T>`
values and their contents as immutable.

## 10. The `DoesNotMutateOriginalParameters` test only checks one level

`ErrorHandleTests.cs:358-376` — verifies the `originalParams`
`List<Data>` is unchanged, but doesn't verify that the
`goalCall` OBJECT is unchanged. LINQ's `.ToList()` protects the list
reference, but the `goalCall.Parameters = parameters` reassignment is
unobserved. **Lesson for writing security-relevant tests:** when the
concern is "don't mutate this input," assert on the TOP-LEVEL object
identity and all mutable subobjects, not just one list.
