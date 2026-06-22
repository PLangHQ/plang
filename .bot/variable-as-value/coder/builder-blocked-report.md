# variable-as-value — builder is blocked (validation report)

**Branch:** `variable-as-value` (validated at `208ae6c48`, after clean C# rebuild)
**Status:** simple goals build+run; **the builder cannot build**. Two regressions, both in the variable-as-value compile/resolve layer (confirmed not stale binaries — clean rebuild + fresh `.pr`).

## What works
- C# solution rebuilds clean (0 errors).
- `Tests/Scratch/Hello.goal` (`write out "hello world"`) builds and runs → prints `hello world`.

## Regression 1 — `variable.set`/`variable.get` compile with `Name = null` (ROOT BLOCKER)

The variable name is dropped during compile. Any goal using `set %var% = ...` is broken.

**Repro** (from `Tests/`):
```
Scratch/Repro.goal:
  Repro
  - set %myrec% = {"id":"abc123"}
  - write out %myrec.id%

plang '--build={"files":["Scratch/Repro.goal"]}'
```

**Compiled `.pr` (wrong):**
```
variable.set  Name = null          ← should be "myrec" [variable]
variable.set  Value = {id: abc123} [dict]
```

**Runtime:** born-typed guard fires → `NullReferenceException` at `PLang/app/module/variable/set.cs:97`:
```
%Name% holds a null — a variable names a thing; it is born typed
(declare 'type:variable'), never created from a value.
```

**Ground truth for the correct shape** — the builder's own `.pr`, built by the *old* binary, is right:
```
os/system/builder/BuildGoal/.build/start.pr →
  variable.set  Name = %buildStart% [variable]
  variable.set  Value = %Now.Ticks%  [object]
```

So the new compile path no longer emits the LHS `%var%` as a born-typed `Name`
(`Data<app.variable.@this>`). Likely in the param extraction / normalization that
should map `set %x% = v` → `Name="x" [variable]`.

### Extra hazard — name collision contaminates the `.pr`
When the variable name collides with a builder-internal (`%trace%`, `%goal%`,
`%data%`), build-time pre-resolution does worse than null — it **bakes the
builder's live runtime object into the goal's `.pr`**. Repro with `set %trace% = {...}`:
the `Name` parameter came out as the builder's own 55 KB `trace` object (id
`639176889289416783_…`, including the whole goal being built). Build-time
variable pre-resolution must not resolve a goal's own LHS target name against the
builder's runtime scope. See `.bot/variable-as-value/coder/build-time-variable-parsing.md`.

## Regression 2 — builder trace-save crashes at runtime (`%!infra%` in a path string)

`plang build` in `os/system` dies at `builder/BuildGoal/Start.goal:26`:
```
- save %trace% to file '/.build/traces/%!trace.id%/%goal.Name%.json'
```
`file.save`'s `Path` resolves to **null** — interpolating the `%!trace.id%` infra
variable inside a path string yields null under the new semantics. NRE at
`PLang/app/module/file/save.cs:15` (`(await Path.Value())!.Save(...)`), the typed
ask declines with `%Path% holds a path — 'path' cannot be created from it.`

This blocks the self-host: the builder's `.pr` files predate this branch
(`6e210f4c5`) and can't be rebuilt because rebuilding runs this same broken step.
Likely clears once Regression 1 is fixed and the builder can recompile its own
`.pr`, but the `%!x.member%`-in-path resolution should be verified independently.

## Suggested order
1. Fix Regression 1 (set/get `Name` compile → born-typed `[variable]`). Verify with the repro above and that `set %trace%` no longer pulls builder runtime scope.
2. Rebuild the builder (`cd os/system && plang build`) and confirm Regression 2 is gone; if not, fix `%!infra.member%` interpolation inside path-typed strings.
3. Re-run `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

## Key files
- `PLang/app/module/variable/set.cs:97` (born-typed guard / NRE site)
- `PLang/app/module/file/save.cs:15` (path null NRE site)
- `PLang/app/type/item/ICreate.cs:48` (the decline message)
- `os/system/builder/BuildGoal/Start.goal:6,25` (`%trace%`, `%!trace.id%`)
