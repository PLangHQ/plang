# v7 plan — Stage 9: born-typed values, rebuilt on the model doc

**Contract:** `coder/data-value-model.md` (settled, nothing open) +
`architect/stage-9-born-typed.md` + `architect/stage-9-demolition.md`
(verdicts are the contract). Approved by Ingi 2026-06-10, with the lazy-param
ruling below.

## Ingi's lazy-param ruling (this session)

The ref/template text type IS the lazy mechanism. A .pr param's authored form
lifts to a typed instance at the entry seam: literal → text/number, ref or
templated string → stamped template text, structure → dict with stamped
entries. After the lift, "lazy" is nothing extra — resolution happens inside
`Value()`, the stamped text's own behavior. The Data-level factory
(`SetValue(Func<…>)` / `_valueFactory`) is a second lazy mechanism and dies in
slice 1.

- Params follow the rebind rule: stamped param resolves fresh at every
  `Value()`, never rebinds; cache iff `template == null`. If the per-execution
  backing-field reset turns out still load-bearing somewhere, FLAG it, don't
  keep both mechanisms.
- "Mechanical retarget" boundary: swapping the generated carrier's internals
  while the handler-facing shape stays identical is mine. If the emitted
  getter's contract changes (what it returns, or when resolution fires — the
  2.1c await-then-guard pattern must survive: resolve at `await Param.Value()`,
  error surfaces there, guard after the await), show Ingi first.

## Slices (each ends green on both suites, committed)

0. **Pre-step** — baseline both suites → `baseline-tests.md`; audit the
   committed tail-fix residue from the disposable patching path, mark what
   survives the model → `survives.md`.
1. **Core collapse** — `item` gains async `Value()` (may answer as a different
   type), priors chain slot + `Is`/`Facet` walk, async `Write(IWriter)`;
   `file`/`url` get private `_raw` (load-once, lock, nulled on parse, gate
   exemption by name). Data collapses to name + typed instance + properties +
   signature; kills together: `_value`, `_type`, `_raw`, `_valueFactory`,
   `SetValue(factory)`, `Materialize`/`ForceMaterialize`/`_materializeCount`,
   `NarrowReference`, `As<T>`/`AsT_Impl`/`WrapAs`/`_resolvingValues`. Entry
   lift per the table (bare data.@this THROWS). `Value<T>()` lands (T plang
   only; facet → hand over, else Convert hook, else Data.Error; never
   rebinds). Compile-error-driven consumer sweep to green.
2. **Consumer tail** — kill `GetValue<T>`/`GetValue(Type)`, Data-level
   `Clr<T>`, `AsEnumerable`/`IsPlangIterable`/`IsPlangAssignable`, `ToBoolean`
   CLR arms, `UnwrapJsonElement`, `SnapshotClone`, implicit-to-CLR operators,
   audit `number : IConvertible`. Six `Stage2_ValueDoorTests` stubs green
   across slices 1–2.
3. **Templates** — builder stamps `template`; live single-pass render at every
   use, never cached; `Write` resolves inline; `GetValue()` pre-crawl dies;
   `TryFullVarMatch` retired when stamps land.
4. **Collections** — remove `CopyStructure` copy-on-add; reference-semantics
   test (`[1,2,3]`); property-bag copy at set.
5. **Follow-ons** — `text.Value` private; `item.ToRaw()` removed.

**Transitional, untouched:** `SetValueDirect`, wire-envelope recognition
members, `EnsureSigned` (verify zero callers only) — schema branch deletes
them.
