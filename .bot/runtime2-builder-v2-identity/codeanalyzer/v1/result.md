# Code Analysis: Identity Module — runtime2-builder-v2-identity

## PLang/App/modules/identity/types.cs (IdentityVariable)

### OBP Violations
None. Persistence methods (`LoadAsync`, `SaveAsync`, `RemoveAsync`) live on the owner and navigate to `engine.System.DataSource` internally. Good OBP.

### Simplifications

1. **Line 10: Not sealed** — `IdentityVariable` is a concrete class with no virtual methods and no subclasses. Per codebase convention, seal it.
   - Current: `public class IdentityVariable`
   - Fix: `public sealed class IdentityVariable`

2. **Lines 99-101: Double TryGetValue for "Created"** — Calls `dict.TryGetValue("Created", ...)` twice. The second call always returns the same value as the first. The `out var c` from the first call already holds the value.
   - Current:
     ```csharp
     Created = dict.TryGetValue("Created", out var c) && c is DateTime dt ? dt :
               dict.TryGetValue("Created", out var cs) && cs is string s && DateTime.TryParse(s, out var parsed) ? parsed :
               DateTime.UtcNow
     ```
   - Fix:
     ```csharp
     Created = dict.TryGetValue("Created", out var c)
         ? (c is DateTime dt ? dt : c is string s && DateTime.TryParse(s, out var parsed) ? parsed : DateTime.UtcNow)
         : DateTime.UtcNow
     ```

3. **Lines 106-114: JSON round-trip fallback** — The `try { Serialize → Deserialize }` path handles object types that aren't `IdentityVariable` or `Dictionary`. In practice, `SqliteDataSource.DeserializeValue` returns either `IdentityVariable` (never — it doesn't know the type), `Dictionary<string, object?>` (via `UnwrapJsonElement` on JSON objects), or primitives. The dictionary path handles the primary DataSource flow. The JSON round-trip is a defensive fallback that has no exercising test. Low-risk to keep, but worth noting.

### Readability
Clean. `Deserialize` is the longest method but each branch is straightforward.

### Verdict: NEEDS WORK
Seal the class. Fix the double TryGetValue — it's confusing to read and redundant.

---

## PLang/App/modules/identity/IdentityData.cs

### OBP Violations
None. IdentityData owns its own lazy resolution and cache update.

### Simplifications

1. **Lines 46-64: Duplicate auto-create logic** — `ResolveDefault()` contains the same auto-create-a-"default"-identity logic as `Get.Run()` (get.cs lines 38-54). Both paths:
   - Load all identities
   - Find default non-archived
   - If none: generate Ed25519 keys, create IdentityVariable("default"), save

   **Why it matters:** Two creation paths for the same "default" entity means maintenance burden and potential divergence. If one path changes behavior (e.g., different default name), the other won't. Additionally, if both paths race (unlikely but possible), two "default" identities could be created.

   **Recommendation:** `IdentityData.ResolveDefault()` should delegate to the `Get` handler or share a common static method on `IdentityVariable` (e.g., `IdentityVariable.GetOrCreateDefaultAsync(engine)`).

### Readability

1. **Lines 48, 63: Sync-over-async** — `.GetAwaiter().GetResult()` in `ResolveDefault()`. This is a known trade-off for property-based lazy resolution (properties can't be async). In PLang's execution model (thread pool, no SynchronizationContext), this is safe. But it's worth documenting why — a future maintainer may see this and try to "fix" it.

### Behavioral Reasoning

1. **Lines 23-28: Thread safety of `_resolved` flag** — Two threads accessing `Value` before resolution could both see `_resolved == false` and both call `ResolveDefault()`. The second call would try to create a duplicate "default" identity. In practice, PLang runs sequentially per context, so this won't happen. But `_resolved` is not `volatile` and doesn't use `Interlocked`, so on weakly-ordered architectures the flag could be stale. Low-risk but worth noting.

### Verdict: NEEDS WORK
The duplicate auto-create logic is the main issue. One creation path, not two.

---

## PLang/App/modules/identity/create.cs

### OBP Violations

1. **Lines 33-39: Iterating another object's collection** — `foreach (var existing in all.Where(i => i.IsDefault))` iterates the list of identities to clear defaults. This is the same pattern used in `setDefault.cs` lines 30-35. However, `all` is a plain `List<IdentityVariable>`, not a smart collection. The logic is handler-specific (clear defaults before creating). **Borderline** — would only be a violation if this clearing logic appeared 3+ times and belonged on a collection wrapper.

### Simplifications
None. Clean handler.

### Readability
Clean. Good validation, clear flow.

### Verdict: CLEAN (borderline OBP note is minor)

---

## PLang/App/modules/identity/get.cs

### Simplifications

1. **Lines 38-54: Duplicate auto-create** — Same logic as `IdentityData.ResolveDefault()`. See IdentityData.cs finding above.

### Readability

1. **Line 24: Side-effect on read** — `Get` by name calls `Context.Engine.System.Identity.Update(identity)` even when fetching a non-default identity. This mutates the System identity cache to whatever identity was last fetched by name, regardless of whether it's the default. This is semantically wrong — fetching "alice" shouldn't change `%MyIdentity%` to alice.

   **Impact:** If a PLang script does `get identity 'alice'` then `%MyIdentity%` becomes alice until something else updates it. This violates the principle that `%MyIdentity%` reflects the *default* identity.

### Behavioral Reasoning

1. **Lines 24, 34, 53: Unconditional Identity.Update()** — All three exit paths call `Identity.Update()`. The auto-create path (line 53) is correct — the newly created identity is the default. The default-found path (line 34) is correct. But the **by-name path (line 24)** updates the System identity cache to a non-default identity. This is a behavioral bug.

### Verdict: MAJOR ISSUES
The `Update(identity)` call on the by-name path overwrites `%MyIdentity%` with whatever identity was fetched. Should only update when fetching the default.

---

## PLang/App/modules/identity/getAll.cs

### Verdict: CLEAN
Three lines of logic, all correct.

---

## PLang/App/modules/identity/rename.cs

### Behavioral Reasoning

1. **Lines 32-36: Non-atomic rename** — `RemoveAsync` then `SaveAsync`. If `SaveAsync` fails (DataSource error, disk full), the identity is deleted but not re-created. **Data loss risk.** There's no rollback.

   **Recommendation:** Save the new name first, then remove the old entry. Or at minimum: if `SaveAsync` fails, attempt to restore the old entry.

### Readability
Clean otherwise.

### Verdict: NEEDS WORK
Non-atomic rename is a data loss risk.

---

## PLang/App/modules/identity/setDefault.cs

### OBP Violations
Same borderline note as create.cs — iterates `all.Where(i => i.IsDefault)` externally. Acceptable since it's a plain list.

### Verdict: CLEAN

---

## PLang/App/modules/identity/archive.cs

### Simplifications

1. **Line 28: Returns `Data.Ok()` with no value on idempotent success** — When already archived, returns `Data.Ok()` (no value). When newly archived, also returns `Data.Ok()` (line 34). This is consistent and intentional. Not a problem.

### Verdict: CLEAN

---

## PLang/App/modules/identity/unarchive.cs

### Verdict: CLEAN
Mirror of archive with correct semantics. Returns identity on success.

---

## PLang/App/modules/identity/export.cs

### Deletion Test

1. **Lines 27-33: Default identity fallback path** — No test calls `Export { Name = null }` to exercise the default-identity fallback. Could delete lines 27-33 and no test would fail.

### Verdict: NEEDS WORK (test gap)

---

## PLang/App/modules/identity/KeyGenerator.cs

### Verdict: CLEAN
Simple, focused, sealed (internal static). Correct use of NSec. Key export policy explicitly set.

---

## PLang/App/Engine/Channels/Serializers/SensitivePropertyFilter.cs

### Verdict: CLEAN
Static utility, single responsibility, correct reverse-iteration for removal. Well-tested (5 tests).

---

## PLang/App/Engine/View.cs (SensitiveAttribute)

### Readability

1. **Good doc comment** — Clearly states that `[Sensitive]` is serialization-only, not access control. This prevents future misunderstanding.

### Verdict: CLEAN

---

## PLang/App/Engine/Channels/Serializers/Serializer/JsonStreamSerializer.cs

### Simplifications
The `SensitivePropertyFilter.Filter` is wired into default options (line 30) and also explicitly in `ForView` (line 50). This is correct — view serializers inherit from base options via `new JsonSerializerOptions(_options)`, but `TypeInfoResolver` isn't inherited (it's replaced), so both places need it.

### Verdict: CLEAN

---

## PLang/App/Engine/Context/Actor.cs

### OBP Violations
None. Identity is a lazy property on Actor. DynamicData registers `%MyIdentity%` via lambda navigating to `engine.System.Identity.Value`. Clean navigation.

### Behavioral Reasoning

1. **Line 79: All actors get `%MyIdentity%` pointing to System identity** — `engine.System.Identity.Value` is evaluated on every access via `DynamicData`. This means `%MyIdentity%` in User context and Service context also resolves to the System actor's default identity. This is the intended design (confirmed by architect plan). Good.

2. **Clone/copy family audit** — `Actor` has no `Clone()` method. Actors are singletons per engine (System, Service, User). No copy needed. `PLangContext.Clone()` and `CreateChild()` share the same `Variables`, which inherits the `DynamicData` registration. No property was missed.

### Verdict: CLEAN

---

# Summary of Findings

## Critical

| # | File | Issue | Impact |
|---|------|-------|--------|
| 1 | **get.cs:24** | `Identity.Update()` on by-name fetch overwrites `%MyIdentity%` with non-default identity | Behavioral bug — `%MyIdentity%` becomes wrong after any `get identity 'name'` |

## Needs Work

| # | File | Issue |
|---|------|-------|
| 2 | **IdentityData.cs + get.cs** | Duplicate auto-create-default logic in two places |
| 3 | **types.cs:99-101** | Double `TryGetValue("Created")` — redundant and confusing |
| 4 | **types.cs:10** | Not sealed |
| 5 | **rename.cs:32-36** | Non-atomic remove+save — data loss if save fails |

## Test Gaps (Deletion Tests)

| # | File:Lines | Missing |
|---|-----------|---------|
| 6 | **export.cs:27-33** | No test for `Export { Name = null }` (default identity fallback) |
| 7 | **types.cs:106-114** | No test for the JSON round-trip Deserialize fallback |

## Overall Verdict: NEEDS WORK

The identity module is well-structured and follows OBP correctly. The handlers are clean, focused, and well-tested. But finding #1 (get.cs updating `%MyIdentity%` on by-name fetch) is a behavioral bug that changes system state incorrectly. Fix that, deduplicate the auto-create logic, and clean up the minor issues.
