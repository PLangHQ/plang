# codeanalyzer — typed-action-returns

## Version
v1

## What this is

Branch `typed-action-returns` introduces typed action return plumbing in five stages.
Stage 0 — the foundation — landed across six coder commits. This codeanalyzer pass
reviews Stage 0 production code only; Stages 1–4 are not yet implemented.

Stage 0 ships:
- `IClass` interface on every action handler with an optional `Build()` hook (default `Data.Ok()`).
- `Default.RunBuildPass` in `builder.code` — runs each action's `Build()` during validate;
  a returned `typeName` stamps the trailing `variable.set`'s `Type` parameter.
- `Channels.Channel(name)` — named-channel lookup with a no-op fallback sink
  (`channels.channel.noop.@this`).
- `builder.warning.@this` record — `(IClass Action, string Message)` payload destined for
  the `"builder"` channel.
- `Data.As<T>` made `internal` (generator-only); new public `Data.As(string typeName)` for
  explicit cross-type coercion.
- `[PlangType]` attribute slimmed to a single optional `Name` override; metadata
  (`Example`, `Description`, `Shape`) moved to static-property convention read via reflection.
- Source-generator `EmitSetAction` — public `SetAction(action, context)` on every handler
  partial so `RunBuildPass` can prime lazy property getters before invoking `Build()`.

## What was done

Read the production diff `c4404b9c5..3c8285760` (24 files, ~865 insertions). Ran the
five-pass analysis. Wrote findings to `v1/report.md` and the verdict to `v1/verdict.json`.

### Findings (all LOW)

1. **`[PlangType]` `AllowMultiple = false` vs. Registry multi-attr loop**
   `PlangTypeAttribute.cs:30` declares `AllowMultiple = false`, but `Registry.cs:126–144`
   iterates attrs as if multiple could be present, and `Registry.cs:13–14` documents
   alias support that no longer exists. Pick one.
2. **`Default.cs:543` discards `err`** from `GetCodeGenerated`. Pattern is `(handler, _)`.
3. **`IClass.cs:22–23`** uses fully-qualified `System.Threading.Tasks.Task.FromResult` —
   missing `using` directive. Cosmetic.
4. **`Default.cs StampOnTerminalVariableSet` (564–580)** — insert path sets
   `Type = data.type("string")`, replace path leaves Type untouched. Asymmetry; benign today.
5. **`channels/this.cs:111–114`** — local `channel` shadows context. Rename to `ch`.
6. **`data/this.cs As(string typeName) (471–488)`** — does NOT propagate Properties /
   event lists, unlike every other type-converting path here (which routes through
   `ConstructWrap`). Likely intentional but doc it.
7. **Design call: `IClass.SetAction` has no default body** — every implementer must
   provide it. Production handlers get it from the generator; hand-rolled IClass
   impls (future test fixtures) would need to write it. Consider an empty default,
   or doc the requirement.
8. **Test-file only: `Handlers.cs:49`** — `BuildOrdered.InvocationLog` is shared
   mutable test state. Safe today (Setup clears, runs are serial in this class);
   would race under TUnit parallelisation.

### Verdict

**FAIL — NEEDS WORK (low severity).** No blocker; all findings are polish that
can land as a follow-up. The architectural decisions in this stage (Build()
contract, named channels, attribute slim, materialization API split) are sound
and consistent with the architect + handoff documents.

## Code example

The cleanest illustration of the new contract — `BuildReturnsType` test handler
exercising the full path (Handlers.cs:18–24):

```csharp
[global::app.modules.Action("buildreturnstype")]
public partial class BuildReturnsType : global::app.modules.IContext
{
    public partial global::app.data.@this<string>? Tag { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult(global::app.data.@this.Ok());
    public Task<global::app.data.@this> Build() => Task.FromResult(global::app.data.@this.Ok("foo"));
}
```

Validate calls `SetAction` (generator-emitted), then `Build()`; the `"foo"`
return stamps the trailing `variable.set`'s `Type` parameter. That's Stage 0
in one snippet.
