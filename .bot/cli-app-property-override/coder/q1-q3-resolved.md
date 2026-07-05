# Q1–Q3 resolved — Ingi's calls (fold into the plan)

**Branch:** `cli-app-property-override`. **From:** coder, after tracing the three seams the
signed-off plan hand-waved and putting them to Ingi. Decisions below are his; they change the
plan's leaf-conversion contract and add a naming carve-out + a second rename.

## Q1 — non-plang-typed leaves: `CallStack.Flags` was the counterexample → reshaped away

The plan says leaves convert "via the plang catalog." `CallStack.Flags` broke that: it's an
immutable `record struct` with 6 positional fields and its own `Flags.Parse` — no `dict→Flags`
in the catalog, and the walk (which sets public-set leaves) can't populate a struct copy.

**Decision (b): reshape it into a settable, descended config object — and rename it.**
`Flags` is an invented term; in plang everything configurable is a **`Setting`** (singular).
These knobs (should we time it, store diffs, …) are the callstack's settings.

```
// app/callstack/setting/this.cs   →   app.callstack.setting.@this
public record class @this {
    public bool Timing    { get; set; }
    public bool Diff      { get; set; }
    public bool DeepDiff  { get; set; }
    public bool Tags      { get; set; }
    public bool History   { get; set; }
    public int  MaxFrames { get; set; } = 1000;
}
```

- `record class`, not struct → the walk sets each leaf, then assigns the object back; `with`
  (needed by error-recovery, below) still works because it's a record.
- `CallStack.Flags` → `CallStack.Setting`, non-null, `= new()`.
- **`Flags.Parse` and `Flags.Shorthand` die** — no bespoke parser (§5), no `true`→Shorthand
  expansion (§4). The walk sets fields explicitly.

Usage:
```
plang '--callstack={"setting":{"timing":true,"diff":true}}'
  → CallStack.Setting.Timing = true, CallStack.Setting.Diff = true
```

**Corrected perf note (I was wrong earlier):** I'd claimed struct→class was a hot-path
regression. Traced it — it isn't. `call.@this` reads the flag fields once at construction
(`call/this.cs:120-139`) and keeps **no** Flags field; it reaches `_stack.Flags` live at
`:303`. There's already one live instance on the stack, no per-Call snapshot. So class is
**neutral-to-faster** (8-byte ref replaces a 24-byte struct copy per push) and shared-reference
breaks nothing.

**Error-recovery contract to preserve** — `error/list/this.cs:80` temporarily flips Diff on:
```
stack.Setting = stack.Setting with { Diff = true };   // save prior ref, restore on Restorer.Dispose (:111)
```
`with` on a record class allocates a fresh instance and assigns it; the saved reference stays
intact → restore stays correct. Verified.

Rename touch-ups: `call/this.cs:107` param `Flags flags` → `setting.@this setting`;
`Restorer._priorFlags` → `_priorSetting`.

**Generalized answer to Q1:** the walk descends config objects and sets their public leaves —
it does **not** rely on a catalog `dict→T` converter for composite leaves. A composite leaf is
a settable `record class`, walked field-by-field. No type keeps a private `Parse`.

## Q2 — `--app` maps to the app root itself: the sole parser exception

`Create` etc. are public-set on the root (`this.cs:208`), so by flag=property-name they'd be
`--create`. Instead **`--app` is the one sanctioned exception in the parser** — it names the
app object itself, and its keys are root-level public-set leaves:
```
plang '--app={"create":true}'   → app.Create = true
```
This is the *only* remap in the whole design; every other flag is an exact tree-path. Name it
explicitly as the carve-out so it doesn't read as a special case that slipped through.

## Q3 — `--test` is canonical; `Tester` → `Test`; `--tester` gone

The `--test`/`--tester` split does not survive. **`--test` is the flag, and the code matches
the flag: rename `app.Tester` → `app.Test` (`app/tester/` → `app/test/`).**
```
app.Tester (app.tester.@this, top-level)  →  app.Test (app.test.@this)
--tester  →  gone
--test    →  the flag  (== property name, tree-mirror holds)
```
Distinct from `app.module.test` (the runner action module) — mode object vs runner, same split
as `app.Build` vs the build module.

Docs/CLAUDE.md already invoke `plang --test`, so this *keeps* the documented command while
dropping the alias — the rename lands us on the right side for free.

## Net changes to the plan

- Path step 1: rename is now **two** — `Builder → Build` **and** `Tester → Test`. Drop both
  `--builder` and `--tester` aliases.
- Path step 4 / §3: `Tester.*` leaves become `Test.*`; add the callstack `Setting` reshape.
- §1: add `--app` as the named sole exception (app-root alias).
- §6.B / demolition: `Flags.Parse`, `Flags.Shorthand` join the kill list; `Debug`'s
  `callstack` cross-node write already slated to die (§6.B) — its replacement is
  `--callstack={"setting":...}`.
- Future todo (not this branch): `app.module.settings` → singular `setting`.
