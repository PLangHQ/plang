# Stage 4 — v2 plan: the 5-leg spike (4a first commit)

Architect greenlit (`c2f674647`). This commit is the de-risk spike, NOT the full 4a split. It proves the risky mechanics on real shapes before the collection relocation lands.

## Goal
Answer, empirically, the 5 spike legs the architect named — so the full 4a build proceeds on facts, not hope. Land as a C# spike test that renders real element shapes through the REAL Fluid provider and runs the REAL `list.where`.

## The 5 legs (architect, plan.md pieces §SPIKE)
- **(a)** enumeration of host elements — `{% for m in modules %}` over a native `item.list` of element POCOs.
- **(b)** Fluid filters (`where:`/`map:`) over element properties on a native plang list.
- **(c)** the `property` row host — template reads row fields.
- **(d)** async prose doors — **the least-proven.** Draft doors are `async Task<string?> Description()` (METHODS). Liquid `{{ m.Description }}` reads a PROPERTY by reflection — it neither invokes methods nor awaits Tasks. Hypothesis: **prose must be a sync property resolved at mint**, not an async method. Spike confirms and pins the consequence.
- **(e)** `list.where`'s `subject.Get(field)` over `clr(action)` — filter a native list of real action elements by a field. Acceptance (ruled): `where` over a REAL catalog surface, native list (not synthetic) — `where.cs:36` gates on `item.list`, a clr host falls to apex error.

## Approach
One spike test file, `PLang.Tests/Modules/App/Modules/Stage4Spike/HostRenderSpikeTests.cs`:
- Minimal spike POCOs mirroring intended shapes (element `Name` + `Actions` native list + prose doors in BOTH forms — async method AND sync property — to measure leg d; action `Name`/`ActionName` + `Properties`; property row `Name`/`TypeName`/`IsVariable`/`Nullable`/`Default`).
- Build native `item.list` via the internal `List<object?>` ctor (InternalsVisibleTo covers PLang.Tests.Modules) — exactly the draft's construction.
- Render through the real `app.module.ui.code.Fluid` provider (same path the builder uses).
- Leg (e): native list of REAL `clr(action)` from `Describe()`, run real `list.Where` with Operator `"in"`.

Each leg asserts pass, OR surfaces the exact failure so the 4a design absorbs it. Spike POCOs are test-local throwaways — zero production shape change this commit. The real element/collection shapes land in 4a proper once the spike's answers are in.

## Acceptance
- No new C# reds vs `v2/baseline-fails.txt`.
- Each leg has a definite verdict (pass / precise-failure-with-consequence).
- Leg (d)'s verdict pins whether prose is sync-at-mint or async-door — the single biggest shape input to 4a/4c.

## Out of scope (this commit)
The collection relocation (`module/this.cs` → `module/list/this.cs`), freeing the element slot, deleting `Describe`/`RegisterModuleChoiceTypes`, template rewrites — all 4a-proper and later. The spike informs them.
