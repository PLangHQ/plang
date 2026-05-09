# CLAUDE.md proposals — runtime2-cleanup

## coder — v29 — 2026-05-09
**Target:** /CLAUDE.md
**Why:** v29 renames `[Provider]` → `[Code]` (attribute class `ProviderAttribute` → `CodeAttribute`), aligning the attribute with the runtime escape-hatch (`app.Code.Get<T>()`) which had already been renamed in an earlier sweep. The PLNG001 message and all 50 module call sites are updated; the CLAUDE.md text at line 39 still names the old attribute and should follow.
**Proposed change:**

Replace this bullet (currently at `/CLAUDE.md:39`):

```markdown
- **Property kinds (PLNG001 build-time gate)**: action handler properties must be `Data<T>` (or plain `Data`) or `[Provider] T`. Anything else fails the build with `PLNG001`. For parameters that *name* a variable (write targets, read-by-name lookups: `variable.set`, `list.*`, `loop.foreach` ItemName/KeyName), use `Data<App.Variables.Variable>`. `Variable` implements `IRawNameResolvable`, which tells `Data.As<T>` to skip its `%var%` substitution branch and dispatch to `Variable.Resolve(raw, ctx)` directly — both `value="%x%"` and bare `value="x"` collapse to `Variable { Name = "x" }`. Use sites read `Foo.Value` (Variable's implicit `string` operator covers method-call boundaries; `ToString() => Name` makes interpolation read naturally). Non-nullable `Data<Variable>` slots get a generator-emitted pre-Run guard that surfaces `MissingRequiredParameter` (auto-detected via the `IRawNameResolvable` marker through Discovery → ActionClassInfo → Action emitter, mirroring `[IsNotNull]`).
```

with:

```markdown
- **Property kinds (PLNG001 build-time gate)**: action handler properties must be `Data<T>` (or plain `Data`) or `[Code] T` (eagerly resolved from `app.Code.Get<T>()`). Anything else fails the build with `PLNG001`. For parameters that *name* a variable (write targets, read-by-name lookups: `variable.set`, `list.*`, `loop.foreach` ItemName/KeyName), use `Data<App.Variables.Variable>`. `Variable` implements `IRawNameResolvable`, which tells `Data.As<T>` to skip its `%var%` substitution branch and dispatch to `Variable.Resolve(raw, ctx)` directly — both `value="%x%"` and bare `value="x"` collapse to `Variable { Name = "x" }`. Use sites read `Foo.Value` (Variable's implicit `string` operator covers method-call boundaries; `ToString() => Name` makes interpolation read naturally). Non-nullable `Data<Variable>` slots get a generator-emitted pre-Run guard that surfaces `MissingRequiredParameter` (auto-detected via the `IRawNameResolvable` marker through Discovery → ActionClassInfo → Action emitter, mirroring `[IsNotNull]`).
```
