# app-lowercase — Coder Summary

## Version
Final state across v1–v3, plus seven merge commits driven interactively with Ingi.

## What this is
Mechanical rename: lowercase the PLang vocabulary C# namespaces so the
runtime reads in the same casing as plang itself. The win:
- `System.Type` / `System.IO.Path` shadowing dies (kills the
  `using System.Type? = ...` workaround in tests).
- The codebase makes vocabulary vs. infrastructure visible by case alone —
  `app.goal`, `app.variable`, `app.modules.cache` are plang nouns;
  `Attributes`, `Diagnostics`, `Services` are C# plumbing.
- Seven case-only folder pairs (e.g. `app/Cache/` vs `app/modules/cache/`)
  resolved by merging into a single namespace each, fixing OBP smell #4
  ("same logical thing stored twice across types").

## What was done

Twelve commits on `app-lowercase` from `runtime2`:

1. `Phase 1: rename root namespace App → app` — 600+ files, root namespace only, sub-vocabulary still PascalCase.
2. `Phase 2: rename app.Data → app.data, class Type → type` — kills `System.Type` clash.
3. `Phase 3: rename class Path → path (FileSystem)` — kills `System.IO.Path` clash.
4. `Cache merge: collapse app.Cache + app.modules.cache into one` — first OBP merge.
5. `Phase 4 namespace rename — fix type-position references in ~65 source files` — Actor, CallStack, Channels, Config, Errors, Events, FileSystem, Formats, Goals, KeepAlive, Snapshot, Tester, Types, Variables.
6. `Phase 4 cleanup: finish tests + generator after vocabulary rename` — test using-imports, generator template strings, StoreOnlyModifier production bug.
7. `Builder merge`, 8. `Callback merge`, 9. `Settings merge`, 10. `Modules merge`, 11. `Code merge`, 12. `Debug merge` — all collision pairs consolidated.

Final state: zero case-only folder collisions at top of `app/`. Windows/macOS clone safe.

## Key decisions / discoveries

- **Source generator out-of-sync risk is real.** Discovery scans for `"app.modules"`, `"app.data"`, `"app.variables"` as string literals in attribute namespace checks. Every phase had to update the matching string in the generator alongside the folder move, or the next build would silently emit nothing for renamed types.
- **Property-access vs namespace-navigation ambiguity** is regex-impossible in this codebase. Many classes carry an `App` property of type `app.@this`. Inside their methods, `App.X` means `this.App.X`, not a namespace ref. Sed-based passes had to be paired with sonnet-agent cleanup (per file: read context, distinguish, edit) to avoid breaking instance chains.
- **C# keyword collision.** `app/FileSystem/Default/` cannot become `app/filesystem/default/` — `default` is a keyword. Reverted to `Default` (PascalCase) for that one subfolder as a documented carve-out.
- **Class-name collisions surfaced two plang API breaks:**
  - The action module `app/modules/app/` had to be renamed to `environment/` because once the root lowercased to `app`, a sibling `app.modules.app` namespace would shadow the root. PLang action `app.run` → `environment.run`.
  - The class `builder.app` (action handler) had to be renamed to `builder.load` for the same reason. PLang action `builder.app` → `builder.load`.
  Both are real breaking changes to the plang surface. Accepted to unblock the rename.

## Code example — the OBP merge pattern

Same shape applied seven times:

```csharp
// Before:
namespace app.Cache;
public sealed class @this { /* ICache registry */ }
public sealed class Memory : ICache { /* default impl */ }

// app/modules/cache/wrap.cs — separate folder, same domain:
namespace app.modules.cache;
[Action("wrap")]
public partial class wrap : IContext { /* cache.wrap action */ }
```

```csharp
// After:
namespace app.modules.cache;            // one home
public sealed class @this { /* registry  */ }
public sealed class Memory : ICache { /* impl */ }
[Action("wrap")]
public partial class wrap : IContext { /* action */ }
```

Engine property `engine.Cache` stays PascalCase (property name); only its declared type changes from `app.Cache.@this` to `app.modules.cache.@this`. All consumer code reading `engine.Cache.X` continues to work — they go through property access, not the type.

## Test gates

Both suites stayed green at every commit boundary:
- C# (`dotnet run --project PLang.Tests`): **2752 / 2752 pass**
- PLang (`cd Tests && plang --test`): **203 pass / 6 known-fail** (`_fixtures_sensitive/sensitivefail.fixture.goal` and `_fixtures_fail/failsvar.fixture.goal` — pre-existing intentional negative fixtures, identical to `runtime2` baseline)

## What's still pending (post-merge follow-ups)

1. **The two breaking plang API renames** (`app.run` → `environment.run`, `builder.app` → `builder.load`) are temporary names chosen under pressure. They want a deliberate naming decision.
2. **Docs drift** — `Documentation/v0.2/app-tree.md`, `Documentation/Runtime2/plang_object_based_pattern.md`, and the `/PLang/CLAUDE.md` files all reference old namespace paths (`App.Goals`, `App.Data`, etc.). Docs bot pass needed.
3. **Reviewer passes** — given the scale of the rename (~600 files touched, 12 commits), running codeanalyzer/security/auditor before merging to `runtime2` would lock in quality.
