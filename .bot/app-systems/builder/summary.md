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

## Decider key (resolved mid-session)
Early on, builds 403'd at `llm.decider` — no TypeSafe key in env/settings. Ingi then pointed me at
`/shared/hopkaup/secrets/typesafe.txt`; passing it as `TYPESAFE_API_KEY` (the env source
`TypeSafe.Config` already reads) unblocks builds. **It is NOT yet stored in a settings table** — I
pass it per-build via env. Ingi's intent is that it live in the settings table so the decider gets
it there; that storage step is still owed (needs a plang `set %!decider.apiKey%` into the build
folder's/os settings, or an env export in the bot runner).

## What was done (v3)
Full detail: `v3/result.md`. Shapes relayed in `to-architect.md` (v3 section). All diagnoses are
**static** (reading pick/decider source) — reliable for which-branch questions, but the counts that
confirm a fix still need the key.

- **Issue 5 (stray `.goal2`) — DONE.** `git rm os/system/Build_dpricated.goal2` (v0.1 builder
  sketch, non-`.goal` ext, only `.bot` referenced it).
- **Issue 25 (C4) — MEASURED 10/10 PRESENT on head; symptom resolved.** c4-1/c4-2 fresh, cache off,
  `Template=plang` 5/5 each. The Compile user message shows `file.read 0.98` (Certain) → Prefill
  fills `file.read(Path, Template=plang)` into `=> formal:`, the writer copies it. The educator's 4/6
  was on `aa9cadcd5` (file.read under 0.90 there). My static mechanism diagnosis holds (chosen option
  reaches the formal only via `Call()`←`Prefill()` over `Mark.Certain`), so the `listed.Option` shape
  is a **latent robustness** fix for an uncertain load-vars read — architect's call whether to still
  land it. Shape in `v3/result.md`/`to-architect.md`: coder adds `listed.Option` (pick is core),
  builder renders it on `=> decider:` (`properties.template` l.40, prepared).
- **Issue 2 with key — MEASURED, REPRODUCES: Item+Key present 3/5, both dropped 2/5** (fresh, cache
  off). Drives **Option-question v2** — drafted in `v3/option-v2-shape.md` (the architect asked for
  it; they review before the coder builds core). v2: a non-choice option (`Item`/`Key`/`Conversation`)
  offers the step's own variables + "none"; Prefill writes the pick. Needs `ask:` note lines on
  loop.foreach's Item/Key + llm's Conversation (builder's); the Conversation `{continue: …}` wrapper
  is an open question for the architect.
- **Issue 32(b) — MEASURED, REPRODUCES 5/5.** `call goal Page module=%!app.module.file%` →
  `goal.call(Name="%!app.module.file%")`, `Page` lost. goal.call's note is **already correct**; the
  *writer* misreads the dotted `%!a.b.c%` variable as the name. Evidence for the architect's
  decider/writer-opaque-variable direction (they own it); hold per their instruction.
- **Issue 30 pass 2 — DONE (closed by architect).** 15 remaining os/ goals; all build clean / have
  current valid .pr; no writer-mis-map/core/write-in-formal refusals left (v2-era failures were in the
  24 swept goals). 5 rebuilt this session (dropped deprecated isSetup/etc. per 819f239c6; kept). Two
  needed one FixSteps retry (issue-17 class, recovered). Table in `v3/result.md`.
- **`cache:false` regression found (`v3/cache-false-diagnosis.md`).** On head, cache:false does NOT
  rebuild an unchanged goal in ANY folder (reproduced on hash-take: fresh build then unchanged
  cache:false rebuild → skipped, md5 unchanged). The CLI build setting isn't visible as
  `%!build.setting.cache%` at Build.goal start, so `Build.goal:7` `set default … = true` clobbers it;
  `Default.cs:116` reads true → MergePrData → `IsCached` (`goal/this.cs:288`) → skip. The LLM-cache
  half still works (`Executor.cs:111`). build.md 69-71 are correct; it's a code regression (root in
  the `.setting` projection, 7c98a5e44 family) — handed to the architect/coder. Fresh-folder
  measurements are unaffected (no .pr to merge), so this session's c4/loop/modules measurements stand.
- **Issue 32 masking shape drafted (`v3/issue32-masking-shape.md`).** Step variables → opaque `%vN%`
  for both decider and writer (via a new `s.Masked` + reverse map on step, core); templates read
  `s.Masked`; the formal answer's `%vN%` maps back before parse. Couples with Option-v2 (shared mask).
  I own the template side + measurements; core to the coder after architect review.
- **Item 6** (goal.call `Parallel`/task teaching) — gated on the coder's stages 1–2 of
  `test/plan/task/`, not started.

## Key pattern (teach the writer, not the dev's goal)
The Properties writer reads an action's **notes** and the decider's `=> decider:`/`=> formal:` lines.
A param the writer must set from a droppable trigger word (load-vars→Template, `with key`→Key,
"continue"→Conversation) is unreliable by teaching alone — the fix is the decider surfacing the
value so the writer copies it. Issue 25's remaining gap: the surfaced value reaches the writer only
when the action is *certain*; a listed-but-uncertain action needs it too (the v3 shape).

## Next session (in order)
1. **Decider key:** pass `TYPESAFE_API_KEY="$(cat /shared/hopkaup/secrets/typesafe.txt)"` to every
   `plang build`; still owed — store it in a settings table so the runner doesn't need the env each time.
2. Option v2 (`v3/option-v2-shape.md`): once the architect reviews and the coder builds the core,
   add the `ask:` note lines (loop.foreach Item/Key; llm Conversation) and measure issue 2 at 5/5.
3. Issue 25 `listed.Option` (if architect still wants it): apply the `properties.template` render
   after the coder lands the field; re-measure a *low-score* load-vars read (C4 itself is 10/10 now).
4. Issue 32: coder builds the opaque-variable core (step's variables → opaque placeholders for both
   decider and writer); I own the template side when the architect sends the shape.
5. Issue 30 pass 2: DONE (all current/clean). Architect to decide if a forced fresh rebuild of the 10
   skipped goals is wanted (needs fresh .build, off-limits for os/).
6. Item 6 after the coder's `test/plan/task/` stages 1–2 (coder v19 plan just landed).
