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

## How the offer is produced — ACCEPTED SHAPE (architect, 2026-10-02)
**No choice-or-not branch in the question.** A branch like `Property.Type.Values is {} own ? … :
Variable.Count > 0 ? …` is a type-switch ("is this a choice?") — misplaced behavior. The **property's
type answers what it offers for a step**, through **one member on the type**, called once by
`Questions()`: a closed set (a `choice` type) offers its values; any other type offers the step's own
variables. The coder names and places that member (core). Consequence: **`question.Variable` is NOT
needed** — the question keeps its "never its words" contract; the offers come from the type given the
step. `Values` stays a thin read of that member + `None`; `decider2.template`'s `when "Option"` render
is **unchanged** (still a `"type":"choice"` over `q.Values`). v2 reuses the exact Option rendering.

## Where v2 is asked (`list/this.cs` `Questions()`)
Today the Option loop (ll.503-513) adds a question when a property's note line has `ask:`. Two deltas:
1. For a **non-choice** property with `ask:`, build the question with
   `Variable = <step's parser variables as %name%>, + distinct`. For a **choice** property, leave
   `Variable` empty (v1 path).
2. `Item`/`Key` (loop.foreach) and `Conversation` (llm) each need an `ask:`-tagged note line
   (builder's, in `os/system/modules/loop/*.notes.md` and `.../llm/*.notes.md`) — that tag is the
   only trigger. loop.foreach is a common action and is certain in the normal case, so its properties
   are already reached by the Option loop; no new reachability needed.

## What Prefill writes — ACCEPTED (architect)
`Call()` already appends `_option[{m}.{a}.{opt}] = value` as `Property=value`, `None` skipped. Prefill
writes the **plain pick for every option** — `Key=%field%`, `Item=%value%`, and
**`Conversation=%answer%`**. **No wrapper.** The conversation type is born from a response:
`Conversation=%answer%` makes `{continue: %answer%}` through `conversation.Create`, i.e. **the value's
own door does the shaping**. Today `conversation.Create` declines anything but a dict
(`llm/type/conversation/this.cs:35-40`) — the coder makes `Create` accept the plain pick. (Core,
coder's.) Ingi: a bare "continue" is null → the decider answers `None` → `Call` leaves it out.

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
