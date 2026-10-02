# Option-question v2 — shape (builder → architect, 2026-10-02)

Requested by the architect (peer msg). v1 of the Option question is measured (issues 25 & 28 at
5/5), so v2 can be shaped. **Architect reviews this before the coder builds the core.**

## What v1 is (the baseline)
A stage-2 `Kind.Option` question (`pick/question/this.cs`) asks **which value an action's option
takes**, as a **choice over the option's own type Values + "none"**:
- `Values => Property.Type.Values is {} own ? [..own, None] : []` — the fixed enum of a `choice`-typed
  property (e.g. `Template` → `plang`/`none`; `Algorithm` → `sha256`/…/`none`).
- `Ask` = the words from the action's note line tagged `ask:` for that property.
- Asked for: each action certain from stage 1, and every action of each module stage 2 asks the
  action of (`Questions()`, `list/this.cs` ll.497-513) — gated on the property's note line having `ask:`.
- The pick is written by `Prefill` → `Call()` as `Property=value` (`None` → left out).
- Rendered by `decider2.template` `when "Option"` (ll.49-59): a `"type":"choice"` over `q.Values`.

v1 only works for a **choice-typed** option (one with `Type.Values`). For `Item`, `Key`,
`Conversation` the type has no `Values`, so `q.Values` is empty — no question is asked.

## What v2 adds (Ingi decision 496 — a non-choice option)
For an option that is **not a choice**, the offers are **the step's own variables (from the parser)
+ "none"**; the decider picks one and Prefill writes it. This is the lever for:
- **Issue 2 with a key** — `foreach %person% as %value% with key %field%` drops `Item`/`Key`.
  **Measured on head (fresh, cache off, 5 builds): Item+Key present 3/5, both dropped 2/5.** Real.
- **Conversation 26(1)** — `continue the conversation from %answer%` drops `Conversation`. Ingi ruled a
  **bare "continue" is null** (no default) → maps to the decider answering `"none"` (Call skips it).

## Members the question needs
`pick/question/this.cs` `@this`:
- **Add** `public IReadOnlyList<string> Variable { get; init; } = [];` — the step's own variables as
  `%name%` strings, in the order the parser reads them (`type/item/variable/parser`), **distinct**.
  (Populated by `Questions()`, which has `_step`; the question itself never sees the step, matching
  its "never its words" contract — the strings are data, not the step text.)
- **Change** `Values` so a non-choice option offers the step's variables:
  ```
  public IReadOnlyList<string> Values =>
      Property?.Type.Values is { } own ? [.. own, None]        // v1: a choice-typed option
    : Variable.Count > 0            ? [.. Variable, None]       // v2: a non-choice option
    :                                 [];                        // nothing to offer → not asked
  ```
  Everything downstream (`decider2.template` `q.Values`, the `"type":"choice"` render) is unchanged —
  v2 reuses the exact Option rendering; only the option list differs.

## Where v2 is asked (`list/this.cs` `Questions()`)
Today the Option loop (ll.503-513) adds a question when a property's note line has `ask:`. Two deltas:
1. For a **non-choice** property with `ask:`, build the question with
   `Variable = <step's parser variables as %name%>, + distinct`. For a **choice** property, leave
   `Variable` empty (v1 path).
2. `Item`/`Key` (loop.foreach) and `Conversation` (llm) each need an `ask:`-tagged note line
   (builder's, in `os/system/modules/loop/*.notes.md` and `.../llm/*.notes.md`) — that tag is the
   only trigger. loop.foreach is a common action and is certain in the normal case, so its properties
   are already reached by the Option loop; no new reachability needed.

## What Prefill writes
`Call()` already appends `_option[{m}.{a}.{opt}] = value` as `Property=value`, `None` skipped. For
`Key`/`Item` the value is the chosen variable (`Key=%field%`, `Item=%value%`) — works as-is.

**One open question for the architect — the Conversation wrapper.** Conversation's correct formal is
`Conversation={continue: %answer%}`, not `Conversation=%answer%`. The plain `Property=value` that
`Call` writes won't produce the `{continue: …}` wrapper. Options:
- (a) the `ask:` note / the property carries a small format so the pick is wrapped
  (`{continue: <pick>}`), or
- (b) the decider offers the already-wrapped forms as the choices (uglier — the offers would be
  `{continue: %answer%}`, "none"), or
- (c) Conversation stays a teaching case and v2 covers only Item/Key for now.

I lean (a): keep the offer the bare variables (clean for the decider), and let the property's note
shape the written form. But this is a core decision (the pick writes the formal) — your call.

## Caveat (same as v1)
Prefill fills a chosen option **only when its action ends up certain** (`Mark.Certain`; see issue 25
result). loop.foreach is certain in the normal case, so Item/Key ride the formal. An uncertain
foreach would still drop them — that is the **issue-25 robustness shape** (carry the option onto the
`=> decider:` line), independent of v2. The two shapes compose.

## Measurement owed (I have the decider key now)
- Issue 2: `/shared/educator/work/guide-examples/runs/loop-dict-1` → Item+Key present, target 5/5.
- Conversation: a named-answer continue step → `Conversation={continue: %answer%}`.
- Guard: a plain `foreach %x% as %i%` (no key) keeps Item, no stray Key; a read with no load-vars
  stays Template-free.
