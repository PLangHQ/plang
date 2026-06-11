# Coder v7 — Stage 9 (born-typed values) — FINAL REPORT

**Status: COMPLETE.** All five slices landed, gated and pushed
(2026-06-10 → 2026-06-11, overnight run under Ingi's standing
"continue all slices, commit+push between slices" instruction).

Per-slice detail: `result.md` (slice 1), `slice2-worklist.md` (slice 2),
`slice3-plan.md` (slice 3), `../summary.md` (all slices + open points).

## Slice → headline → commit

1. **Core collapse** — Data holds ONE typed instance; three doors; entry
   lift; `Value<T>()`; new item types (source/clr/computed/absent) —
   3b981e57e, 2eebbbb29, 5a30503fc (+follow-ups).
2. **Consumer tail kills** — GetValue/Clr/AsEnumerable/SnapshotClone/
   UnwrapJsonElement dead; outbound implicit operators killed on
   text/bool/binary (~90 prod + ~100 test sites read the `.Value` face);
   IConvertible audited-kept — c5d172dc7, aec62c168, 4a414dbb9.
3. **Live templates** — `Template` init-only stamp; authored seams stamp
   (goal.list.Add, GoalCall.LoadFromFile, Action.FromWire, Data.Authored);
   text.Render door, render never cached; sniffs stamp-gated (input
   "%secret%" prints literally) — 8c090bf99, 0485fe2f8.
4. **Collection reference semantics** — CopyStructure removed with callers;
   entries mint own Data over the shared instance ([1,2,3] rule); property
   bag per binding — aef3b4377.
5. **Follow-ons** — `item.ToRaw` + `text.Value` internal (no public raw
   faces); both pinned stubs filled and green, 0 skips remain — 58424e9f4.

## Final gates

- C#: all six test slices 0 failures, 0 skips (Modules 1001, Types 729,
  Wire 538, Data 992, Generator 203, Runtime 805).
- plang: 330 pass / 4 skips / 0 real failures (halves /tmp/ph1+/tmp/ph2;
  the two `../../Simple` cross-half artifacts verified green in a
  Builder+Simple combo run).

## Open points for Ingi (also in ../summary.md)

1. **Scalar equality on an unread reference** — PROVISIONAL model position
   taken (compare parses); two LazyDeserialize goals re-pinned; easy flip.
2. **`internal` vs `private`** for text.Value / item.ToRaw — stubs said
   "private"; internal satisfies the no-public-face pins and keeps the
   slice-2 leaf edge reads alive. Flip needs a different edge mechanism.
3. **Async `Write(IWriter)`** — blocked on the channel pipeline moving off
   the sync STJ converter (the model doc's documented exception that
   pre-resolves). Prerequisite is its own work item.
4. **Peek()/Open() tighten toward `item?`** — ~75 raw-shape consumers
   remain (clr.cs note); revisit after templates shrink the surface.
5. **`Ready()` → `Value()` rename** — partially unblocked (text's public
   Value is gone) but bool/binary/number still expose `Value` properties.
