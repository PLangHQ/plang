# coder/v7 — Variable replaces [VariableName] via Data<Variable> + IRawNameResolvable bypass

## What this is

Architect/v5 handed off a plan to retire the `[VariableName]` attribute, the
Legacy property emitter, and the parallel `__Resolve<T>`/`__StripPercent`/
`__HasParam` helper family. Replacement: introduce `App.Variables.Variable` (a
record carrying `Name`, `RawValue`, `WasPercentWrapped`), declare every former
`[VariableName] string` slot as `Data<Variable>`. The architect's claim was
that the existing `Data.As<T>` static-Resolve dispatch (line 612-624) would
route `Data<Variable>.As<Variable>(ctx)` to `Variable.Resolve` automatically —
no new wiring needed.

## What was done

Three commits (62947bc plus three more on this branch, total v7 = 3 commits):

1. **3bd0e9a5** — Add `App.Variables.Variable` (record + implicit-string +
   `Resolve` + `ToString=Name`), add `App.Variables.IRawNameResolvable` marker
   interface, extend `Data.AsT_Impl` with a raw-name carve-out (one block
   before the `%var%` substitution branch). Variable implements the marker.
   8 unit tests in `VariableResolveTests.cs`.

2. **68acfad8** — Migrate 22 handler property declarations from
   `[VariableName] partial string X` to `partial Data.@this<Variable> X`.
   Use sites updated to `.Value` (Variable→string implicit conversion fires).
   Plus catalog-builder adjustments (App/Modules/this.cs, ExampleRenderer.cs)
   and Variable single-arg ctor for direct C# composition in tests.

3. **ecba2e33** — Delete the dead code: `Emission/Property/Legacy/this.cs` (file),
   `[VariableName]` attribute, `IsAppResolvable` detection, `ScanRawScalarValidations`,
   `__Resolve<T>` / `__HasParam` / `__StripPercent` helpers, the RawScalarValidations
   emit block. PLNG001 collapses to a two-rule gate (`Data<T>` or `[Provider] T`).
   Activated 5 PLNG001PostMigration tests that were stubs (`Assert.Fail("Not implemented")`).

## The unplanned but necessary extension

The architect's plan asserted "Data<Variable>.As<Variable>(ctx) for a `value="%x%"` slot ...
finds Variable.Resolve and calls it with the raw string `%x%`." Empirically false
(test SlotData_PercentWrapped_AsVariable_NameIsX failed with NRE):

- `Data.AsT_Impl<Variable>` for raw `"%x%"` enters the `%var%` substitution branch
  (line 546) BEFORE reaching the static-Resolve dispatch (line 612).
- TryFullVarMatch extracts varName="x", calls `Variables.Get("x")`.
- For `set %x% = 5` creating a new variable, x doesn't exist → returns NotFound
  with `Value=null`. Handler crashes on `.Value.Name` / implicit-string op.

The plan worked for bare `value="x"` (no `%` → falls through to line 612), but
not for the dominant `%x%` slot form. After empirical evidence, Ingi approved
**Option 1** (marker interface bypass) over reordering AsT_Impl globally (which
would break Path's interpolation contract — `"/foo/%bar%/baz.txt"` needs `%bar%`
substituted before Path.Resolve sees it).

```csharp
// PLang/App/Variables/IRawNameResolvable.cs — marker
public interface IRawNameResolvable { }

// PLang/App/Variables/Variable.cs — implements marker
public sealed record Variable(string Name, string RawValue, bool WasPercentWrapped)
    : IRawNameResolvable
{
    public static implicit operator string(Variable v) => v.Name;
    public static Variable Resolve(string raw, Actor.Context.@this context) { ... }
    public override string ToString() => Name;
}

// PLang/App/Data/this.cs AsT_Impl — bypass added BEFORE %var% substitution
if (raw is string rawNameStr && ctx != null
    && typeof(App.Variables.IRawNameResolvable).IsAssignableFrom(typeof(T)))
{
    var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), ...);
    if (resolveMethod != null)
    {
        var resolvedObj = resolveMethod.Invoke(null, new object[] { rawNameStr, ctx });
        if (resolvedObj is T result)
            return ConstructWrap<T>(result, ctx);
    }
}
```

Path is unchanged because Path doesn't implement IRawNameResolvable — its
`%var%` interpolation contract is preserved.

## Code example — handler migration shape

```csharp
// before
public partial class Get : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial Data.@this<int> Index { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        var item = data.GetChild($"[{Index.Value}]");
        if (!item.IsInitialized)
            return Task.FromResult(Error(new App.Errors.ValidationError(
                $"Index {Index.Value} out of range for '{ListName}'")));
        return Task.FromResult(Data(item.Value));
    }
}

// after
using App.Variables;

public partial class Get : IContext
{
    public partial Data.@this<Variable> ListName { get; init; }
    public partial Data.@this<int> Index { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);  // Variable→string implicit
        var item = data.GetChild($"[{Index.Value}]");
        if (!item.IsInitialized)
            return Task.FromResult(Error(new App.Errors.ValidationError(
                $"Index {Index.Value} out of range for '{ListName.Value}'")));  // ToString=Name
        return Task.FromResult(Data(item.Value));
    }
}
```

The 22 sites all follow this pattern. Two variants:

- **Pattern A** (write target / mutation): `list/add`, `list/remove`, `list/reverse`,
  `list/set`, `list/sort`, `variable/remove`, `variable/set`. Use sites:
  `Context.Variables.Set(X.Value, ...)`.
- **Pattern B** (read by name): the rest. Use sites:
  `Context.Variables.Get(X.Value)` or interpolation.
- **Nullable variant** (`loop/foreach.cs` ItemName, KeyName): use
  `X?.Value?.Name ?? default` because `?.Value` returns nullable Variable
  which doesn't chain through the implicit operator.

## Catalog/prompt-facing adjustments

Two test failures (`GetActions_VariableNameParams_Marked`,
`Add_NaturalForm_RendersAddThenSet`) revealed that the catalog-builder and
example-renderer were detecting the deleted `[VariableName]` attribute via
reflection. Replaced with `IsVariableNameSlot(propType)` (checks
`Data<T>` where T : IRawNameResolvable):

- `App/Modules/this.cs` Describe() — `%var% string` advertised when slot is Variable-shape.
- `App/Catalog/ExampleRenderer.cs` LookupParamTypeName() — returns "string" for
  IRawNameResolvable T (so the LLM sees `Name([string] %sum%)`, not
  `Name([variable] %sum%)`).

## Test contract delta

| Surface       | Before v7 | After v7 | Delta |
|---|---|---|---|
| C# total      | 2554      | 2554     | (8 added in commit 1, 5 PLNG001 stubs activated; net 0 on count) |
| C# pass       | 2545      | 2550     | +5 (PLNG001 stubs filled in) |
| C# fail       | 9         | 4        | -5 (4 left are pre-existing ListAdd Identity stubs from runtime2-data-share-state) |
| plang pass    | 160       | 160      | 0 |
| plang fail    | 16        | 16       | 0 (same TestReport / sensitive-fixture failures from before commit 1) |

8 new VariableResolveTests cover:
- Resolve symmetry: `%x%` and `x` both yield Name="x"
- Empty string handles cleanly
- Slot Data with `%x%` → As<Variable> → Name="x" via the bypass (load-bearing for the migration)
- Slot Data with `%x%` when "x" exists in Variables — bypass STILL produces Name="x"
  (the plan's "ignores existing value" assertion)
- Slot Data with bare `x` → Name="x" via static-Resolve fallthrough
- Implicit Variable→string conversion
- ToString() returns Name

## CLAUDE.md proposals appended

Three entries in `.bot/runtime2-generator-obp/claude-md-proposals.md`:

1. `/PLang/App/CLAUDE.md` — replaces the v1 three-rule "Property kinds (v4)"
   section with the post-v5 two-rule contract + Variable description.
2. `/Documentation/v0.2/good_to_know.md` — new section on Variable, including
   the `var foo = X.Value` infers Variable not string gotcha and what
   WasPercentWrapped is for.
3. `/Documentation/Runtime2/todos.md` — close v6's `[VariableName]` migration
   hand-off entry.

## Hand-off

Suggest **codeanalyzer** next. Surface to review:
- `Data.AsT_Impl` carve-out — is the marker check appropriately placed and
  cached? (Note `ResolveMethodCache` is shared with the line-612 path.)
- Variable's three-field record + its single-arg helper ctor + ToString
  override. Equality stays default per architect's documented decision
  ("Variable equality is technically loose").
- The `[VariableName]` attribute being literally gone from `Attributes.cs`.
  Any reflection elsewhere expected its existence? `App/Modules/this.cs`
  was updated; an unscoped grep on the project shows no remaining
  `VariableNameAttribute` references in code (only stub class definitions in
  test fixtures, removed).
- 5 PLNG001PostMigrationTests are now real — not just shape, they drive the
  actual generator. The same harness GeneratorValidationTests uses.

The 4 remaining failing C# tests (`ListAddIdentityTests`) are all stubs
(`Assert.Fail("Not implemented")`) from the runtime2-data-share-state branch.
Outside this v7 scope.
