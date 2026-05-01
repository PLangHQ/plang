# v2 — Security re-audit on the Variable + IRawNameResolvable migration

## What this is

Branch landed three rounds of work since security/v1's PASS:

1. **v5/v6** — closed v1 finding #1 (`__SnapshotParams` ignores `[Sensitive]`) and #3
   (cycle/depth silent passthrough). Verified below.
2. **architect/v5 → coder/v7** — replaces `[VariableName] string Name` with
   `Data<Variable>` across 22 handler properties. Adds `App.Variables.Variable`
   record, `App.Variables.IRawNameResolvable` marker, and a new bypass branch in
   `Data.AsT_Impl` (lines 549–562) that dispatches the literal slot string to
   `T.Resolve(string, Context)` via reflection when `T : IRawNameResolvable`,
   bypassing the `%var%` substitution branch entirely. Deletes the Legacy
   property emitter, `__StripPercent`/`__Resolve<T>`/`__HasParam` helpers,
   `RawScalarValidations` block, and the `[VariableName]` attribute.
3. **coder/v7 commit 4** — `variable.set` `CopyProperties` fix (Properties
   survival across binding mint) and 4 `ListAddIdentityTests` stubs filled in.

Codeanalyzer/v4, tester/v7, auditor/v1 already covered structural/test/test-honesty
review. My job: red-team the security delta only — what new attack surface, what
old finding got closed, what regressions did the architectural change introduce.

## v1 finding closure (confirmed against current code)

| v1 # | severity | status now | evidence |
|---|---|---|---|
| 1 | medium | **fixed** | `Emission/Property/Data/this.cs:62-72` emits `IsSensitive`-aware mask for both `PrValue` and `FinalValue`. Discovery surfaces `IsSensitive` from `[Sensitive]` on the property symbol. |
| 2 | low | open (unchanged) | `Variables.Resolve(input, skipInfrastructure: false)` default still in place (`PLang/App/Variables/this.cs:393`). Convention finding, not regression. |
| 3 | low | **fixed** | `Data/this.cs:575-578` returns `FromError(VariableResolutionCycle)`; `Data/this.cs:583-587` returns `FromError(ResolveDepthExceeded)`. Both branches now ServiceError instead of silent passthrough. |
| 4 | low | bounded | `Errors/Error.cs:280-296` `FormatVerboseValue` truncates strings at 200 chars and JSON containers at 300 chars. With v1 #1 fix layered above, defense-in-depth holds. |

Two of four v1 findings closed. Two remain low/bounded.

## New attack surface (delta)

### 1. `App.Variables.Variable` value record + `Variable.Resolve(string, Context)`

`PLang/App/Variables/Variable.cs:44-52`:

```csharp
public static Variable Resolve(string raw, Actor.Context.@this context)
{
    if (string.IsNullOrEmpty(raw))
        return new Variable("", raw ?? "", false);
    var trimmed = raw.Trim('%');
    var wasPercentWrapped = raw.Length >= 2 && raw[0] == '%' && raw[^1] == '%';
    return new Variable(trimmed, raw, wasPercentWrapped);
}
```

Trust boundary: `raw` is the literal slot string from a signed `.pr`. Threat
model: signed = trusted. The function does no resolution, no I/O, no
cross-context reads — pure value-level transformation. Safe.

Edge inputs reviewed:
- `""` / `null` → `Variable{ Name="", RawValue="", WasPercentWrapped=false }`. Empty Name.
- `"%"` → `Variable{ Name="", RawValue="%", WasPercentWrapped=false }`.
- `"%%"`, `"%%%"` → `Variable{ Name="", WasPercentWrapped=true }` (length≥2 + both ends `%`). Empty Name.
- `"%a%b%"` → `Variable{ Name="a%b" }`. `Trim('%')` only strips leading/trailing.
- `"foo.bar"` → `Variable{ Name="foo.bar" }`. Variables.Set then parses dot-path.
- `"!app"` → `Variable{ Name="!app" }`. Variables.Set will overwrite an
  infrastructure variable. Same surface as the legacy `[VariableName]` path —
  not a v7 regression. .pr signing is the gate.
- Whitespace: `"  x  "` → `Variable{ Name="  x  " }`. `Variables.Set("  x  ")`
  then trims via `CleanName` to `"x"`. Asymmetry between Variable.Name and the
  stored key — a footgun, not a vulnerability.

### 2. `IRawNameResolvable` marker + `AsT_Impl` bypass (Data/this.cs:549-562)

```csharp
if (raw is string rawNameStr && ctx != null
    && typeof(App.Variables.IRawNameResolvable).IsAssignableFrom(typeof(T)))
{
    var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
        t.GetMethod("Resolve",
            BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(Actor.Context.@this) }, null));
    if (resolveMethod != null)
    {
        var resolvedObj = resolveMethod.Invoke(null, new object[] { rawNameStr, ctx });
        if (resolvedObj is T result)
            return ConstructWrap<T>(result, ctx);
    }
}
```

Reviewed:

- **Reflection cache safety**: `ConcurrentDictionary<Type, MethodInfo?>`,
  keyed by runtime `T`. Bounded by the number of distinct generic instantiations
  compiled into the app. Same cache the line-632 Path-style branch uses; both
  look up the same method signature. No confusion.
- **Public-static-only lookup**: `BindingFlags.Public | BindingFlags.Static`.
  Cannot pull a private/internal `Resolve` into the dispatch.
- **Signature is exact**: `(typeof(string), typeof(Actor.Context.@this))`. Only
  fires for types that explicitly declare this signature. Coincidental
  collisions with library types that take `(string, Context.@this)` from this
  exact namespace are improbable.
- **Reachable input**: `raw` is a slot string from a signed `.pr`. Trust holds.
- **Ordering**: bypass runs BEFORE `%var%` substitution. For `Data<Variable>`
  this is intentional (the slot is asking for the variable's identity, not
  value). For non-marker `T` (e.g. `Data<string>`) the bypass condition is
  false, substitution runs as before. **No exposure widening for existing T.**
- **Exception safety**: no `try/catch` around `Invoke`. Variable.Resolve doesn't
  throw on any input. Future T whose Resolve throws would surface as
  `TargetInvocationException` → caught by App.Run's catch (TargetInvocationException
  is not in the NRE/OOM/SOE exclusion list) → mapped to ServiceError. Acceptable.
- **Silent fallthrough**: if `resolveMethod == null` (T is marker but no public
  static `Resolve` matches the signature) or `resolvedObj is T result` returns
  false (Resolve returned wrong type), execution falls through to the `%var%`
  substitution branch. That's exactly the "bare-name regression" the marker
  was designed to prevent. Today only Variable implements the marker so the
  trap is theoretical, but a future T forgetting Resolve would silently
  re-introduce it. **Codeanalyzer NIT-4 and tester #2 already raised this.**
  Logged below as a low defense-in-depth finding.

### 3. Removal of the `RawScalarValidations` safety net

`PLang.Generators/Emission/Action/this.cs` previously emitted a missing-parameter
`ServiceError` for `[VariableName] string Foo` slots when the slot was absent or
the value was empty. v7 commit 3 deleted that block under the assumption that
`[IsNotNull]` would cover the missing/empty case (architect/v5 plan §
"Source generator: what collapses").

Reality (verified by `grep -rn "\[IsNotNull\]" /workspace/plang/PLang/App/modules/{variable,list,loop}`):
only **3 of the 22** migrated handlers apply `[IsNotNull]` — `list/any.cs` Key
+ Operator (not ListName), `list/group.cs`. The other 19 carry no null guard.

Trace of the failure mode for the 19 unguarded handlers when slot value is
null OR slot is absent in `.pr`:

1. `__ResolveData("Name")` returns either the slot Data with `Value=null`, or
   `Data.NotFound("Name")` with `Value=null`. Both have `Value == null`.
2. `.As<Variable>(Context)` enters `AsT_Impl<Variable>(null, ctx)`.
3. The bypass at line 549 fails (`raw is string` is false on null). The `%var%`
   branch fails. The walkable-container branch fails. The Path-style branch at
   line 632 fails. Execution lands at `WrapAs<Variable>(null, ctx)` →
   `ConstructWrap<Variable>(default, ctx)` → returns
   `Data<Variable>` with `Value=null`, `Success=true`. **No error captured.**
4. The generated property getter (`Emission/Property/Data/this.cs:50`) sets
   `__resolutionError` only `if (!Backing.Success)`. Skipped. Handler runs
   with `Backing` non-null but `Backing.Value == null`.
5. Handler does `Variables.Get(Name.Value)` (or `Set`, `Contains`). `Name.Value`
   returns `null` Variable. Implicit operator `op_Implicit(Variable v) => v.Name`
   dereferences null → `NullReferenceException`.
6. App.Run's catch filter (`PLang/App/this.cs:415`) excludes NRE deliberately.
   Action dispatch crashes uncaught.

Pre-v7 (v1-baseline) behavior: legacy emit path produced an empty/null string,
the `RawScalarValidations` block emitted `ServiceError("Required parameter
missing")`, App.Run mapped to graceful error.

Post-v7 behavior: NRE → unrecoverable.

**Severity-relative-to-threat-model assessment**: PLang is user-sovereign;
.pr is signed. A null/missing Name slot in a signed .pr is internal trust
breach (developer/LLM emission bug, hand-edited .pr, regression in builder
pipeline). Within strict threat boundary, this is a robustness issue, not a
vulnerability — same severity rule the discipline memory codifies.

But: the App.Run filter's NRE exclusion is a **deliberate** design decision —
NRE is treated as unrecoverable because it usually signals a real bug. v6 had
a graceful-error path for this case. v7 lost it. Defense-in-depth dimension
is real.

Logged below as low. Not a blocker.

### 4. `variable.set` CopyProperties fix (commit 4)

`PLang/App/modules/variable/set.cs:103-108`:

```csharp
private static void CopyProperties(Data.@this source, Data.@this target)
{
    if (source.Properties.Count == 0 || ReferenceEquals(source, target)) return;
    foreach (var p in source.Properties)
        target.Properties.Set(p.Name, p.Value, p.Type);
}
```

Restores Properties survival that the binding-mint refactor lost. Iterates
source's Properties (a Variables collection on Data) and sets each on target.
Per-Property `.Value` is `object?` — the raw value, not the Data. Variables.Set
re-wraps in fresh Data, so subscribers don't follow but raw mutable refs
(lists/dicts) are shared. **This was the pre-refactor behavior.** Not a new
attack surface — restoration of prior semantics.

If a Property's value is sensitive — e.g. test runs producing a snapshot
Property carrying secrets — the same disclosure surface the standing
`Variables.Snapshot()` finding tracks. No worsening.

### 5. `[VariableName]` attribute removal

The attribute is gone from `App/modules/Attributes.cs`. Catalog-builder
detection moved to `IsVariableNameSlot(propType)` (checks `Data<T> where
T : IRawNameResolvable`). Reflection lookups for the attribute name
elsewhere in the project: zero (verified by `grep`). No dangling references
that would silently fail.

## Findings to log in security-report.json

1. **Low / robustness** — null-Variable NRE in 19 unguarded migrated handlers.
   See §3 above. Concrete fix options:
   - Generator: emit a not-null check on `Data<T>` properties where T implements
     `IRawNameResolvable`, surface as ServiceError when Value resolves null.
   - Variable: make `op_Implicit` and `ToString` null-safe (e.g. return `""`).
   - Per-handler: apply `[IsNotNull]` on all 22 sites — closest to the
     architect/v5 stated intent.

   The generator-side fix is the most defensible — preserves the architect's
   "Resolve produces non-null Variable, .Value is never null" contract by
   enforcing it at the wrapper level instead of relying on each handler.

2. **Low / contract trap** — IRawNameResolvable bypass silent fallthrough.
   See §2 above. Codeanalyzer NIT-4 + tester #2 also raised this. Suggest
   FromError fail-fast when `resolveMethod == null` for a marker T.

3. **Low / informational** — `Variable.Resolve` accepts edge inputs that
   produce empty or special-character Names. `"%%%"` → empty Name; `"!app"`
   → infrastructure-variable Name; mid-string `%` preserved. Same surface as
   legacy `[VariableName]`; not a v7 regression. Defensive validation
   (e.g. reject empty Name, normalize `%` strip identical to `CleanName`)
   would harden but is not required by the threat model.

4. **Low / standing from v1** — Variables.Resolve(input, skipInfrastructure=false)
   default. Unchanged. Carry forward.

## Verdict

Blue + Red, no critical/high open. Two v1 findings closed (Sensitive masking,
cycle/depth → ServiceError). Three new low/informational on the v7 delta.
Standing v1 #2 carries forward. **Verdict: PASS.**

## Hand-off

- If reviewer accepts as-is: recommend **auditor**.
- If reviewer wants finding 1 closed: send to **coder** with the generator-side
  not-null-check fix as the targeted change. Localized to
  `Emission/Property/Data/this.cs:EmitProperty` — emit a `Backing.Value == null
  ? __resolutionError = FromError(...)` arm when `T : IRawNameResolvable`.
