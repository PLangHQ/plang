# Handoff to coder — snapshot FixAndResume + how to build plang validly

## TL;DR
Your snapshot stack **builds clean and maps correctly** — the `plang build` of
`Tests/Snapshot/FixAndResume/Start.test.goal` succeeds (all `[✓]`, mappings right). What
fails is the **runtime test**: the fix-and-resume edit doesn't survive to the resumed run.
Most likely you hit a *build environment* problem (stale binary / poisoned cache / wrong
invocation), not a builder bug — recipe below.

## How to build plang VALIDLY (this is the part that bit you)
Three independent gotchas; miss any one and the build looks broken for reasons unrelated to
your code:

1. **Rebuild from clean before trusting `plang --test`.** `plang`/`plang --test` uses the
   pre-built `PlangConsole/bin/Debug/net10.0/plang` — it is NOT recompiled per run. After a
   C# change you MUST:
   ```
   rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj PLang.Generators/bin PLang.Generators/obj
   dotnet build PlangConsole
   ```
   Phantom "Action '<x>' not found" / null reads of symbols that exist in source = stale binary.

2. **The LLM cache poisons builds.** `--build cache:false` does NOT fully bypass the local
   `llmcache` on the build path — a single bad/degenerate cached response gets replayed and
   fails EVERY build until cleared (there's no `sqlite3` binary; use Python):
   ```
   python3 -c "import sqlite3;[ (lambda c:(c.execute('DELETE FROM llmcache'),c.commit()))(sqlite3.connect(d)) for d in ['os/.db/system.sqlite','os/system/.db/system.sqlite','Tests/.db/system.sqlite']]"
   ```
   Clear it before measuring, or you're testing stale cache, not your code.

3. **Building the BUILDER itself** (only if you touch `os/system/builder/**`): cwd MUST be
   `os/` (NOT `os/system/`), and every `files` entry MUST start with `system/builder/`
   (a bare/`builder/`-prefixed filter silently pulls in dozens of unrelated goals →
   phantom `BuilderPlannerFailed`). Full recipe: `Documentation/v0.2/building-the-builder.md`.

4. **C# tests:** `dotnet test` is unsupported (MTP). Use `dotnet run --project PLang.Tests`
   (recompiles in-place — immune to the stale-binary trap). **PLang tests:** `cd Tests` first,
   then `../PlangConsole/bin/Debug/net10.0/plang --test`.

## The actual snapshot bug (FixAndResume)
Test: `Tests/Snapshot/FixAndResume/Start.test.goal`. Flow: Check fails (`assert %x% is not 1`,
x=1) → `on error call FixAndResume` → save `%!error.callback%` (the throw-time snapshot) to
`crash.snapshot` → `read … as snapshot` into `%snap%` → `set %snap.variables.x% = 2` →
`resume %snap%`. Expected: Check re-runs with x=2 → passes. **Actual: assert still sees x=1**
(`Expected:1 Actual:1`). So the `x=2` edit never reaches the resumed execution.

Build is fine (verified): mappings are
`resume %snap%`→`snapshot.resume`, `read … as snapshot`→`file.read`+`variable.set(Type=snapshot)`,
`set %snap.variables.x%`→`variable.set(Name=%snap.variables.x%)`. So this is **runtime**, in
your snapshot code. Check these three links (one is broken):

1. **Does `read … as snapshot` yield a NAVIGABLE `snapshot.@this`, or a raw string?**
   `resume.cs`'s comment says "Read stays dumb; type-driven rebuild is pulled at resume." If
   `%snap%` is still the JSON string after the read, then `set %snap.variables.x% = 2` can't
   navigate it (it edits nothing / mints a flat `snap.variables.x`), and `resume` deserializes
   the UNEDITED string → x=1. The string→snapshot seam exists at
   `PLang/app/type/list/Conversion.cs:184-192` (WireReader/`FromWire`) — verify
   `snapshot.@this` actually exposes the `static object? FromWire(string, string?)` signature
   `app.type.@this.WireReader` looks for; `this.Wire.cs` has `Deserialize(string, context)`,
   which may NOT match → falls to the generic JSON deserialize that the comment says produces a
   *broken* snapshot.
2. **Does navigation-SET into `%snap.variables.x%` mutate the Variables section** of a parsed
   `snapshot.@this`? (Your commit bc600c10b claims navigate+edit works — confirm it holds for
   the Variables section specifically, set not just get.)
3. **Does `resume` use the edited `%snap%` object**, or re-deserialize from the original wire
   (losing the edit)? `resume.cs` does `Snapshot.Value` → `As<snapshot.@this>` → FromWire.

Likely fix: make `as snapshot` parse eagerly to a navigable `snapshot.@this` at set time (so
edit applies), and have `resume` use that object as-is. Your unit tests prove the pieces; the
PLang integration (edit-then-resume) is the gap.

## Also failing (verify ownership)
`plang --test`: 259/263 pass. Besides FixAndResume, 3 event tests fail
(`Channels/Events/AddOnAsk`, `Channels/Events/AddBeforeWrite`, `Modules/Event/Basic`). These
MAY be from the builder's `Plan.llm` slim changing `event.on` planning — builder will triage
those (not coder's snapshot work). Flagging so they're not attributed to the snapshot change.
