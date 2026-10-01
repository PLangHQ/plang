# Building the Builder (the bootstrap)

How to rebuild PLang's own builder — the part written in PLang (`os/system/builder/*.goal`)
that compiles `.goal` files into `.pr`. This is the bootstrap case: PLang building PLang.
Get the invocation wrong and the builder either won't run or silently produces broken `.pr`
files that look like LLM hallucinations.

For what the builder *does*, read [`builder.code.md`](builder.code.md) first. This doc is
only about rebuilding it safely.

## The cardinal rule

**Never hand-edit a builder `.pr` file to mask a bad rebuild.**

If a self-rebuild produces a wrong `.pr` — a mis-bound action, a missing parameter,
anything — **make it loud**. The fix belongs in:

- the LLM prompt or its data (`llm/Properties.llm`, `llm/SourceFix.llm`, `llm/decider.json`),
- a template (`llm/templates/*.template`),
- the action teaching (`os/system/modules/<module>/*.md` — see [`../modules/catalog.md`](../modules/catalog.md)),
- or, when the builder needs data it can't reach, the C# that feeds it (`PLang/app/module/build/code/Default.cs`).

**Never** in the `.pr`. The `.pr` is downstream of the builder; patching it there masks the
bug, lets the same mistake reappear on the next rebuild, and corrupts the source of truth.
The one exception is a deliberate one-time bootstrap patch when `.goal` semantics change
faster than the builder can re-derive them — and that's a pre-first-rebuild hand-patch, not
a post-build cleanup.

## The recipe

```bash
cd os/
plang build '--build={"cache":false,"files":[
  "system/builder/Build.goal",
  "system/builder/BuildGoal.goal",
  "system/builder/BuildGoal/Start.goal",
  "system/builder/BuildGoal/Decide.goal",
  "system/builder/BuildGoal/Properties.goal",
  "system/builder/BuilderChannel.goal",
  "system/builder/EmitBuildEvent.goal"
]}'
```

Three things matter, all non-obvious.

### 1. `cwd = os/`

Not `os/system/`, not `os/system/builder/`, not the repo root. Only `os/`.

The builder's `.pr` files stamp paths like `/system/builder/.build/buildgoal.pr`, which
resolve relative to cwd. Only `cd os/` lands them on the actual files; run from elsewhere
and you get `File not found: /.build/buildgoal.pr` or short-form paths that break the next
run. `os/.build/app.pr` is the app-root marker (`name: "os"`); all builder pathing assumes
that app root.

### 2. File order — outer to inner

Pass the explicit ordered `files` list, entry first, leaves last — the call chain:

1. `Build.goal` — entry (channel, `build.load`, `build.goals`, foreach → `BuildGoal`)
2. `BuildGoal.goal` — the shim → `BuildGoal/Start`
3. `BuildGoal/Start.goal` — per-goal pipeline; owns `Compile`, `BuildSubGoal`, and the recovery goals (`FixSteps`, `ConfirmNumbers`, `SourceError`, `HandleBuildFailure`)
4. `BuildGoal/Decide.goal` — the two decider exchanges
5. `BuildGoal/Properties.goal` — the writer `llm.query`
6. `BuilderChannel.goal` — the output channel sink
7. `EmitBuildEvent.goal` — build-time output helper

During the rebuild the running app executes the *previous* in-memory pipeline. Building
entry-first keeps a half-updated pipeline from being picked up mid-run and producing
inconsistent output. `build.goals` honours the `files` order (it builds the matched files
in the order they appear in the filter).

### 3. Path-qualify every filter — bare filenames fan out

Each `files` entry **must** start with `system/builder/`. `build.goals` matches a filter
through `path.Matches`: a filter with no path separator matches by **filename only**, and
`os/` has many `Build.goal` / `Start.goal` files (every app has a `Start.goal`; several
modules have a `Build.goal`). A bare `"Build.goal"` silently pulls in dozens of unrelated
goals. The tell is a build count far larger than the builder's handful of files, or a
phantom failure on some incidental stub goal that has nothing to do with the builder.

Qualify with the full `system/builder/` prefix, not a shorter suffix — matching is
case-insensitive, so `"builder/Build.goal"` could still catch `system/modules/db/Builder/Build.goal`
(`Builder` vs `builder`). `"system/builder/Build.goal"` is unambiguous.

## Pre-flight check

Before a self-rebuild, audit the existing builder `.pr` path stamps. A `.pr` from a prior
run with the wrong cwd carries wrong `path`/`prPath` and will mislead the next build:

- top-level `path` should be `/system/builder/<Goal>.goal`, `prPath`
  `/system/builder/.build/<goal>.pr`; sub-goals (in `child[]`) share the parent's `path`/`prPath`.
- Anything short (`/Build.goal`, `/.build/build.pr`) means a prior build ran from the wrong
  cwd — fix the stamps (or rebuild from the correct cwd) before trusting the output.

A quick read of the stamps (do not edit `.pr` by hand to "fix" mapping — stamps are the one
mechanical exception, and only when they're provably from a wrong-cwd run):

```bash
cd os && for f in system/builder/.build/*.pr; do echo "$f: $(jq -c '{path, prPath}' "$f")"; done
```

## Verifying the result

After a successful self-rebuild, sanity-check by re-building an already-built goal with it
and confirming nothing moved. Build from the project root with a relative filter — **don't
`cd` into a Tests subfolder** (that re-stamps `prPath` from the wrong cwd; see the repo
`CLAUDE.md` "Running plang Tests"):

```bash
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang build '--build={"files":"<Area>/Start.goal"}'
```

With the cache on and the source unchanged, every step should report cached (`[≡]`) and the
`.pr` should come out byte-identical (`git diff` clean). A fresh failure, or a changed `.pr`,
means the just-rebuilt builder has a real regression — revert before pushing.

> **Stale-binary trap.** `plang build`/`plang --test` use the pre-built
> `PlangConsole/bin/Debug/net10.0/plang`, not recompiled per session. If a C# change to the
> build module is involved, rebuild first (`dotnet build PlangConsole`) or you're testing an
> old binary. See the repo `CLAUDE.md` "Stale-binary trap".

## When a self-rebuild maps something wrong

Don't strip it from the `.pr`. Find which stage owns the mistake and fix there — the
[`debugging.code.md`](debugging.code.md) runbook pins it to the decider, the writer, or the
rendered prompt before you touch anything. Recovery loops (`FixSteps`, `ConfirmNumbers`,
`SourceError`) absorb some classes automatically; a mistake that survives them is a prompt,
template, or teaching gap.

## Related

- [`builder.code.md`](builder.code.md) — what each builder goal does.
- [`debugging.code.md`](debugging.code.md) — triaging a wrong `.pr`.
- [`../modules/catalog.md`](../modules/catalog.md) — the action catalog / teaching.
- `PLang/app/module/build/code/Default.cs` — the `IBuilder` actions (`goals`, `load`, `pick`,
  `match`, `fold`, `goalsSave`, `unreached`, `appSave`).
- [`../../../Documentation/v0.2/build.md`](../../../Documentation/v0.2/build.md) — the `plang build` CLI.
