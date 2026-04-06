# Code Analysis v1 — UI Module + Clone Fixes

## PLang/App/modules/ui/render.cs

### OBP Violations
None. Action handler follows the correct pattern: `[Action]`, `IContext`, `partial` properties, single-line `Run()` that delegates to provider via `[Provider]`. Exactly what the module structure guide specifies.

### Simplifications
None needed. 32 lines, clean delegation.

### Readability
Clean. Properties have XML docs. `[IsNotNull]` on Template catches nulls at dispatch.

### Verdict: CLEAN
Textbook handler — thin delegation to provider.

---

## PLang/App/modules/ui/providers/ITemplateProvider.cs

### OBP Violations
None. Provider interface takes `Render action` — callee navigates the action record. Correct OBP pattern.

### Verdict: CLEAN

---

## PLang/App/modules/ui/providers/FluidProvider.cs

### OBP Violations
None. The provider navigates `action.Context.Engine.FileSystem`, `action.Context.Variables`, `action.Context.Goal?.Path` — all through the action's object graph.

### Simplifications

1. **Line 104: `catch (Exception ex)` in Render() is too broad.**
   Fluid's `RenderAsync` can throw various template runtime errors, but this catch-all also swallows `NullReferenceException`, `InvalidOperationException`, and other programming errors. Should narrow to the exceptions Fluid actually throws.

   - Current: `catch (Exception ex)`
   - Suggested: `catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))`
   - **Why:** A NRE from a PLang bug inside `callGoal` would be silently converted to a user-facing "RenderError" — masking the real issue. This is the recurring "catch-all in wrapper methods" pattern.

2. **Line 131-140: Empty `catch` in RegisterTypeIfNeeded silently swallows errors.**
   - Current: `catch { // Some types may not be registerable — skip silently }`
   - This is acceptable for a type registration helper, but should at minimum catch a specific exception type rather than bare `catch`.
   - Suggested: `catch (Exception) { }` at minimum to be explicit. But truly, what throws here? If Fluid.Register never throws, remove the try/catch entirely.

3. **Lines 243-265: Double try/catch in PlangFileProvider.GetFileInfo is convoluted.**
   The outer catch catches `ValidatePath` failures, then retries with raw `Path.Combine`. The inner catch catches the retry's failure. This creates nested exception handling for what should be a simple path resolution.

   - Current: try { ValidatePath } catch { try { raw combine } catch { continue } }
   - Suggested: Extract a helper `TryResolvePath` that returns `string?` without throwing:
     ```csharp
     var fullPath = TryResolvePath(candidate);
     if (fullPath != null && _fs.File.Exists(fullPath))
         return new PlangFileInfo(_fs, fullPath, candidate);
     ```
   - **Why:** Nested try/catch with `continue` is hard to follow. A null-returning helper is simpler.

### Readability

1. **Line 28: Render() is 82 lines.** This is borderline. The method has clear phases (resolve → parse → build context → render), but could benefit from extracting the "build Fluid context" phase (lines 64-95) into a private method like `BuildTemplateContext(action, options)`. Not critical.

2. **Line 217: `catch (Exception ex)` in CallGoalTagAsync** — same catch-all concern as finding #1. A NRE from engine internals becomes `[Error: Object reference not set]` in the template output. Should let programming errors propagate. Since this is inside a Fluid tag handler, the outer `catch` in `Render()` will catch it — so narrowing here means the outer catch handles it (which is already better: it returns a `Data.FromError` rather than embedding an error string in output).

### Pass 4: Behavioral Reasoning

1. **CallGoalTagAsync writes errors into template output (line 213-214).** When a goal fails, the error message appears as rendered HTML text: `[Error: Goal 'X' not found]`. This is a **design choice**, not a bug — it's noted as "per Ingi's requirement." But it means:
   - Error messages from goal execution leak into user-facing HTML output
   - `result.Success` on the `Render()` call will be `true` even though a callGoal failed
   - The PLang developer has no programmatic way to detect that a callGoal failed inside a template

   This is worth noting but not necessarily wrong — template rendering libraries typically work this way. If Ingi wants callGoal errors to fail the whole render, the design would need to change.

2. **Line 80-85: Variables.GetAll() + SetValue loop.** Each `Data` from `GetAll()` has its `.Value` extracted and registered with Fluid. This means Fluid sees the *unwrapped* value (the CLR object), not the `Data` wrapper. This is correct — templates should see `"Alice"`, not `Data{Name="name", Value="Alice"}`. The test `Render_DataObject_ExposesValueNotWrapper` confirms this.

3. **Line 101: HtmlEncoder.Default is always-on.** All template output is HTML-encoded. This is correct for XSS prevention in web contexts, but if someone uses `ui.render` for non-HTML output (email subject lines, markdown, plain text), they'll get unexpected encoding (`&amp;` instead of `&`). This is a design limitation worth noting for future consideration, not a bug.

### Pass 5: Deletion Test

1. **Lines 116-141 (RegisterTypeIfNeeded): Could this be deleted with no test failing?** Yes — if removed, Fluid would still render primitives, strings, and collections correctly (native support). Only complex user-defined objects would lose property access. But the test `Render_DotNavigation_AccessesObjectProperties` uses an anonymous type, which Fluid handles natively. **No existing test specifically requires RegisterTypeIfNeeded for a named class type.** This is a gap — there should be a test with a real class (not anonymous type) to prove the registration works.

2. **Lines 184-222 (CallGoalTagAsync): Deletion test — would any test fail?** The tests `Render_CallGoal_*` all test with nonexistent goals (producing error text). If `callGoal` tag were not registered, `{% callGoal 'X' %}` would produce a Fluid parse error, not an `[Error:]` message. So the tests would fail differently (TemplateError instead of inline error or RenderError). The tag IS tested, but only the error path — no test proves a successful goal call writes output.

3. **Lines 226-283 (PlangFileProvider): Deletion test — would any test fail?** Yes — `Render_Include_RendersPartialInline`, `Render_Include_InheritsVariables`, `Render_Include_NestedPathResolvesRelativeToPartial` all use `{% include %}` which requires the file provider. Covered.

### Verdict: NEEDS WORK
Three findings: broad catch-all (×2, lines 104 and 217), nested try/catch in PlangFileProvider (line 243). Plus one deletion-test gap (RegisterTypeIfNeeded untested for named types) and one successful-callGoal coverage gap.

---

## PLang/App/Engine/Memory/Data.cs (Clone changes)

### OBP Violations
None. `Clone()` is virtual on the owner — subclasses override with proper cloning. Correct OBP.

### Pass 4: Behavioral Reasoning

1. **DataList.Clone() (lines 402-409) drops metadata.** The clone copies `Name`, `_items`, and `Error`, but does NOT copy:
   - `Handled`
   - `Warnings`
   - `Signature`
   - `Properties`
   - `Context`
   - `_type`

   Compare to `Data.Clone()` which copies all of these. **If a DataList has a Signature or Properties, the clone loses them.** This is likely acceptable today (DataList is used for collection results, not signed data), but it's a latent bug if DataList ever carries metadata.

2. **Data<T> has no Clone() override.** `Data<T>` inherits `Data.Clone()`, which creates a `new Data(...)` — not a `new Data<T>(...)`. This means cloning a `Data<T>` returns a `Data`, losing the typed `Value` property. Currently `Data<T>` is mainly used for one-shot returns (e.g., `Providers.Get<T>()`) so it's unlikely to be cloned. But this is the "clone family audit" pattern — every subclass should be checked.

### Verdict: NEEDS WORK
DataList.Clone() drops metadata. Data<T> has no Clone() override.

---

## PLang/App/Engine/Memory/Properties.cs (Clone)

### OBP Violations
None. Properties owns its own Clone().

### Simplification
Clean — shallow copy of the item list. Properties contains `Data` items; whether those Data items need deep cloning depends on whether Properties mutations are independent. Since Properties is typically a bag of named values, shallow copy is probably fine.

### Verdict: CLEAN

---

## PLang/App/Engine/FileSystem/PathData.cs (Clone)

### OBP Violations
None.

### Pass 4: Behavioral Reasoning
Clone creates a new PathData with the same absolute path and filesystem reference. This is correct — PathData is essentially immutable (the path doesn't change). `Properties.Clone()` is called, consistent with Data.Clone().

**Missing from clone:** `Error`, `Handled`, `Warnings`, `Signature`, `Context`, `_type`. Same gap as DataList — PathData.Clone() only copies `Name` and `Properties`, not the full Data metadata. Compare to base `Data.Clone()`:
- Data.Clone() copies: Name, Value, Type, Error, Handled, Warnings, Signature, Properties, Context
- PathData.Clone() copies: absolutePath (→ re-derives Absolute/Relative/etc.), Value, Source, Name, Properties

**Missing: Error, Handled, Warnings, Signature, Context, _type.** If a PathData has an error set on it and gets cloned, the error is lost.

### Verdict: NEEDS WORK
PathData.Clone() doesn't copy Error, Handled, Warnings, Signature, Context. Same issue as DataList.Clone().

---

## PLang/App/modules/identity/types.cs (IdentityData.Clone)

### Pass 4: Behavioral Reasoning
Same pattern issue: IdentityData.Clone() copies identity-specific fields (PublicKey, PrivateKey, IsDefault, IsArchived, Created) and Properties, but misses: **Error, Handled, Warnings, Signature, Context, _type.**

This is a systematic issue across all three Clone() overrides.

### Verdict: NEEDS WORK
Same missing metadata as PathData and DataList.

---

## PLang/App/Engine/Memory/Variables.cs (Clone narrowing)

### Pass 4: Behavioral Reasoning

The change narrows the "share by reference" check from `kvp.Value.GetType() != typeof(Data)` (any non-exact-Data subclass) to `kvp.Value is SettingsData` (only SettingsData). This means:
- **DynamicData**: skipped earlier in the loop (line 199). OK.
- **PathData**: now deep-cloned via `Clone()`. Previously shared by reference. This is correct — PathData can carry mutable Value (file content).
- **IdentityData**: now deep-cloned via `Clone()`. Previously shared by reference. This is correct — identity data should be isolated per-context.
- **DataList**: now deep-cloned via `Clone()`. Previously shared by reference. Correct.
- **Data<T>**: now deep-cloned via base `Data.Clone()`. Returns a `Data`, not `Data<T>`. If something later expects `Data<T>`, it'll fail. But Data<T> is rarely on Variables — mostly used for one-shot returns.

The narrowing is correct in intent. The issue is that the Clone() overrides are incomplete (see above).

### Verdict: NEEDS WORK (due to incomplete Clone overrides it calls)

---

## PLang/App/Engine/Memory/Data.Envelope.cs

### The catch addition
Adding `InvalidOperationException` to the catch in `Decompress()` is straightforward. The existing catches already handle `JsonException` and `NotSupportedException`. InvalidOperationException can come from `Decompress()` operations on malformed data.

### Verdict: CLEAN

---

## PLang/App/modules/condition/providers/DefaultEvaluator.cs

### The catch addition
Adding `InvalidCastException` to both `Evaluate()` and `Compare()` catches is correct. The JSON boxing problem (int vs long) can produce InvalidCastException during type normalization. This was a known gap.

### Verdict: CLEAN

---

## PLang/App/Engine/Providers/this.cs

### The registration
`Register<ITemplateProvider>(new FluidProvider())` follows the same pattern as all other providers. `ResolveType` adds `"template"` and `"itemplateprovider"` mappings. Consistent.

### Verdict: CLEAN

---

## Remaining files (Attributes.cs, on.cs, skipAction.cs, remove.cs, GoalCall.cs)

All changes are XML documentation comments only — no behavior change. Skipping analysis.

### Verdict: CLEAN

---

# Summary

## Critical Findings

### Finding 1: Clone() overrides drop Data metadata (MAJOR)
**Files:** `DataList.Clone()`, `PathData.Clone()`, `IdentityData.Clone()`
**Issue:** All three Clone() overrides only copy their subclass-specific fields. They miss `Error`, `Handled`, `Warnings`, `Signature`, `Context`, and `_type` from the base `Data`. If any of these subclasses carries error state, warnings, or signatures, cloning loses that information.
**Fix:** Each override should either call base.Clone() and transfer subclass fields, or explicitly copy all base fields. Pattern:
```csharp
public override Data Clone()
{
    var clone = new PathData(_absolutePath, _fs, Value, Source)
    {
        Name = Name,
        Properties = Properties.Clone(),
        Error = Error,
        Handled = Handled,
        Warnings = Warnings != null ? new List<Info>(Warnings) : null,
        Signature = Signature,
    };
    clone.Context = Context; // if needed
    return clone;
}
```

### Finding 2: FluidProvider catch-all masks programming errors (MEDIUM)
**File:** `FluidProvider.cs:104` and `FluidProvider.cs:217`
**Issue:** `catch (Exception ex)` in both `Render()` and `CallGoalTagAsync()` converts ALL exceptions to user-visible error messages, including NullReferenceException and other programming errors.
**Fix:** Use `catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))` or narrow to the actual exception types that Fluid/engine can throw.

### Finding 3: Nested try/catch in PlangFileProvider.GetFileInfo (MINOR)
**File:** `FluidProvider.cs:243-265`
**Issue:** Double-nested exception handling is hard to follow. The comment says "ValidatePath may throw for uninitialized filesystems" — but why would the filesystem be uninitialized during template rendering?
**Fix:** Extract a null-returning helper or determine if the fallback is actually needed.

## Deletion Test Gaps

1. **RegisterTypeIfNeeded (lines 116-141):** No test uses a named class type that requires registration. All tests use primitives, strings, anonymous types, or lists — all of which Fluid handles natively. A test with a real class (e.g., a DTO with named properties) would prove this code is needed.

2. **Successful callGoal:** All callGoal tests invoke nonexistent goals. No test proves that a successful goal call writes its result into template output.

## Overall Verdict: NEEDS WORK

The clone family issue is a systematic pattern that should be fixed across all three overrides. The catch-all is the recurring pattern from previous reviews. The nested try/catch is a readability issue.
