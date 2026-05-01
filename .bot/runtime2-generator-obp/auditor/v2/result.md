# auditor v2 — detailed findings

Three findings. One MAJOR (cross-file contract regression) and two MINOR
(review-gap findings explaining the path the major took past three reviewers).

---

## #1 — MAJOR — null-Variable NRE regression in 22 handlers

**Category:** cross-file
**Missed by:** codeanalyzer (file-scope review can't see the
architect-vs-implementation contract gap)
**Files (entry point representative):** `PLang/App/modules/variable/get.cs:16`
plus 21 sibling handlers (full list below)
**Severity rationale:** Major because (a) replaces a clear `MissingParameter`
ServiceError diagnostic with a stack-derived generic StepError, (b) the
architect's v5 plan made an explicit safety-net claim that was not honored
in implementation (zero of 22 Variable slots got `[IsNotNull]`), (c)
empirically reproduces at the Data layer.

### Pre-v7 behavior

The deleted `RawScalarValidations` block in
`PLang.Generators/Emission/Action/this.cs` (recovered from `git show
0312f5f9`) emitted, for every `[VariableName] string` slot:

```csharp
if (string.IsNullOrEmpty({PropertyName}))
{
    if (__resolutionError != null) return __resolutionError;
    var __prValue = __action?.Parameters?.FirstOrDefault(p =>
        string.Equals(p.Name, "{PropertyName}", StringComparison.OrdinalIgnoreCase))
        ?.Value?.ToString() ?? "(unknown)";
    var __stepText = __step?.Text ?? "(unknown step)";
    if (__stepText.Length > 80) __stepText = __stepText[..80] + "...";
    var __err = new ServiceError(
        $"'{__prValue}' is empty — nothing to use as '{lower}' in step: {__stepText}",
        __step, __callFrames, "MissingParameter", 400);
    __err.Context = context;
    return Data.@this.FromError(__err);
}
```

Result for missing/null Name: `ServiceError("'(unknown)' is empty — nothing
to use as 'name' in step: <step text>", "MissingParameter", 400)` with
parameter name + step context + Params snapshot.

### Post-v7 behavior (verified)

Trace, for `__action.Parameters` not containing "Name" (or containing it
with `Value=null`):

1. `__ResolveData("Name")` returns `Data.@this.NotFound("Name")` — `Value=null,
   IsInitialized=false`. (`PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs:136`)
2. `.As<Variable>(ctx)` → `AsT_Impl<Variable>(null, ctx)`:
   - `IsActionDestination(typeof(Variable))` → false; skip.
   - **Bypass branch (lines 549-562):** `raw is string rawNameStr` — `null`
     is NOT a string → skip.
   - `if (raw is string strVal && strVal.Contains('%'))` → null, skip.
   - `if (ctx != null && IsWalkableContainer(raw))` — null is not walkable; skip.
   - **Path-style branch (lines 632-644):** `raw is string srStr` → null, skip.
   - Falls through to `WrapAs<Variable>(null, ctx)`.
3. `WrapAs`:
   - `if (this is @this<T> sameTyped && sameTyped._value is T)` — `this` is
     plain Data NotFound, not Data<Variable>; skip.
   - `if (value is T fast && IsPlangAssignable(typeof(T), value.GetType()))` —
     value is null; skip.
   - `if (value == null) return ConstructWrap<T>(default, ctx);` — **fires.**
4. `ConstructWrap` returns `new Data<Variable>("Name", null, _type, Parent)
   { Context = ctx }` with Properties + events aliased from the source
   Data. **Importantly:** `Success=true` (no error), `IsInitialized=true`
   (default — note that ConstructWrap does NOT propagate
   `IsInitialized=false` from the NotFound source).
5. Generated property getter (`PLang.Generators/Emission/Property/Data/this.cs:50`):
   ```csharp
   get { if (Backing == null) {
       Backing = __ResolveData("Name").As<Variable>(Context);
       if (!Backing.Success) __resolutionError = Backing;
       SetFlag = true; }
       return Backing!; }
   ```
   `Success` is true → `__resolutionError` stays null.
6. `[IsNotNull]` validation in `Emission/Action/this.cs:193-208` only
   iterates over `info.IsNotNullProperties`. ZERO of 22 Data<Variable>
   slots are decorated with `[IsNotNull]`, so this check does nothing for
   the Variable case.
7. Handler `Run()` reads `Name.Value` (returns `null` Variable instance).
8. `Context.Variables.Get(Name.Value)` triggers the implicit operator at
   `PLang/App/Variables/Variable.cs:32`:
   ```csharp
   public static implicit operator string(Variable v) => v.Name;
   ```
   `v` is null → accessing `v.Name` throws **NullReferenceException**.
9. NRE escapes the handler, then escapes generated `ExecuteAsync`, then
   escapes `App.Run` — `PLang/App/this.cs:415` catch deliberately excludes
   NRE:
   ```csharp
   catch (Exception ex) when (ex is not (NullReferenceException
       or OutOfMemoryException or StackOverflowException))
   ```
10. NRE bubbles up through `Action.RunAsync` (no catch) and is finally
    caught by `Step.RunAsync` at `PLang/App/Goals/Goal/Steps/Step/this.cs:157`:
    ```csharp
    catch (Exception ex) when (ex is not (OutOfMemoryException
        or StackOverflowException or OperationCanceledException))
    {
        result = Data.@this.FromError(new Errors.ServiceError(
            ex.Message, "StepError", 400) { Exception = ex });
    }
    ```
    Result: `ServiceError("Object reference not set to an instance of an
    object.", "StepError", 400)`.

### Empirical confirmation

A transient TUnit test (`AuditorNullVariableVerifyTests`, removed before
commit) reproduced the exact path:

```csharp
var notFound = global::App.Data.@this.NotFound("Name");
notFound.Context = _app.Context;

var resolved = notFound.As<Variable>(_app.Context);

await Assert.That(resolved.Success).IsTrue();         // PASS — error masked
await Assert.That(resolved.Value is null).IsTrue();    // PASS — null Variable
```

And:

```csharp
Variable? v = null;
await Assert.That(() => { var s = (string)v!; return s; })
    .Throws<NullReferenceException>();                // PASS
```

### Impact

- Loses parameter name in error message
- Loses step text in error message
- Loses the `MissingParameter` (or new `MissingRequiredParameter`) error
  key — error categorization collapses to generic `StepError`
- Loses the `Params` snapshot (App.Run line 419 attaches it; Step.RunAsync
  catch at line 157-161 doesn't)
- Same regression shape across **22 handlers**:
  - `variable.{get,set,exists,clear,remove}`
  - `list.{add,any (ListName), contains, count, first, flatten, get,
    group (ListName), indexof, join, last, remove, reverse, set, sort, unique}`
  - `loop.foreach.{ItemName, KeyName}` — these are nullable, so the
    nullable getter (`Emission/Property/Data/this.cs:42`) returns null
    Backing (not null Variable) when `__d.IsEmpty`. The foreach.cs Run()
    line 28 uses `ItemName?.Value?.Name ?? "item"` and line 41 guards
    `if (KeyName != null)` — so foreach is **safe** today via its
    null-conscious code shape, not via the contract.

Trust boundary holds (.pr is signed, so reaching this state requires
malformed signed content) — not an external attacker vector. Severity is
about contract fidelity and developer experience, not security/availability.

### Suggested fix

Generator-side, one shot. Plumb a new flag through Discovery into
ActionClassInfo, mirror the IsSensitive pattern from v5 fix #1:

`PLang.Generators/Discovery/this.cs` — add an `IsRawNameResolvable`
detection on the inner T of `Data<T>` properties (check
`InnerType.AllInterfaces.Any(i => i.Name == "IRawNameResolvable")`).

`PLang.Generators/Emission/Property/Data/this.cs` — add
`IsRawNameResolvable` to the `@this` record, then in the non-nullable
emit branch (line 50) and the non-nullable-with-default branch (line 46):

```csharp
sb.AppendLine(
    $"        get {{ if ({Backing} == null) {{ "
  + $"{Backing} = __ResolveData(\"{ParamName}\").As<{InnerType}>(Context); "
  + $"if (!{Backing}.Success) __resolutionError = {Backing}; "
  + (IsRawNameResolvable
      ? $"else if ({Backing}.Value == null) __resolutionError = "
      + $"global::App.Data.@this.FromError(new global::App.Errors.ServiceError("
      + $"\"Required parameter '{ParamName}' is missing or null\", "
      + $"__step, __callFrames, \"MissingRequiredParameter\", 400)); "
      : "")
  + $"{SetFlag} = true; }} return {Backing}!; }}");
```

(Roughly 10 lines of generator + 1 Discovery flag + 1 ActionClassInfo
field. Per-handler `[IsNotNull]` ×22 is the alternative but brittle — the
next migrated handler will forget.)

The nullable branch (line 42) is intentionally permissive (foreach
ItemName/KeyName uses it for "no destination"); leave it alone.

### Tests required for closure

Per finding #3 below, when fix lands: bulk-parametrize a single test that
runs each of the 22 handlers with no `Name` parameter and asserts
`result.Error.Key == "MissingRequiredParameter"`.

---

## #2 — MINOR — security/v2 [IsNotNull] count is wrong (3 of 22 vs. 0 of 22)

**Category:** review-gap
**Missed by:** security
**File:** `.bot/runtime2-generator-obp/security-report.json`
**Severity rationale:** Minor — affects severity calibration on finding
#1, doesn't change the underlying bug or fix.

security/v2 finding #1 cites:

> only **3 of 22** apply `[IsNotNull]` (`list/any.cs` Key+Operator,
> `list/group.cs`)

The 3 cited are real `[IsNotNull]` decorations, but they're on Key /
Operator properties — not on the Variable slot. The `[IsNotNull]`
validation in `Emission/Action/this.cs:193-208`:

```csharp
if (info.HasAnyIsNotNull) {
    if (__action?.Parameters != null) {
        foreach (var name in info.IsNotNullProperties) {
            if (__action?.Parameters.FirstOrDefault(...).Value == null)
                return FromError(new ServiceError(
                    $"'{lower}' must have a value", ...));
        }
    }
}
```

…iterates over `IsNotNullProperties` only. Decorating Key with
`[IsNotNull]` doesn't help when Name is missing. Counting handlers that
have `[IsNotNull]` somewhere as "Variable-slot guarded" overstates the
defense by 100%.

Verified count of `[IsNotNull]` on `Data<Variable>` slots: **0 of 22.**

```bash
grep -B1 "Data\.@this<Variable>" PLang/App/modules/{list,variable,loop}/*.cs \
  | grep -B0 "IsNotNull"
# (no output — no Data<Variable> slot has [IsNotNull])
```

This compounds the codeanalyzer + tester gap into a finding that reads as
"mostly fine, three need attention" instead of "systematic, all 22 need
the safety net restored."

### Suggested fix

When a future cross-spec review counts decorator coverage on a specific
property kind, the predicate should match the property kind exactly:

```bash
# Right: count [IsNotNull] on Data<Variable> slots specifically
grep -B1 "Data\.@this<Variable>" PLang/App/modules/{list,variable,loop}/*.cs \
  | grep "IsNotNull" | wc -l
```

Worth pinning the counting predicate in the report so reviewers can audit
it.

---

## #3 — MINOR — tester/v7 lacks regression test for missing-parameter contract

**Category:** review-gap
**Missed by:** tester
**File:** `.bot/runtime2-generator-obp/tester/v7/summary.md`
**Severity rationale:** Minor — tester PASSed honestly on the migration's
positive path (35-49 tests deletion-pinning the carve-out). The gap is
that the pre-v7 contract being deleted was never pinned by a test — so
its removal didn't break anything in CI.

tester/v7 ran a deletion-test on the IRawNameResolvable carve-out (35
tests fail when the bypass is disabled — confirmed load-bearing) and on
the implicit Variable→string operator (49+ tests fail when it returns
"MUTATED"). Both verdicts are real. The tester also caught the misnamed
PLNG001 test, the variable.set CopyProperties C# coverage gap, the
WasPercentWrapped value-only pinning, and the IRawNameResolvable contract
trap untested concern. All four findings filed are valid.

What didn't happen: a regression test for the deleted `RawScalarValidations`
contract. Pre-v7 invariant: missing/null `[VariableName]` slot returns
ServiceError with `MissingParameter` key. Post-v7: NRE → StepError.

C# 2550/2550 green is honest but does not pin this contract. A 1-test
parametrize-over-22-handlers pin would have caught the regression at
coder/v7 commit 3 land time, before security/v2's red-team pass.

### Suggested fix

When finding #1 is closed:

```csharp
[Test]
[Arguments("variable", "get", "Name")]
[Arguments("variable", "set", "Name")]
[Arguments("variable", "exists", "Name")]
[Arguments("variable", "remove", "Name")]
[Arguments("variable", "clear", "Name")]
[Arguments("list", "add", "ListName")]
[Arguments("list", "any", "ListName")]
// ... 15 more
public async Task MissingVariableName_ReturnsMissingRequiredParameterError(
    string module, string actionName, string slotName)
{
    var action = MakeAction(module, actionName /* no slotName parameter */);
    var result = await _app.Run(action, _app.Context);
    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredParameter");
}
```

22 deletion-pinned cases in one test definition.

---

## What's clean (the bulk of the work)

These pieces of v7 review the auditor agrees with — listed for the coder
so the fix scope stays narrow:

- **The carve-out itself** — IRawNameResolvable as a marker, public-static
  Resolve dispatched via reflection cache, gated on `T :
  IRawNameResolvable`, runs before %var% substitution. Sound design;
  Path's interpolation contract is preserved (Path doesn't implement the
  marker). Codeanalyzer NIT-4 + tester #2 already raised the
  silent-fallthrough trap; doesn't bite today (Variable is the only T)
  but worth the 3-line `if (resolveMethod == null) FromError(...)` for
  contract safety.

- **Variable record + implicit op + ToString** — heavily deletion-pinned
  (49+ tests), correct semantics, defensible record-equality decision.

- **22-handler property migration** — uniform shape, consistent use of
  `.Value` for the Variable→string read, consistent `.Value.Name` for
  diagnostic interpolation. The migration applied was clean; the missing
  guard is the issue, not the application.

- **variable.set CopyProperties (commit 4)** — verified by 10 plang
  TestReport tests via deletion (tester #1). C# coverage gap is real but
  minor.

- **codeanalyzer/v4 MINOR DRY findings** — TryStaticResolve helper
  (lines 549-562 + 632-644 collapse to one) and IsVariableNameSlot
  duplication. Optional; don't block.

- **Codeanalyzer/v4 NIT findings** — all valid; mostly cosmetic/comment.

The migration's structural contribution to the runtime is clean. The
defect is in the contract-deletion-without-replacement at the missing-
parameter handling layer.
