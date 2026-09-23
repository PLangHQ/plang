# Prototype: values read the running context (throwaway, not on the branch)

Done on the step-1 tree, in a separate worktree. Nothing from it is committed as code. Reference files next to this one:

- `proto-running-context.patch`: the prototype's production diff. It includes the step-1 changes in the same files.
- `proto-running-context-sites.txt`: 811 distinct call sites that asked for a context outside any run. Each line is the value type followed by up to 6 app frames.
- `proto-running-context-newfails.txt`: the 73 tests that newly fail against the step-1 tree, with their messages.

## What the prototype did

- A static `AsyncLocal` slot `Running.Current`. `action.Run(context)` and `goal.Run(context)` set it at entry. The app constructor sets System's.
- `source`, the path family (url/file/directory delegate to path), `list` and `dict` answer `Context` from the slot, not their field.
- **Fallback for mapping only:** with an empty slot, a value falls back to its birth context and records the site. It also records `CROSS-APP` when a value born in one app is read under another app's run. Without the fallback, TUnit (below) made every test fail and hid the map.

## 1. The reds: all green (6/6)

| Test | Result |
|---|---|
| plain `%var%` slot, bound under System then User, each read in its run | green |
| typed slot | green |
| literal path, born under System; User holds the Read grant; checked in a User run | green (it checked as User) |
| literal list with a `%x%` element | green |
| literal dict with a `%x%` element | green |
| Binding_NeverWritesTheSharedRow | green |

Two of my own test premises were wrong and are corrected in the prototype (the step-1 tree copy still needs the same fix):

- The typed-slot test used `list.add ListName`. That is `data<variable>`, which NAMES `%n%` and doesn't resolve it. It now uses `list.split Value` (`data<text>`).
- The list/dict tests built their literal with `new Data("", "%x%")`, which is plain text `"%x%"`, not a variable reference. They now write the action through the goal's own `.pr` writer and read it back (`RealGoalLoad.ViaChannel`), so the elements are born as a real `.pr` gives them.

## 2. Six suites, by name, prototype vs the step-1 tree

| Suite | new | fixed |
|---|---:|---:|
| Modules | 0 | 3 |
| Types | 23 | 2 |
| Wire | 2 | 0 |
| Data | 1 | 0 |
| Generator | 9 | 0 |
| Runtime | 38 | 5 |

The failures fall into four clusters.

### A. Construction sets the slot for its CALLER (the dominant cause)

The app constructor's `Running.Current = System.Context` runs synchronously. A synchronous AsyncLocal write isn't scoped: it stays in the caller's flow after the constructor returns. So every flow that creates an app and then works outside a run is now "running as System".

- 16 failures say it outright: `Permission denied: System on …`. Examples:
  - HttpPath contract tests (Types, ~20 in total with the http ones: Get/Post/Redirect/Stat/Idn…);
  - Authorize scenarios 1–6;
  - Move/Copy bundled asks;
  - Providers_Restore (`app.Restore` loads a provider assembly: "Permission denied: System").
- Variable lookups return `""`: FullVarMatch_*, ReResolveAcrossCalls_*, SubGoalCall_EachGoalSeesOwnResolvedView. The variables sit on User, but lookup runs as System.
- **Minting a second app hijacks the first.** `TestApp.SharedContext` creates a shared app lazily, which re-points the slot at ITS System. The test runner's child app per test does the same to the runner's flow.
  - Run_* tests fail with `File not found: /tmp/shared-504287/.build/a.test.pr`: the test goal's path computed its absolute under the SHARED app's root.

What it needs: boot must set the slot for the boot's own flow only, not its caller's. For example, the app's own entry (`Start`/`Run`) sets it inside its async flow, never the constructor. A nested app (test runner child, Restore target) must not touch the creator's slot.

### B. TUnit: `[Before(Test)]` doesn't flow AsyncLocal into the test body

`TestApp.Create` inside a Setup hook set the slot, and the test body saw it empty. TUnit only flows hook AsyncLocals when the hook calls `TestContext.AddAsyncLocalValues()`. So "the test app sets it for C# tests" needs one of:

- every hook opts in;
- a TUnit-level `[BeforeEvery(Test)]` hook;
- tests that do their work through a run.

### C. Work done outside any run: the real production map (811 sites)

These are the places that ask a value for its context with no run around them. Grouped by the first app frame, with counts of distinct stacks:

- **Program structure using a path value** — 322 at `goal.PrPath` (goal/this.cs:122 → path/file/this.Derivation.cs:20 `Parent`), plus `goal.Output`, `goal.list.Load/TryLoadPr/Get`, `app.Load`, `module.Folder/Description`, `action.Notes`:
  - The program graph is context-free by law, but its `Path` is a path VALUE whose Parent/Relative/root derivation reads Context.
  - Under the prototype, these would run as whichever actor happens to be running. The builder's catalog reads of `os/system/modules/*.md` would check permissions as the User running the build, and os/ is outside the app root.
  - **This is the biggest thing the reasoning didn't foresee:** a program node's path needs an actor, and the answer isn't "the running one".
- **Handlers called directly, not through the run door** — `file.Read.Run`, `file.List.Run`, `file.Delete.Run`, `code.load.Run`, `file.Read.Build`:
  - Tests `new X(ctx).Run()` and skip `action.Run`.
  - `Build()` runs in the builder's validate pass, which does go through a run (the builder's), so the build hooks run as the builder's actor, not the target goal's.
- **Lazy shared singletons** — `setting.Sqlite.CreateAsync` (`App.SettingsStore` is a `Lazy<Task>`):
  - The store's path authorizes as WHICHEVER run first touches it.
  - A User run touching settings first would authorize the system.sqlite path as User.
- **Formatting and rendering outside a run** — `ui.code.Fluid.Render`, `test.junit ToString`, `file.Type/image.Mime` (`Format.TypeFromExtension` via Context.App), `item.Output`:
  - These mostly need Context only for `App` (registry/format), not for the actor.
  - That's a second hidden split: values use Context for TWO things, the actor (permissions, variables) and the App (registry, root, formats).

### D. Paths change meaning: absolute resolution follows the RUNNING app's root

A path's `Relative`/`Absolute`/`Parent` derive from `Context.App` (root).

- Read under another app's run, the same path value points somewhere else. Goal_JsonRoundTrip_*, DerivedPath_InheritsContext_FromSource and the Run_* "File not found under /tmp/shared-…" cases show it.
- Recorded as `CROSS-APP` 145 times; most are tests minting values through `SharedContext` and reading them under another app's run.
- In production, the test runner runs a parent-loaded goal inside a child app, so its path literals would resolve against the child's root.

## 3. What the reasoning didn't foresee

1. **A synchronous AsyncLocal set leaks to the caller.** Boot-in-constructor and nested-app creation re-point the creator's flow (cluster A). The ambient must be set only inside async run doors, never in a constructor or synchronous factory.
2. **TUnit hooks don't flow AsyncLocal** (cluster B).
3. **Context means two things.** The ACTOR (permission, variables, later culture) belongs to the running context. The APP (root, registry, formats) is a birth fact of where the value lives: a path's absolute path, a goal's .pr path. Reading the App from the running context breaks path meaning across apps (cluster D) and forces program nodes into an actor (cluster C). A split would fit the law: values read the running ACTOR, and keep their App as a birth fact, reached the way program nodes reach it.
4. **Program nodes aren't fully context-free today.** `goal.Path`/`PrPath` are path values, and every .pr load/save and every catalog read asks one for a context. Whose permission applies to the builder reading `os/system/modules` needs a ruling; today it's whoever stamped last.
5. **Lazy app-wide singletons** (`App.SettingsStore`, and likely others created on first use) capture the actor of their first toucher.
6. **Build hooks** run as the builder's actor. For stamping `%goal%` that's fine; for any hook that reads a file it's a question.

## Not done (time-box)

- No attempt to fix clusters A–D in the prototype.
- The 3 Modules and 5 Runtime tests the prototype FIXED were not investigated.
