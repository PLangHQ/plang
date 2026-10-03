# Build Checks — how a build answer is judged

When the builder's LLM writes a step's actions (in formal), the answer is **judged before it is
kept**. A step is only frozen into the `.pr` once it passes every gate; a gate that refuses turns
the step's code back to empty and hands the refusal to the step-fixer, which retries with those
exact messages. The messages are written for that retry — each one names the step and says what to
do, not just that something is wrong.

This doc is the map of those gates: what each refuses, and through which door it asks. The gates
run per step in `app/goal/step/list/this.cs` (the build loop), in this order:

1. **Validate** — the code judges itself.
2. **Cover** — the answer holds what the step's words say.
3. **Unwritten** — numbers the words give without digits.
4. **Build** — the handlers build their own code (only if Validate passed).
5. **Agree** — the answer agrees with the decider's picks.
6. **Scope** — the step's variables match the settings store.

Any refusal from any gate is collected; if a step has even one, its code is reopened and the
step-fixer runs. A step with none freezes its code (defaults frozen, warnings kept).

## Validate — the code judges itself

`step.Validate` (`app/goal/step/this.Validate.cs`) asks the action chain whether its code is sound
on its own terms — a clause that needs a partner it doesn't have (`else` with no `if`, keyed
`ElseWithoutIf`), and the like. The verdict keeps its cause's **key**, so `on error key "…"` sees
what went wrong, not merely that a step failed. Code that fails Validate is not built.

## Cover — the answer holds the step's words

`step.Cover` checks the answer **as the LLM wrote it** (before any handler's `Build` rewrites it)
against the step's text, both directions, so nothing the words say is dropped and nothing the
answer adds is invented. The step's own markers are plang's, not human language: a `%variable%`, a
quoted literal (`"…"`, or `'…'` when it isn't inside a word), a bare number, a bare path. Each is
checked through the **value's own door**, never by matching the formal's syntax.

| What the step holds | How Cover checks it |
|---|---|
| a `%variable%` | the answer names the same variable (case-insensitively); one the answer names that the words don't is invented — a system `%!…%` excepted |
| a `%!…%` root | it names something plang has — a binding, a shortcut (`%!goal%`, `%!step%`, `%!error%`), an `%!app…%` member, or a module's (`%!llm.setting.cache%`); one that names nothing reads nothing at run |
| a quoted literal | one of the answer's values **holds** it (`value.Holds(text)`) — each value answers for itself, so a `separator` named `comma` holds `","`; the line's own syntax (`Left=%x%`) holds nothing |
| a bare path (`/photos`, `./x`) | the same — held by one of the answer's values |
| a number written as digits | it stands in the answer on its own, or a value **holds** it (`value.Holds(number)`) answering for itself — a `duration` written `1s` holds the step's `1000` from `1000 ms` |
| — | a `text` the answer writes that the step's words don't contain is invented (`channel="X"` on a step that names no `X`); a choice option, a number, and a dict's keys are not texts |

Two edges worth knowing: a literal whose backslashes the answer **doubled** (`\\n` for the step's
`\n`) is told so by name, so the retry writes the escape as the step does; and Cover runs after the
code dropped the defaults it didn't need to write, so a default left out never counts as missing.

## Unwritten — numbers the words give without digits

A step may give a number in words — "retry once", "half a second" — with no digits for Cover to
match. `step.Unwritten` finds every number the code wrote that the words don't write as digits.
Whether the words really give it is the **decider's** to say: with the decider's confirmation in
hand each is checked against it; without one, it is asked. An unconfirmed number reopens the step.

## Agree — the answer agrees with the decider

`step.Pick.Agree` (`app/goal/step/pick/list/this.cs`) is where the code meets what the decider said
the step may do. (A step written directly in formal was asked of no decider, so it skips this.) It
refuses:

- an action the decider is **certain** of that the code leaves out (a certain action may sit
  anywhere in the step — inside a recovery, a callback);
- an **option** the decider says the step gives that the chosen action leaves out — unless the
  value is the option's default, which the code holds by leaving it out;
- an option of a **closed** choice (`kind.IsClosed`) the decider says the step does **not** give,
  that the answer writes anyway — invented, so leave it out;
- an action the decider **did not list** for the step (the refusal names the step's list);
- an action **held as a value** that isn't listed — only `goal.call` may be held unlisted;
- a step whose words say it **writes** a variable when no `variable.set` in the code writes it.

It also carries two **warnings** (built with, not refused): an action the decider was only
*possibly* sure of, and one taken from its *popular-action* choice.

## The decider's stage 2 is told the variables' types

The decider works in two stages; stage 2 resolves the questions stage 1 left open — for each
module, which action; whether the step uses it at all; a branch's yes/no; a popular action's share;
and which option a choice takes. Stage 2 is given the step's variables **with the types the build
knows** (`app/goal/step/this.Scope.cs`, `Scope.Typed`), so it chooses knowing `%count%` is a
`number` and `%name%` a `text`. Scope also checks those variables against the settings store and
refuses one whose stored type is wrong — one message per mistyped property.

## A refusal reaches the step-fixer

Every refusal above is collected against its step. A step with any refusal (or an unconfirmed
number) has its code set back to empty — reopened — and the step-fixer retries it with the
collected messages, which are phrased as instructions. A step with no refusal takes its code: its
warnings are attached and its defaults frozen, and it is written to the `.pr`.

## The teaching-edit rule

The decider and the learner pages are both pinned to the catalog, so **editing teaching changes
what they pin — for types as well as modules.** The decider's prompt C lists every type with its
description, so a type's `type.description.md` moves the decider pin exactly as an action's notes
do. A commit that touches `os/system/modules/**/*.md` **or `os/system/type/**/*.md`** re-runs every
decider fixture — `PickListTests`, `ConfirmTemplateTests`, `LineTwinTests`, `PickOptionTests` — and
the page goldens — `ModulePageTests` for a module, `TypePageTests` for a type — and re-pins each one
it moved through that fixture's own `AcceptTheFixture` / `AcceptTheGolden`, diffing word by word.
(The pinned fixtures are not rebuilt by `dev.sh test`'s class filter; run the Explicit
`AcceptTheFixture` directly against the test binary, e.g.
`PLang.Tests/Wire/bin/Debug/net10.0/PLang.Tests.Wire --treenode-filter "/*/*/PickListTests/AcceptTheFixture"`.)

Treat a re-pin as a review, not a rubber stamp: a re-pin made while the model is under load can
capture a worse answer than the one it replaces. Read the diff before committing a moved pin — a
pin is only as good as the answer it froze.
