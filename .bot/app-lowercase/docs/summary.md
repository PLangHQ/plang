# docs — app-lowercase

## Version
v1

## What this is

Final docs gate for the `App` → `app` namespace rename + seven OBP folder
merges that collapsed each case-pair (`App/Cache` + `app/modules/cache`, etc.)
into a single `app/modules/<name>/` home.

Upstream verdicts: codeanalyzer PASS, tester PASS, security PASS. My job:
apply the coder's CLAUDE.md proposal, scrub the remaining `App.X` drift in
docstrings and `Documentation/`, rewrite `app-tree.md` (which still showed
the pre-merge top-level shape), and call the merge verdict.

## What was done

### CLAUDE.md proposal applied to `/CLAUDE.md`

- Three stale-line fixes per coder v3 proposal (Console.* example, PLNG001
  property-kind gate, test alias clash).
- New first bullet in "Runtime2 Conventions" documenting the lowercase
  vocab / PascalCase infra rule, the seven merged engine concepts under
  `app/modules/`, the **property-name PascalCase carve-out** (the single
  most error-prone aspect of the rename), the `default` keyword carve-out
  at `app/filesystem/Default/`, and the two pending PLang action renames
  (`environment.run`, `builder.load`).

### Docstring + comment scrub

Ten sites flagged by codeanalyzer R1 + R2 lowercased to namespace-position
references. Property-position references (`ctx.App.FileSystem`, `App.Tester`,
…) deliberately left PascalCase per the convention.

Files touched: `PLang/app/data/this.cs`, `PLang/app/GlobalUsings.cs`,
`PLang/app/channels/channel/events/this.cs`, `PLang/app/types/Registry.cs`,
`PLang/app/callstack/call/Position.cs`, `PLang/app/modules/settings/IStore.cs`,
`PLang/app/errors/CallbackGoalErrors.cs`, `PLang.Generators/Discovery/this.cs`,
`PLang.Tests/GlobalUsings.cs`.

### Documentation sweep

- **`Documentation/v0.2/app-tree.md`** — rewrote. Lowercased all paths;
  remapped Cache / Builder / Callback / Settings / Modules / Code / Debug
  rows to `app/modules/<name>/` (no longer top-level); added a
  Case-Convention section with the property-vs-type rule; renamed module
  `app` → `environment`; noted `builder.app` → `builder.load`.
- **`Documentation/v0.2/{build,build_process,builder-data-t-roadmap,todos}.md`**
  and **`Documentation/Runtime2/todos.md`** — path references lowercased,
  merged-folder paths remapped.
- Mid-session Ingi spotted `Documentation/v0.2/todos.md:422` still
  pointing at the bogus `builder/providers/DefaultBuilderProvider.cs`
  path. Fixed: real location is `PLang/app/modules/builder/code/Default.cs:18`,
  and the `_buildTimer` field is already `private readonly` there — so
  the underlying multi-App concern is resolved at that site (kept in
  todos.md as an example for the rule).

### What I deliberately did **not** touch

- Property-access references in docs and code (`ctx.App.X`,
  `context.App.Debug.Write(...)`, `App.Builder.IsEnabled`, etc.) — those are
  the `App` property on `app.@this` and stay PascalCase by rule.
- `app/data/Code/` folder rename (codeanalyzer S1) — coder work, not docs.
- `environment.run` / `builder.load` deliberate naming pass (S3) — Ingi's
  call before next release.

## Code example — the property-vs-type rule

The doc convention now in `/CLAUDE.md`:

```csharp
// Property access on the app instance — PascalCase (it's a property name).
await context.App.FileSystem.Read(path);
await context.App.Debug.Write("…");
ctx.App.Variables.Set("x", 1);

// Type reference — lowercase (it's a namespace path).
Data<app.variables.Variable> Foo { get; init; }
new app.callstack.@this(…);
global::app.channels.@this.Output
```

The same name (`Variables`) appears in both forms in working code. Knowing
which case to use without the rule is impossible; with the rule, it's
mechanical: ask whether you're naming a property or a type.

## Verdict

**PASS — ready to merge into `runtime2`.**

Build clean (0 errors, 447 warnings, all pre-existing nullable warnings).
Upstream gates all PASS.
