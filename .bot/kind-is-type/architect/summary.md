## Now

**2026-10-14: the branch starts.** Forked from runtime2 at cf66e2d01 (runtime2 holds all of app-systems, reviewed: decisions 725, 729, 730, 732). The focus: **a kind is a type** (711) with **parsed json becoming plang values** (717), plus a small first stage, **a `.pr` stores its own path** (736). The design is talked through with Ingi first (idea → conversation → summary → code); the plan lives in `test/plan/kind-is-type/`. Meanwhile the coder and the builder take small items from the backlog that don't touch the kind design.

The backlog (everything carried from app-systems): `backlog.md` beside this file. The app-systems record: `.bot/app-systems/architect/summary.md` (decisions 1–742), `review-findings.md`.

## Why

Ingi (2026-10-14): "you tell me, dont we have already items for it, maybe we can knock few outs or take a big one which will fix others?", then "kind-is-type it is". Half of the plang suite's 16 failures trace to one root: a kind isn't a type, so the builder can't offer `list<path>` or json as a type, and a kind is lost or can't be checked (kind/strict ×4, DataTypeReadsEntity, BareLiteralJudgedByForm, Plane_DataKeyVsProperty, DoublePlusDecimal_Errors, and the unbuilt TypedListHoldsItsType); two more come from parsed json being a C# carrier (Cut2, Narrow: `%cfg% is dict` answers false). The same root holds four design debts: "is this an action slot" asked by name (F8.1), `type.ToString` hardcoding list/dict/choice (F4.2), the `%x!type.kind%` stopgap (80e4c9e4e), and the json kind's own copy of count/first/last.

## Decisions (newest first)

| # | what | the rule it serves | detail | open | who |
|---|---|---|---|---|---|
| 743 | the branch starts; app-systems closed | one branch at a time, named by its focus | **app-systems closed** (Ingi: "didn't we merge into runtime2, we can close it"): runtime2 brought up to app-systems' last commit (cf66e2d01, nothing left only on app-systems), the coder and the builder confirmed nothing unpushed, the branch deleted; the os bot rebased plang-os-stable on runtime2. **The diary branches deleted** (their entries in runtime2) and **the diary moved to `/shared/diary/`** (Ingi: "shared/ folder?"; the 12 entries copied there with a `start.md`; Ingi tells the diary bot). **kind-is-type created** from runtime2 (Ingi: "you choose what you do. kind-is-type it is"). My choice: the `.pr` path (736) is this branch's small first stage, shape first, because it unblocks moving the app-systems plan tests. **Ingi: "2. sure"**: the coder and the builder take small backlog items that don't touch the kind design. The os bot's "a question knows who asked" proposal is on the backlog; Ingi handles the os bot. | the 711 design talk with Ingi | coder, builder |
