# Code Analyzer v1 — Full Analysis

Branch: `data-envelope-architecture` vs `runtime2` merge base.
Three passes per file: OBP compliance, simplification, readability.

---

## PLang/Runtime2/Engine/Memory/Data.cs

### OBP Violations

1. **Line 21: Per-request state stored as property — Rule 4**
   - Current: `internal PLangContext? Context { get; set; }` on `Type` (line 21), plus `_context` on `Data` (line 71)
   - `Type` is a small value-like object that gets stamped with `PLangContext` so it can navigate to `Engine.Types`. `Data` holds `_context` and propagates it to `Type`.
   - This *looks* like caching per-request state on a shared object (Rule 4). However, `Data` IS the per-request variable container — it lives in `MemoryStack`, which is per-context. `Type` lives on `Data`. So context-on-Data is correct. Context-on-Type is propagation, not caching.
   - **Verdict: Not a violation.** The ownership chain is `MemoryStack → Data → Type`, all per-request.

2. **Line 156-158: Type getter navigates context to derive type name — potential double-navigation**
   - Current:
     ```csharp
     var typeName = _context?.Engine.Types.Name(_value.GetType())
                    ?? TypeMapping.GetTypeName(_value.GetType());
     ```
   - This navigates `_context.Engine.Types` and has a static fallback. The fallback exists for when Data has no context (e.g., standalone tests). This is acceptable — it's a graceful degradation pattern, not decomposition.
   - **Verdict: Acceptable.** The fallback is intentional for context-less usage.

### Simplifications

1. **Line 140-145: `SetValueDirect` private method**
   - Only called by `RehydrateNestedData` (in Data.Envelope.cs). It bypasses the `Value` setter to avoid clearing `_type`. This is a workaround for the `Value` setter's side effect (line 131: `_type = null`).
   - The need for this method signals that the `Value` setter does too much. The setter clears `_type` because "value changed, type may differ." But `RehydrateNestedData` knows the type hasn't changed.
   - **Recommendation:** Consider separating "value assignment" from "type invalidation" more cleanly. Not urgent — the method is well-documented and safe — but it's a code smell that a private back-door was needed.

2. **Lines 210-263: `UnwrapJsonElement` + `UnwrapNewtonsoftToken` — v1 compatibility shim**
   - The Newtonsoft shim uses reflection to avoid importing Newtonsoft. This is clever but fragile — it checks `value.GetType().Namespace == "Newtonsoft.Json.Linq"`. If Newtonsoft changes its namespace (unlikely but possible), this silently breaks.
   - `UnwrapNewtonsoftToken` also re-parses JSON (`value.ToString()` → `JsonDocument.Parse`) which allocates twice.
   - **Recommendation:** Track this as tech debt. When v1 compatibility is dropped, delete the entire Newtonsoft path. For now it's fine — it's at a serialization boundary and clearly marked.

3. **Lines 265-283: `UnwrapJsonObject` / `UnwrapJsonArray` — could be one method**
   - Both follow the same pattern: iterate, recurse into `UnwrapJsonElement`. Could be unified, but the type safety of returning `Dictionary` vs `List` is cleaner as two methods.
   - **Verdict: Fine as-is.** Two small methods are clearer than one polymorphic method.

### Readability

1. **Line 109: Constructor parameter order**
   - `Data(string name, object? value = null, Type? type = null, Data? parent = null)` — the `parent` parameter feels like it belongs earlier (structural hierarchy) but it's always optional so trailing is fine.
   - **Verdict: Fine.**

2. **Line 208: Magic number `MaxJsonDepth = 128`**
   - Well-named constant. Good.

3. **Lines 285-301: `CleanName` and `BuildPath` — clear utility methods**
   - Short, focused, well-named. Good.

### Verdict: CLEAN
Core Data class is well-structured. The partial split is a good pattern. The only smell is `SetValueDirect` as a private backdoor, but it's well-documented.

---

## PLang/Runtime2/Engine/Memory/Data.Result.cs

### OBP Violations
None.

### Simplifications

1. **Lines 40-57: `Merge` treats Value as `List<Data>` with a cast**
   - `var myData = Value as List<Data> ?? new();` — if `Value` is NOT a `List<Data>`, it creates an empty list and loses the original value. This is documented behavior (the Merge test `Merge_NonListValues_ReturnsEmptyList` confirms it).
   - **Question:** Is silent data loss the right behavior? If someone calls `Merge` on a Data with a string value, the string disappears. This feels like it should either throw or be a no-op.
   - **Recommendation:** Consider adding a guard: if `Value` is not null and not `List<Data>`, return an error or `this`. Low priority — Merge is only called by `Actions.RunAsync` where Value is always `List<Data>`.

### Readability

1. **Line 28: `implicit operator bool`**
   - `public static implicit operator bool(Data d) => d.Success;`
   - This allows `if (!result)` shorthand. Clear and well-established pattern.

### Verdict: CLEAN
Small, focused file. The Merge edge case is worth noting but not blocking.

---

## PLang/Runtime2/Engine/Memory/Data.Navigation.cs

### OBP Violations
None.

### Simplifications

1. **Lines 16-70: `GetChild` path parsing is complex**
   - The method handles dot notation, bracket notation, and mixed notation in one pass. The logic has three branches (dot-first, bracket-first, bracket-at-start) and each computes `segment` + `remaining` differently.
   - **Recommendation:** This is inherently complex (path parsing always is). The depth limit is in place. The recursion is tail-recursive in pattern. Could be refactored to an iterative loop for clarity, but the recursive version with depth limit is safe.
   - **Not a real issue** — the complexity matches the problem.

2. **Line 63-64: Child Data creation in GetChild**
   - `var child = new Data(segment, childValue, parent: this);` — creates a new Data for every navigation step. If you navigate `a.b.c.d.e`, that's 5 Data allocations. For hot paths this could matter.
   - **Verdict:** Acceptable for now. Navigation isn't a hot-path bottleneck. Profile before optimizing.

### Readability

1. **Lines 25-56: Path parsing would benefit from extract-method**
   - The segment/remaining extraction could be a `ParseNextSegment(path)` returning `(string segment, string remaining)`. This would reduce the method from 55 lines to ~25.
   - **Recommendation:** Extract `ParseNextSegment`. Not urgent.

### Verdict: CLEAN
Solid navigation implementation. The path parsing is the most complex part but it's inherently complex work.

---

## PLang/Runtime2/Engine/Memory/Data.Envelope.cs

### OBP Violations

1. **Lines 88, 151: Direct use of `JsonSerializer` — bypasses Engine.Serializers**
   - Current:
     ```csharp
     var json = JsonSerializer.SerializeToUtf8Bytes(this, typeof(Data), _envelopeJsonOptions);
     // and
     var result = JsonSerializer.Deserialize<Data>(decompressed, _envelopeJsonOptions);
     ```
   - OBP says: navigate to serializers through Engine (`Engine.Serializers`). These calls bypass the serializer registry and use `System.Text.Json` directly with a private static `_envelopeJsonOptions`.
   - **However:** The envelope pipeline is serializing Data *itself* for transport — it's a self-serialization concern, not a general "serialize some object" call. The serializer registry is for channel I/O. Using it here would create a circular dependency (Data depends on Serializers which may depend on Data).
   - **Verdict: Acceptable exception.** Self-serialization at the transport boundary is a valid reason to bypass the registry. The private `_envelopeJsonOptions` keeps it contained.

2. **Line 57-69: `Wrap()` creates a new Data with `Type.FromName(kind)` — navigate-don't-pass question**
   - `Wrap()` reads `Type.Kind` (which navigates `_context.Engine.Types.KindOf`) and then creates a new envelope Data. It doesn't pass `this` to anything external — it's self-wrapping.
   - **Verdict: Not a violation.** Behavior belongs to Data (wrapping itself). Self-contained.

### Simplifications

1. **Lines 21-27: Static `_envelopeJsonOptions` — allocated once, never changes**
   - Good pattern. Static readonly with initializer.

2. **Lines 215-241: GZip helpers — could be extension methods or a GZip utility class**
   - `GZipCompress` and `GZipDecompress` are pure functions that have nothing to do with Data's domain. They're implementation details of the Compress/Decompress pipeline.
   - **Recommendation:** These could be extracted to a `GZipHelper` or kept as-is. Keeping them private in Data.Envelope.cs is fine since they're only used here. If another class needs GZip, extract then.
   - **Verdict: Fine as-is.** Don't create abstractions for one-time use.

3. **Lines 192-211: `RehydrateNestedData` — recursive rehydration after deserialization**
   - This exists because `JsonSerializer.Deserialize<Data>` deserializes nested Data objects as `Dictionary<string, object?>` (since `Value` is typed as `object?`). The method detects these dictionaries and reconstructs Data objects.
   - This is a known limitation of `System.Text.Json` with polymorphic types. The method is well-documented and has a depth limit.
   - **Observation:** The detection heuristic (`dict.ContainsKey("value")`) could false-positive on any dictionary that happens to have a "value" key. The heuristic is loose.
   - **Recommendation:** Consider a stricter check — e.g., require both "name" AND "value" keys to be present and the dictionary to have no unexpected keys, or add a `"$type": "Data"` discriminator during serialization. Low priority — false positives would create a Data wrapper around the dictionary, which may or may not break depending on usage.

### Readability

1. **Lines 50-111: Pipeline methods are well-structured**
   - Each method follows the same pattern: check preconditions → return self if no-op → do work → return result.
   - Early returns for no-op cases are clean.
   - Good XML docs explaining when each method returns self vs a new envelope.

2. **Line 105-111: `Encrypt()` is a stub with good documentation**
   - The comment explains what it WILL do when crypto is available. Clear intent.

### Verdict: CLEAN
Well-structured envelope pipeline. The RehydrateNestedData heuristic is the only concern, and it's a known limitation with a depth limit.

---

## PLang/Runtime2/Engine/Memory/MemoryStack.cs

### OBP Violations

1. **Lines 15-27: `PLangContext? _context` stored as internal property — Rule 4?**
   - MemoryStack stores `_context` so it can propagate it to all Data objects. When context is set, it loops through all existing variables and stamps them.
   - MemoryStack is per-request (lives on PLangContext). So this is per-object state on a per-request object — not a violation.
   - **Verdict: Not a violation.** MemoryStack IS per-request.

### Simplifications

1. **Lines 150-164: `Clear()` — complex system variable preservation**
   - Current logic: collect system var keys, compute `toRemove = all keys - system keys`, iterate and remove.
   - This could be simpler: iterate and remove anything that's NOT a system var. The current code creates two lists where one pass would do.
   - **Recommendation:**
     ```csharp
     public void Clear()
     {
         foreach (var key in _variables.Keys.ToList())
         {
             if (key.StartsWith("!") ||
                 key.Equals("Now", StringComparison.OrdinalIgnoreCase) ||
                 key.Equals("NowUtc", StringComparison.OrdinalIgnoreCase) ||
                 key.Equals("GUID", StringComparison.OrdinalIgnoreCase))
                 continue;
             _variables.TryRemove(key, out _);
         }
     }
     ```
   - Saves one list allocation and is more readable.

2. **Lines 170-186: `Clone()` — system variable check duplicates Clear's logic**
   - The same set of system variable names appears in both `Clear()` and `Clone()`. These should be extracted to a `IsSystemVariable(string key)` helper.
   - **Recommendation:** Add `private static bool IsSystemVariable(string key)` and use in both places.

3. **Lines 203-237: `ResolveVariablesInPath` — thread-static HashSet for cycle detection**
   - `[ThreadStatic] private static HashSet<string>? _resolvingVars;`
   - Thread-static is correct for preventing stack overflow from circular variable references. The pattern is: check if root call, init set, resolve, cleanup on root exit.
   - **Observation:** The `isRoot` pattern with `_resolvingVars ??= new()` is clever but not immediately obvious. A reader needs to understand that `_resolvingVars` is null between calls and gets set on first entry.
   - **Verdict: Acceptable.** The thread-static + root-cleanup pattern is standard for re-entrant cycle detection. The alternative (passing a set through parameters) would change the public API.

### Readability

1. **Lines 239-260: `CleanName` and `GetRootName` — duplicated `CleanName` with Data.cs**
   - Both `MemoryStack.CleanName` (line 239) and `Data.CleanName` (Data.cs line 285) do the same thing: trim and strip `%`. Identical code in two places.
   - **Recommendation:** Extract to a shared static helper (e.g., `VariableNames.Clean(string name)`) or have one call the other. Data's version could be `internal static` and MemoryStack could call it.

### Verdict: NEEDS WORK
Two issues: duplicated system variable check logic and duplicated `CleanName`. Both are minor but worth cleaning up.

---

## PLang/Runtime2/Engine/Types/this.cs

### OBP Violations
None. This class owns all type knowledge — behavior is on the owner.

### Simplifications

1. **Lines 15-58 + 61-78: Two large dictionary initializers**
   - `_nameToClr` (58 entries) and `_clrToName` (18 entries) are declared inline with collection initializers. These are read-only after construction — correct.
   - **Observation:** The inverse mapping (`_clrToName`) is manually maintained separately from `_nameToClr`. If you add a type to one, you must remember to add the reverse to the other. This is a maintenance risk.
   - **Recommendation:** Consider deriving `_clrToName` from `_nameToClr` in the constructor (first-seen wins, or shortest-name wins). This would eliminate the sync risk. E.g.:
     ```csharp
     _clrToName = _nameToClr
         .GroupBy(kvp => kvp.Value)
         .ToDictionary(g => g.Key, g => g.First().Key);
     ```
   - **Priority:** Low — the dictionaries are small and rarely change.

2. **Lines 80-279: Two more large dictionary initializers**
   - `_extensionToKind` and `_extensionToMime` — ~200 entries each. These are ConcurrentDictionary because `Add`/`Remove` mutate them at runtime.
   - **Observation:** The extension-to-kind and extension-to-mime mappings have significant overlap (every extension with a MIME type also has a kind). Could these be unified into a single `ExtensionInfo { Kind, Mime }` record?
   - **Recommendation:** Not worth changing. The two dictionaries serve different lookup patterns (sometimes you need kind but not MIME, or vice versa). The current structure is simple and fast.

3. **Lines 388-401: Constructor derives `_allKinds` and `_mimeToKind`**
   - Good pattern — derived data structures computed once. The lock on `_derivedLock` is used for `_allKinds` (a `HashSet`, not concurrent) since `Add`/`Remove` can mutate it at runtime.
   - **Observation:** `_allKinds` is a `HashSet<string>` protected by `lock(_derivedLock)`, while `_mimeToKind` is a `ConcurrentDictionary`. Inconsistent concurrency strategy — both are mutated in `Add`/`Remove`.
   - **Recommendation:** Either make `_allKinds` a `ConcurrentDictionary<string, byte>` (and drop the lock), or wrap `_mimeToKind` mutations in the same lock. The current mix works but is confusing.

4. **Lines 596-601: `Remove` — LINQ Contains for kind deduplication**
   - `!_extensionToKind.Values.Contains(removedKind, StringComparer.OrdinalIgnoreCase)`
   - This is O(n) scan of all extension→kind values. Called only on Remove, which is rare.
   - **Verdict: Fine.** Don't optimize what isn't hot.

5. **Lines 503-517: `Name()` — backtick stripping + ValidValues convention**
   - The fallback for unknown types checks for a `ValidValues` static property (convention for constrained types). This is checked twice — once here and once in `BuilderNames()`.
   - **Verdict: Fine.** The convention is clear and documented.

### Readability

1. **Line 12: Class name `@this`**
   - Standard Runtime2 convention. Consumers use the `EngineTypes` global alias.

2. **Lines 409-463: `Clr(string, int depth)` — generic parsing**
   - The generic syntax parsing (`list<string>`, `dict<string,int>`) is hand-written string manipulation. Works, but fragile for edge cases (e.g., `dict<string,list<int>>` — the comma split won't work with nested generics).
   - **Observation:** The comma split at line 434 (`inner.Split(',')`) will break for `dict<string,list<int>>` because it splits on all commas. However, current PLang doesn't support nested generic syntax beyond one level, so this is fine for now.
   - **Recommendation:** Add a comment noting the single-level generic limitation.

3. **Lines 607-624: `BuilderNames()` — readable, well-commented**
   - Good use of `HashSet<Type>` to deduplicate.

### Verdict: NEEDS WORK (minor)
Two minor issues: inverse dictionary sync risk and inconsistent concurrency strategy for derived structures. Both are low priority.

---

## PLang/Runtime2/Engine/View.cs

### OBP Violations
None.

### Simplifications
None needed. Five attributes and an enum — minimal.

### Readability
Clean. Each attribute has clear purpose. `OutAttribute` has a good XML doc explaining its role.

### Verdict: CLEAN

---

## PLang/Runtime2/Engine/this.cs (changes only)

### OBP Violations
None in the changed portions.

### Simplifications

1. **Line 122: `Types` property**
   - `public Types.@this Types { get; }` — clean addition. Created in constructor (line 189), immutable after that.

### Readability
The `Types` property addition follows the established Engine pattern. Good XML doc.

### Verdict: CLEAN (for the changes)

---

## PLang/Runtime2/Engine/Context/PLangContext.cs (changes only)

### OBP Violations
None in the changed portions.

### Simplifications

1. **Lines 101-106: MemoryStack.Context = this**
   - The PLangContext constructor stamps itself onto the MemoryStack (line 106), which then propagates to all existing Data objects. This is the context-stamping pipeline.
   - Clean pattern. Well-documented.

### Readability
No issues in the changes.

### Verdict: CLEAN (for the changes)

---

## PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs

### OBP Violations

1. **Lines 34-37: Action iterates its own Return list — should Return own this?**
   - Current:
     ```csharp
     if (result.Value != null && this.Return != null)
     {
         foreach (var returnVar in this.Return)
             context.MemoryStack.Set(returnVar.Name, result.Value, result.Type);
     }
     ```
   - Action iterates `this.Return` (which is `List<Data>?`) to store return values. By OBP Rule 5 (smart collections), should `Return` own this loop?
   - **However:** `Return` is `List<Data>?` — a plain nullable list, not a smart collection. Making it a smart collection just for this 3-line loop would be over-engineering.
   - **Verdict: Acceptable exception.** The loop is small, owned by the action (its own data), and Return is just a simple list mapping.

### Simplifications
None.

### Readability
Clean, linear flow: before-events → execute → store returns → after-events.

### Verdict: CLEAN

---

## PLang/Runtime2/actions/convert/fromJson.cs

### OBP Violations
None.

### Simplifications

1. **Line 16: `Data.UnwrapJsonElement` is internal static**
   - Good — the dedup from v5 (security hardening) removed the duplicate `UnwrapJsonElement` from this file and calls the canonical one on Data. This is clean.

### Readability
Short, focused handler. Good error handling with `ValidationError`.

### Verdict: CLEAN

---

## PLang/Runtime2/GlobalUsings.cs

### OBP Violations
N/A.

### Simplifications
None.

### Readability

1. **Line 38: `EngineTypes` alias**
   - `global using EngineTypes = PLang.Runtime2.Engine.Types.@this;` — follows the established pattern.

### Verdict: CLEAN

---

## PLang.Tests/GlobalUsings.cs

### Readability

1. **Line 43: `EngineTypes` alias mirrors production**
   - Consistent with PLang/Runtime2/GlobalUsings.cs.

### Verdict: CLEAN

---

## PLang.Tests/Runtime2/Memory/DataTests.cs

### Readability

1. **Well-organized by phase** — comments separate Phase 2, Phase 3, Phase 4, v5 sections. Good for orientation.

2. **Lines 1031-1103: v5 depth limit + zip bomb tests**
   - The zip bomb test (line 1077) creates 110MB of compressed zeros. This test will be slow and memory-intensive. Consider marking it with a category/tag so it can be skipped in fast test runs.
   - **Recommendation:** Add `[Category("Slow")]` or equivalent attribute.

3. **Lines 1161-1207: Merge tests**
   - Good coverage: combine by name, null other, non-list values.
   - `Merge_NonListValues_ReturnsEmptyList` (line 1195) — the test name says "ReturnsEmptyList" but the behavior is "silently loses both values." The test correctly captures current behavior but the behavior itself is questionable (noted in Data.Result.cs analysis).

### Verdict: CLEAN
Thorough test coverage with clear organization.

---

## PLang.Tests/Runtime2/Memory/MemoryStackTests.cs

### Readability

1. **Lines 179-196: `Get_IndexNotation_NavigatesPath` — test avoids index assertion**
   - Comment says "Note: Index notation may not work correctly due to implementation." The test falls back to directly accessing the list. This is a test that doesn't actually test what its name says.
   - **Recommendation:** Either fix the test to actually test `stack.Get("items[0]")` or rename it to reflect what it actually tests.

2. **Lines 489-558: Phase 2 context stamping tests**
   - Good coverage of the context propagation pipeline.

### Verdict: NEEDS WORK (minor)
One test doesn't test what its name claims.

---

## PLang.Tests/Runtime2/Types/EngineTypesTests.cs

### Readability

1. **Well-organized** — sections for Clr, Name, Kind, Mime, Compressible, Add/Remove, KindOf, depth limits, engine integration.

2. **Good edge case coverage** — null, empty, case-insensitive, unknown types.

3. **Lines 604-618: `Add_CustomType_LazyDerivationUsesEngineTypes`**
   - Tests the full context pipeline: Engine.Types.Add → Data.Type.Kind navigates through context. Good integration test.

### Verdict: CLEAN

---

# Cross-Cutting Findings

## Finding 1: Duplicated `CleanName` (Medium)
`Data.CleanName` and `MemoryStack.CleanName` are identical. Extract to a shared utility.

## Finding 2: Duplicated system variable identification (Low)
`MemoryStack.Clear()` and `MemoryStack.Clone()` both hardcode the same system variable check. Extract `IsSystemVariable()`.

## Finding 3: Inverse dictionary sync risk in Engine.Types (Low)
`_nameToClr` and `_clrToName` must be kept in sync manually. Consider deriving one from the other.

## Finding 4: Inconsistent concurrency in Engine.Types (Low)
`_allKinds` uses `lock`, `_mimeToKind` uses `ConcurrentDictionary`. Pick one strategy.

## Finding 5: RehydrateNestedData heuristic is loose (Low)
Checking `dict.ContainsKey("value")` could false-positive on user data. Consider stricter detection.

## Finding 6: Merge silently drops non-list values (Low)
`Data.Merge()` on non-`List<Data>` values silently loses data. Consider a guard.

## Finding 7: Generic type parsing limited to single nesting (Info)
`Clr("dict<string,list<int>>")` will break. Document the limitation.

---

# Summary

| File | Verdict |
|------|---------|
| Data.cs | CLEAN |
| Data.Result.cs | CLEAN |
| Data.Navigation.cs | CLEAN |
| Data.Envelope.cs | CLEAN |
| MemoryStack.cs | NEEDS WORK (minor) |
| Engine/Types/this.cs | NEEDS WORK (minor) |
| Engine/View.cs | CLEAN |
| Engine/this.cs | CLEAN |
| PLangContext.cs | CLEAN |
| Action/Methods.cs | CLEAN |
| convert/fromJson.cs | CLEAN |
| GlobalUsings.cs | CLEAN |
| Tests/GlobalUsings.cs | CLEAN |
| DataTests.cs | CLEAN |
| MemoryStackTests.cs | NEEDS WORK (minor) |
| EngineTypesTests.cs | CLEAN |

**Overall: The code is well-structured and OBP-compliant.** The envelope architecture follows OBP correctly — Data owns its own wrapping/compression/encryption. Engine.Types consolidates type knowledge as a single owned object on Engine. The partial class split is clean.

Seven findings, all low-to-medium severity. No major OBP violations. No major simplification opportunities. The codebase reads well.
