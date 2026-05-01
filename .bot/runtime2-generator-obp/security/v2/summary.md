# Security v2 — runtime2-generator-obp

## What this is

Re-audit on the same branch after three rounds of work since v1's PASS:

1. **v5/v6** — closed v1 #1 (`__SnapshotParams` ignores `[Sensitive]`) and #3
   (cycle/depth silent passthrough → `FromError`).
2. **architect/v5 → coder/v7** — replaced `[VariableName] string Name` with
   `Data<Variable>` across 22 handler properties. New `App.Variables.Variable`
   record + `App.Variables.IRawNameResolvable` marker + a new bypass branch in
   `Data.AsT_Impl` (lines 549-562) that dispatches the literal slot string to
   `T.Resolve(string, Context)` via reflection when `T : IRawNameResolvable`,
   bypassing `%var%` substitution. Deletes the Legacy property emitter, the
   `__StripPercent` / `__Resolve<T>` / `__HasParam` helpers, the
   `RawScalarValidations` block, and the `[VariableName]` attribute itself.
3. **coder/v7 commit 4** — `variable.set` `CopyProperties` fix (Properties
   survival across binding mint) plus 4 `ListAddIdentityTests` stubs filled in.

Codeanalyzer/v4, tester/v7, auditor/v1 already covered structural/test-honesty
review. My job: red-team the security delta and reconfirm v1 closure.

## What was done

Reviewed five surfaces:

1. **v1 finding closure** — `__SnapshotParams` now emits `IsSensitive`-aware
   masks for both `PrValue` and `FinalValue`
   (`Emission/Property/Data/this.cs:62-72`). Cycle and depth-trip both return
   `FromError` ServiceErrors with the right keys
   (`Data/this.cs:575-578` and `:583-587`). v1 #1 + #3 closed.

2. **`Variable.Resolve`** — pure value-level transformation; no I/O, no
   recursion, total on input (never throws). Edge inputs:
   - `""` / `null` → `Variable{ Name="" }`. Empty-Name footgun.
   - `"%%%"`, `"%%"` → `Variable{ Name="" }`. Empty-Name with WasPercentWrapped=true.
   - `"%a%b%"` → `Variable{ Name="a%b" }`. Mid-string `%` preserved.
   - `"!app"` → `Variable{ Name="!app" }`. Variables.Set will overwrite the
     infrastructure variable. Same surface as legacy `[VariableName]`.

3. **IRawNameResolvable bypass** — `ConcurrentDictionary<Type, MethodInfo?>`
   cache, `BindingFlags.Public | BindingFlags.Static` only, exact signature
   match, condition-gated on the marker. Bypass runs BEFORE `%var%`
   substitution but only fires for marker `T`, so non-marker `Data<T>`
   resolution is unchanged. **Trap**: silent fallthrough when `resolveMethod`
   is null or `resolvedObj is T` is false — exactly the bare-name regression
   the marker was designed to prevent. Codeanalyzer NIT-4 + tester #2 already
   raised this.

4. **22 handler migration** — only **3 of 22** apply `[IsNotNull]`
   (`list/any.cs` Key+Operator, `list/group.cs`). The architect/v5 plan deleted
   the `RawScalarValidations` safety net under the assumption that `[IsNotNull]`
   would cover the missing-parameter case. The remaining 19 handlers crash
   with `NullReferenceException` on null-resolved Variable: `__ResolveData`
   returns NotFound (Value=null), `AsT_Impl<Variable>(null, ctx)` falls through
   every branch to `WrapAs(null, ctx)` → `Data<Variable>{ Value=null,
   Success=true }`. Generated getter only sets `__resolutionError` on
   `!Success`, so handler runs, accesses `Name.Value`, implicit op
   `(string)variable` dereferences null Variable → NRE → App.Run filter
   excludes NRE → uncaught crash.

5. **`variable.set` CopyProperties fix** — restores Properties survival that
   the binding-mint refactor lost. Same semantics as pre-refactor; no new
   attack surface. ListAdd identity stubs are test-only code.

## Findings

Four findings written to `security-report.json`. All low; no critical/high.

- **#1 (low / robustness)** — null-Variable NRE in 19/22 unguarded migrated
  handlers. Pre-v7 returned graceful `ServiceError`; post-v7 NREs uncaught.
  Defense-in-depth fix at the generator level: emit a not-null check on
  `Data<T>` properties whose `T : IRawNameResolvable`, surface as
  `MissingRequiredParameter` ServiceError when `Backing.Value == null`.
- **#2 (low / contract)** — IRawNameResolvable bypass silent fallthrough.
  Future T forgetting `Resolve` reverts silently to the bare-name regression.
  Recommend `FromError` fail-fast when `resolveMethod == null` for marker T.
- **#3 (low / informational)** — `Variable.Resolve` accepts edge inputs that
  produce empty / special-character Names. Same surface as legacy
  `[VariableName]`; not a v7 regression.
- **#4 (low / standing from v1)** — `Variables.Resolve(skipInfrastructure=false)`
  default. Carries forward unchanged.

Verdict: **PASS**. No critical/high open.

## Code example — the null-Variable trap

`PLang/App/modules/variable/get.cs`:

```csharp
public partial class Get : IContext
{
    public partial Data.@this<Variable> Name { get; init; }

    public Task<Data.@this> Run()
    {
        return Task.FromResult(Context.Variables.Get(Name.Value));
        //                                          ^^^^^^^^^^
        // If slot value is null OR slot is missing:
        //   AsT_Impl<Variable>(null, ctx) -> Data<Variable>{Value=null, Success=true}
        //   getter doesn't set __resolutionError (Success was true)
        //   Name.Value is null Variable
        //   implicit op (string)null Variable -> NRE on v.Name
        //   App.Run filter (NRE excluded) -> uncaught -> crash
    }
}
```

Same shape in 18 other migrated handlers. Pre-v7 the `RawScalarValidations`
block emitted `ServiceError("Required parameter missing")`. Post-v7, NRE.

## Proposed fix for finding 1

`PLang.Generators/Emission/Property/Data/this.cs:EmitProperty`, after the
existing `__resolutionError` capture:

```csharp
if (!IsNullable && IsRawNameResolvable)
{
    sb.AppendLine($"        get {{ if ({Backing} == null) {{ {Backing} = __ResolveData(\"{ParamName}\").As<{InnerType}>(Context); if (!{Backing}.Success) __resolutionError = {Backing}; else if ({Backing}.Value == null) __resolutionError = global::App.Data.@this.FromError(new global::App.Errors.ServiceError(\"Required parameter '{ParamName}' is missing or null\", \"MissingRequiredParameter\", 400)); {SetFlag} = true; }} return {Backing}!; }}");
}
```

Closes the gap at the contract boundary instead of relying on each handler to
remember `[IsNotNull]`. `IsRawNameResolvable` plumbed through Discovery the
same way `IsSensitive` was for the v1 #1 fix.

## What's next

If reviewer accepts as-is: recommend running the **auditor**.
If reviewer wants finding 1 closed first: send to **coder** with the
generator-side fix above as the targeted change.
