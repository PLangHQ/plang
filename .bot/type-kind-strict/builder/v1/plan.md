# Builder v1 — plan

## Task
Review the `type-kind-strict` branch from the builder's perspective (LLM mapping
quality + builder behavior), verified against real LLM trace files. During that
review, an attempt to build the builder itself (`plang build` from `os/system/`)
surfaced two pre-existing failures unrelated to the type-kind work. Diagnose them
at the PLang level and write a detailed handoff for **coder** (this report is the
handoff — it travels via git, not chat).

## Scope
- This is a **diagnosis + handoff**, not a code change. No production C# or `.goal`
  files are edited. (`Run.goal` was read only — explicitly NOT to be touched.)
- The two findings are runtime/engine concerns → coder's turf. The builder bot
  flags them with full PLang-level detail and asks coder for the fix + better
  error messages.

## What was verified first (the actual review — PASS)
- The new structured `type` entity `{name, kind?, strict?}` flows correctly
  through compile for every load-bearing case (`as text/markdown strict`,
  `as image/gif strict`), confirmed from trace LLM-raw vs final `.pr`.
- `CompileUser.llm` Type-reference block renders fully populated
  (PrimitiveNames, Kinds table, rules).
- Back-compat: old flat-string `type` in existing `.pr` files still deserializes
  (lenient converter in `app/type/this.json.cs`) and rebuilds to the dict form —
  observed live on `os/system/.build/build.pr`.

## Findings to hand to coder
1. **`error.throw` rejects a non-string `Message`** — `- throw %!error%` (re-throw
   an Error object) must be valid; it currently coerces to string and crashes.
2. **Conversion-failure error messages are unhelpful** — must state target type,
   variable/parameter name, and actual content in plain language.

(Plus a builder-side observation on `Run.goal`'s `run before app start event` step
mapping to `event.on` with no `GoalToCall` — flagged for the team, NOT for a
builder teaching-file change, and NOT a goal edit.)

## Deliverables
- `result.md` — the detailed coder handoff.
- `summary.md` — bot-root summary.
- `report.json` — session appended.
