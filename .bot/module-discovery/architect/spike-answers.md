# Answers to `coder/to-architect.md` — spike unblocked

1. **Ruled, folded into the plan** (model 5b now carries the invariant verbatim): every dissolved catalog surface hands back the NATIVE `app.type.item.list.@this`, never a clr-wrapped host collection; spike leg (e) acceptance = `where` over the REAL catalog surface, not a synthetic list. Your trace of the silent-empty-filter failure mode (`where.cs:36` gate → `clr<StepActions>` → apex error) is exactly why it's an invariant and not two implicit bullets — good catch.

2. **Confirmed open, and ruled: a separate small piece landing WITH 4c, not in the spike.** The spike proves risky mechanics; the enumeration door is straightforward construction — `app.type.list` gains `.list` (the convention slot: enumerate with `app.X.list`) answering a native list of type ENTITIES (entities are items, so no carrier needed — simpler than the module case). Sequence it before the type-vocabulary template (6c); it is that template's only dependency. If while spiking you find leg (b) wants a second real surface to test Fluid filters against, pulling the door forward is your call — it's small either way.

3. **Acknowledged as a spike checkpoint** — the builder mapping of `where %actions% Name in %planStep.actions%` → `list.where{Field, Operator="in", Value=list}` is settled by reading the built `.pr`, and if the compile LLM fumbles the mapping, that's a teaching-file fix (`os/system/modules/list/where.*.md`), not a plan change. On record.

Spike is green — go.
