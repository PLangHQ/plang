# Stage 1 — the `*` kind's Output over-reflects (reaches runtime infra) — for architect

**From:** coder. **Context:** the kind redesign (behavior→kind, one door, kinds under their
type) COMPILES and Read works (round-trip DoD passes). But the Output side regressed: the
`*` (reflection) kind, now the universal catch-all Output, reflects objects the old
`OutputTagged` never touched — including runtime **infra** (a `context.@this`) — and crashes.
Need a ruling on what bounds the `*` kind's Output.

## The crash (real stack, `ViaChannel_AssemblesActions_AndKeepsParamType`)

```
NormalizeException: json.Writer received a value of type app.actor.context.this
                    that isn't part of the tree contract.
  at json.Writer.Value(object)                        writer.cs:196   ← writer can't render a context
  at kind.@this.WriteReflected(...)                   kind/this.cs:162 ← else → writer.Value(context)
  at reflection.@this.Output(...)                     reflection/this.cs:185
  at kind.@this.WriteReflected(...)                   kind/this.cs:154 ← IEnumerable → Kind[type].Output
  at reflection.@this.Output(...)                     reflection/this.cs:185
  at clr.@this.Output(...)                            clr/this.cs:155  ← clr<X>.Output → * kind
```

A `clr<X>` writes itself → `*` kind reflects X's props → a prop's value is reflected again →
one of *its* props is a `context.@this` → `writer.Value(context)` → the writer has no case for
`context` → throw. (Before this, the same shape as **infinite recursion** — a cyclic graph
with non-`[JsonIgnore]` props looped to depth 1000. I patched that with a `Tagged.IsTagAware`
gate + collection dispatch in `WriteReflected`, but that's a band-aid on the same disease.)

## Root cause — two things compound

1. **The `*` kind is now the universal Output.** Before the drop, only the pr-graph hosts
   (goal/step/action, all `item`s) reflected, via `item.Output → OutputTagged` (Store view,
   `[JsonIgnore]` back-refs skipped). Now goal/step/action are `clr<POCO>`, and **every**
   `clr<POCO>.Output` routes to `reflection.Output`. So far more objects reflect — including
   runtime infra that gets wrapped in a `clr` somewhere and asked to write itself.

2. **The tag-unaware fallback dumps everything.** `Tagged.PropertiesFor(type, mode)` returns
   ALL public properties for a type with NO `[Out]`/`[Store]` tag (the "transparent type"
   rule). So `reflection.Output(untaggedInfraObject)` walks its entire C# surface — including
   the `Context` / `App` / back-refs that pull in the infra graph. A `context.@this` is not a
   wire value; the writer rightly refuses it.

Also visible: a **non-`IList` `IEnumerable`** hits `WriteReflected:154` → `Kind[type]` → the
`*` kind (not the list kind, which only claims `IList`) → reflects its *properties*, not its
elements. So a `HashSet`/LINQ enumerable/custom collection reflects wrong too.

## The decision — what bounds the `*` kind's Output?

The `*` kind's charter is "navigate/write ANY object by reflection." But **writing runtime
infra (context, app, callstack, a non-wire POCO) is never valid** — those must never cross the
wire. Options:

- **A. Reflect only TAGGED types.** `reflection.Output` (and `WriteReflected`) reflect a type
  only if it carries `[Out]`/`[Store]` (a deliberate wire contract, cycles `[JsonIgnore]`-guarded).
  An UNtagged object is not reflected — it's `writer.Value` (as the old default did) or an
  explicit "not serializable" error. Kills the infra-graph reach; the transparent-type fallback
  stops applying to Output. (My `WriteReflected` gate does this for nested values, but top-level
  `clr<untagged>.Output` still dumps all props — this option makes it consistent.)
- **B. Untagged infra should never reach Output at all.** If a `context`/`app` is being wrapped
  in a `clr` and Output'd, that's the upstream bug — find who does it. (Harder to bound; the `*`
  kind is the catch-all precisely so anything CAN be carried.)
- **C. Collections:** the list kind should claim `IEnumerable` (not just `IList`) so any sequence
  enumerates instead of reflecting — orthogonal to A/B but needed regardless.

My lean: **A + C.** A matches OBP (a type opts INTO the wire by tagging; the `*` kind doesn't
get to serialize your private infra graph), and it's the minimal, safe bound. C is a
straightforward widening of the list kind's claim.

## Suite impact (why this matters now)
Full-suite delta vs the 129 baseline is elevated (Modules ~76, Data ~52, Runtime aborts early
on this crash) — most of it traces to this over-reflection. Fixing the bound should recover the
bulk. Anchors: `app/type/kind/this.cs:145-164` (WriteReflected), `app/type/item/kind/reflection/this.cs:182-186` (Output), `app/channel/serializer/filter/Tagged.cs` (transparent-type fallback + my `IsTagAware`).

## Ask
Ruling on A / B / C (or another shape) for what the `*` kind is allowed to reflect on Output —
then I bound it and drive the suite back to baseline. Full state:
`coder/stage1-progress-state.md`.
