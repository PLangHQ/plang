# builder — summary

## Version
v3 (2026-10-02) — architect's `builder-next-session.md` queue. Continuation of the v2 bug-fixing
round (issue queue `.bot/app-systems/architect/builder-issues-2026-10-01.md`). v1 = docs pass,
v2 = the bulk of the bug fixes (see v2/).

## What this is
The builder owns `os/system/**` (goals, `.llm` prompts, templates, teaching under
`os/system/modules/**`) and the module C# under `PLang/app/module/**`. Core (`goal/**`, `type/**`,
`event`, `actor`) is the coder's, through the architect. Other bots report mapping bugs; builder
diagnoses each against the PLang-written builder and either fixes the builder's own source or shapes
a core fix for the architect/coder.

## ⛔ Session blocker — no decider key (read this first)
This environment has **no TypeSafe decider key** (`TYPESAFE_API_KEY`/`decider.apiKey` unset — not in
env, not in `os/.db` or `test/.db`). The decider (`PLang/app/module/llm/code/TypeSafe.cs`,
`api.typesafe.ai`) is the only `IDecider`, with no offline/mock path for `plang build`. So **every
`plang build` 403s at `llm.decider`** and no mapping can be *measured* this session. Previous
builder/educator sessions had the key provisioned.

To measure: provision `TYPESAFE_API_KEY` in env, or `set %!decider.apiKey% = "…"` in the build
folder's settings. Until then, items 1–4 of the queue are diagnose-and-shape only.

## What was done (v3)
Full detail: `v3/result.md`. Shapes relayed in `to-architect.md` (v3 section). All diagnoses are
**static** (reading pick/decider source) — reliable for which-branch questions, but the counts that
confirm a fix still need the key.

- **Issue 5 (stray `.goal2`) — DONE.** `git rm os/system/Build_dpricated.goal2` (v0.1 builder
  sketch, non-`.goal` ext, only `.bot` referenced it).
- **Issue 25 reopened (C4 4/6) — diagnosis CONFIRMED, shape ready.** Root: in
  `PLang/app/goal/step/pick/list/this.cs`, a chosen option (`Template=plang`) is placed into the
  formal only by `Call()` (ll.356-364), called only from `Prefill()` over `Mark.Certain` entries
  (l.299). `file.read` under Near (0.90) → `Possible` → `Call` never runs → `Template` never reaches
  the writer. Confirms the architect's "Prefill fills a chosen option only for a certain action."
  **Shape:** carry the option onto the `=> decider:` listed line — coder adds `listed.Option` +
  populates `Listing()` (reuse `Call`'s option-gather, factored to one reader); builder renders it in
  `properties.template` l.40 (prepared, lands with the C#). `prompt_c.py` twin + `PickListTests` move
  in lockstep.
- **Issue 32 (a/b) — root found (static).** `decider1.template` l.13 renders `s.Text` raw, so the
  decider reads a module name *inside* a variable path (`%!app.module.condition%`) as a step word. No
  masked step text exists. Shape (direction): core `step` masked-text property + templates use it;
  measure against a control first. (b) also check goal.call's post-`2829786ff` path-name note.
- **Issue 2 with key — static.** `pick/list/this.cs` `Code()` reads `as %x%` but there is **no
  `with key %k%`** handling in `pick/`. Robust fix = Option-question **v2** (decision 496, Ingi's
  gate; same lever as Conversation 26(1)). Shape with architect before building.
- **Issue 30 pass 2 — BLOCKED** (needs real builds → 403).
- **Item 6** (goal.call `Parallel`/task teaching) — gated on the coder's stages 1–2 of
  `test/plan/task/`, not started.

## Key pattern (teach the writer, not the dev's goal)
The Properties writer reads an action's **notes** and the decider's `=> decider:`/`=> formal:` lines.
A param the writer must set from a droppable trigger word (load-vars→Template, `with key`→Key,
"continue"→Conversation) is unreliable by teaching alone — the fix is the decider surfacing the
value so the writer copies it. Issue 25's remaining gap: the surfaced value reaches the writer only
when the action is *certain*; a listed-but-uncertain action needs it too (the v3 shape).

## Next session (in order)
1. **Get the decider key** — otherwise 1–4 stay diagnose-only.
2. Issue 25: once the coder lands `listed.Option`, apply the `properties.template` render and measure
   C4 `c4-1`/`c4-2` (target `Template=plang` 6/6; guard a plain read stays Template-free).
3. Issue 32/2: shape with architect (core masked-text / Option v2), then build+measure.
4. Issue 30 pass 2: the per-goal rebuild table.
5. Item 6 after the coder's task stages.
