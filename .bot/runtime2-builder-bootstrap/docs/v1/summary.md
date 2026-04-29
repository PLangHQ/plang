# Docs v1 — runtime2-builder-bootstrap

## What this is

Pre-merge documentation gate after auditor PASS, security PASS, tester APPROVED, codeanalyzer v4 CLEAN. Branch ships the v2 self-rebuild pipeline plus three coder-described gaps (`variable.set` AsDefault, `file.read` ResolveVariables, single→list auto-wrap) plus a wide swath of architecture work — Catalog system, Trace identity, ParamSnapshot, granular LLM debug, `formal` Fluid filter, four new builder actions, BuildingGuard removal.

The branch already shipped meaningful doc work on its own: `Documentation/v0.2/action-catalog.md` (NEW, 234 lines), `Documentation/v0.2/trace.md` (NEW, 95 lines), `Documentation/v0.2/debug.md` (76 lines added), `good_to_know.md` (test invariants + ExamplesForLlm rule), and XML docs on every new public class I checked. So this gate is mostly about closing user-facing gaps and stale references, not authoring new architecture.

## What was done

Six gaps identified, all six filled:

| ID | Where | Severity | What |
|---|---|---|---|
| G1 | `docs/modules/variable.md` | major | Add `AsDefault` parameter row + `set default` example + sub-section on goal-parameter fallback. |
| G2 | `docs/modules/file.md` | major | Add `ResolveVariables` parameter row + `load vars` example + sub-section + security paragraph on `skipInfrastructure`. |
| G3 | `docs/modules/builder.md` | major | Replace stale BuildingGuard section; add entries for `validateResponse`, `enrichResponse`, `promoteGroups`, `merge` (step-level); rewrite pipeline ordering to current 11-step flow; cross-reference `action-catalog.md`. |
| G4 | `Documentation/v0.2/good_to_know.md` | minor | Rewrite stale BuildingGuard paragraph (line 424) — name removal commit, current `App.Build.IsEnabled` check, no per-action guard. |
| G5 | `Documentation/v0.2/architecture.md` | minor | Add `Trace` to Context shape diagram with pointer; add Modules-section paragraph pointing at `action-catalog.md`. |
| G6 | `PLang/App/modules/builder/merge.cs` | minor | Add `///` class summary + per-property summaries; bring `merge` in line with the other new builder action classes. |

Verified the C# build still succeeds with the XML doc edit (`dotnet build PLang.csproj` → 0 errors).

## Code example

The pattern of these doc edits — close the loop between code and the user-facing module doc:

Before in `docs/modules/file.md`:

```markdown
**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Path | string | yes | Path to the file |
```

After (parameter row + worked example + security paragraph):

```markdown
**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Path | string | yes | — | Path to the file |
| ResolveVariables | bool | no | false | Resolve `%var%` references inside the file content before returning |

#### Resolving variables in file content

Setting `ResolveVariables` (natural form: `load vars`) treats the file as a small template — any `%name%` token in the content is replaced with the variable's current value before the result is returned:

```plang
- set %name% = 'World'
- read 'greeting.txt', load vars, write to %greeting%
/ greeting.txt = "Hello, %name%!" → %greeting% becomes "Hello, World!"
```

**Security:** infrastructure variables (the `%!app%`, `%!fileSystem%`, `%!callStack%`, `%!trace%` family — anything starting with `!`) are deliberately **not** resolved when reading file content, because the file contents may be untrusted.
```

The same shape applies to `variable.set`'s AsDefault: parameter row + sub-section with the natural form. For builder.md the shape is different (replacing a stale section + adding new action entries) but the principle is the same: every parameter that exists in code now appears in the user-facing doc, and every doc claim about gating/guards reflects the real code.

## Verdict

**pass** — branch is ready to merge.

## What's NOT in this version

- No PLang `.goal` examples (tester's job per character file).
- No CHANGELOG entry — repo has no CHANGELOG.md; creating one for a single branch is over-scope.
- No `.bot/` files were edited under any branch other than `runtime2-builder-bootstrap`.
- No code changes outside the merge.cs XML summary (matches the docs role: write docs, route code/test concerns to other bots).
