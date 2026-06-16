# Host-carrier (clr → item) handoff #2 (2026-06-16)

**Branch:** `compare-redesign`. **HEAD:** `a8e6d24af`.
**Authoritative design:** `host-carrier-spec.md` (read first).
**Prior handoff:** `host-carrier-handoff.md` (slices 1–3 of the *original* plan).
**Open side-issue:** `full-suite-exit-segfault.md` (pre-existing, cosmetic, needs a
Windows stack — not part of the clr work).

## The decision (don't relitigate)
`clr` is NOT removed — it's a **closed** foreign-object carrier. A C# object PLang
can't narrow IS an **`item`** (the apex): `type=item`, `kind` = the declared PLang
name. Engine handles (`%!app%`, `%!callStack%`, …) ride it. Reflect-write deferred.
Nested Data abolished.

## Done & committed THIS session (in order)
- `79ea81282` **kind via `[PlangType]` + Mint.** `clr.Mint()` now stamps
  `type=item`, `kind = App.Type.ResolveName(clrType) ?? FullName` (was putting the
  class name in the type slot → every handle reported `kind=@this`). The name can't
  be derived (collection handles all namespace-tail to `list`/`tester`/`trail`), so
  `[PlangType]` is declared on the 5 divergent concept handles:
  `variable` (`app.variable.list.@this`), `channel` (`app.channel.list.@this`),
  `serializers` (`app.channel.serializer.list.@this`), `trace` (`app.error.trail.@this`),
  `test` (`app.tester.@this`).
  - **Registry two-pass** (`app/type/catalog/Registry.cs`): an *aliased* `[PlangType]`
    name (one that diverges from the type's own namespace/class inference) is deferred
    so it never steals the `name→type` resolve slot from a type that naturally owns it.
    This fixed a REAL collision: `[PlangType("test")]` on `app.tester.@this` was
    hijacking `ResolveType("test")` from `app.tester.test.@this`. The kind direction
    (`type→name`) is collision-free; only the reverse needed the guard.
  - **Context threading:** `Lift`'s carrier fallback and the `computed` cell (now
    `module.IContext`) carry Context so a lazily-materialised `%!app%` resolves its
    registry name on mint. Test: `HostCarrierKindTests` (Runtime).
- `5113a3ac0` **fix: SnapshotAt stack overflow** — a regression the kind commit
  introduced. `ShallowCloneStore` cloned EVERY var incl. `!`-prefixed/ DynamicData
  context cells; once `computed` gained a Context field, `Force.DeepCloner` deep-walked
  the whole App graph → SIGSEGV. Fixed to skip context/DynamicData vars, mirroring
  `Variables.Clone()`.
- `a8e6d24af` **carriers clone by reference** — generalises the above. `item.@this`
  now owns `Clone()` (virtual, default = deep copy). `Data.Clone()` calls it. The
  live/immutable carriers — `clr`, `computed`, `source` — override `Clone() => this`
  (they hold a Context into the App graph; deep-cloning overflows, and reference-
  sharing is the correct value-model semantics: a carried host IS shared). Kills the
  deep-clone hazard for ANY `Data` holding `%!app%`, not just the snapshot path.

## Verified
- `HostCarrierKindTests` (6 tests, Runtime): `%!app%`→`item`/`app`, `%!callStack%`→
  `item`/`callstack`, `%!variables%`→`item`/`variable` (proves `[PlangType]` over
  namespace-tail), leaf peel-off (`!app.Name`→`text`), clone-by-reference no-overflow.
- Zero regressions vs `test-baseline.txt`: Data 17 / Runtime 55 / Types 31 / Wire 15
  / Modules 49 / Generator 0 — identical failing-name sets before/after (diffed by
  name, NOT by stashing).

## Remaining (the next slices — from the spec, NOT yet started)
1. **§C courier-label cruft.** The spec says delete `_declared`/`Labeled`/
   `_declaredStrict` from `clr` + the `type/this.cs` `Judge` sites. **Investigated,
   NOT cut — needs Ingi's design call.** Mutation test showed: the two
   `carrier.Labeled(...)` arms (re-label an existing clr) are **DEAD** (safe to
   delete). The three `new clr(value, Name, Kind, Strict)` arms are **LIVE** — they
   give a value a *declared* `{type,kind,strict}` it can't carry itself (binary as a
   category; a structured value as a divergent type). So `clr`'s label role is still
   load-bearing. The clean home is `item.source` (already the declared-judgement
   carrier over a RAW form) — binary moves cleanly; the **structured** case is the
   open fork. **Decision pending** (3 options framed for Ingi: broaden `item.source`
   to hold an item / drop the label for structured / dedicated `item.declared`
   wrapper). `1 as int` does NOT hit this path — it only fires when the value's own
   mint has no kind yet a kind/strict is declared, on a non-door value.
2. **3 reflection consumer sites → use the plang type** (`type/this.cs:276`,
   `condition/code/Default.cs:46`, `module/debug/this.cs`). Verify a clr flows there
   first.
3. **Full Normalize dissolution** — host-reflection off `Normalize` onto `clr.Write`
   (OBP #9). Needs the cycle-guard re-homed.
4. **Deferred:** reflect-write + its actor-permission gate.

## Workflow (LEARNED THE HARD WAY — see memory `build_speed_workflow`)
- **Don't stash to get a baseline.** Baseline failing-test names are in
  `test-baseline.txt`; after a change, run only the affected suite and diff names.
- **Don't `rm Fixtures/pr/.db`.** That pollution is FIXED (TestApp.Create); verified
  no `.db` is created on a full Data run.
- **Build only the slice you're testing:** `dotnet build PLang.Tests/<Slice> -c Debug
  -p:RunAnalyzers=false` (~3–20s). `./dev.sh build` rebuilds ALL six projects +
  console — a **base-class edit** (`item/this.cs`, `data/this.cs`) cascades to a ~90s
  full recompile, so batch those.
- Pre-existing whole-suite exit SIGSEGV (Data/Wire/Runtime): cosmetic, results print
  first — see `full-suite-exit-segfault.md`. Read counts from per-test lines.

## What to tell the next session (paste this)
> Continue the host-carrier (clr→item) work on compare-redesign. Read
> `.bot/compare-redesign/coder/host-carrier-handoff-2.md` and `host-carrier-spec.md`
> first. The kind/item fix + two follow-up fixes (SnapshotAt overflow, carriers
> clone-by-reference) are committed through `a8e6d24af`. The next slice (§C
> courier-label cruft) is BLOCKED on Ingi's design call — present the 3 options for
> where a structured value's declared `{type,kind,strict}` rides once `clr` is
> label-free. Use the saved baseline + slice-only builds; never stash, never rm pr/.db.
