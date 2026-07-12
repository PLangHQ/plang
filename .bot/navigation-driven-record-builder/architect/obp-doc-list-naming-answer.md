# Decision — the naming rule: always singular; the enumeration is always `.list`; no synonyms, ever

**From:** architect. **Settled with Ingi (2026-07-12).** Answers `coder/obp-doc-list-naming-ambiguity.md`. Your ambiguity finding is real and confirmed — the doc's two lines pull apart, and the codebase's plurals made the wrong reading look established. Ingi's ruling closes it with one pattern, no tie-breaks.

## The rule (Ingi, verbatim intent)

**Everything is always singular. If you want a list of it, `.list`.** It's awkward English, yes — but it's a pattern we can follow, which makes it simple.

- **Owner → one of its concepts**: singular concept name, always. `app.Goal`, `app.Error`, `app.Type`, `actor.Channel`, `callStack.Error`. The owner holds several concepts; the concept name disambiguates. Never plural, never a flat compound (`TypeList`), never a bespoke synonym (`Prior`).
- **A thing's own enumeration/chain**: `.list`. `app.Goal.list` enumerates the goals; `item.list` is the item's own history.
- **The diagnostic that catches the `Prior` mistake**: when you reach for a synonym because the concept name is taken (`item.Type` is the current type, so the history "can't" be `Type`), that's the signal you are NOT naming a distinct concept of the owner — you're naming the thing's own list, and its name is `list`.

## The plurals are obsolete — Ingi: "artifacts from older design polluting the code with nonsense"

Full inventory (read this session; the ambiguity doc's table undercounted):

| site | today | end-goal |
|---|---|---|
| `app/this.cs:236` | `Services` | `Service` |
| `channel/this.cs:80` | `Channels` | `Channel` |
| `service/this.cs:25` | `Channels` | `Channel` |
| `actor/context/this.cs:102` | `Events` | `Event` |
| `callstack/call/this.cs:71` | `Children` | `Child` |

**Scope, per Ingi: the current goal is the DOC fix, not the renames.** The renames are the end-goal, not this branch's work — do not take them as a tail unless Ingi asks. What matters now is that the rule is stated so the next session doesn't re-fight `TypeList`/`Prior`. (`call.Child` reads awkwardly — accepted cost, same as `callStack.Error`; the pattern wins.)

## Your CLAUDE.md proposal — confirmed, with this wording direction

Draft it per the proposal workflow (`.bot/<branch>/claude-md-proposals.md`, docs-owned). Content to carry:

1. **Line 32 (naked-collection fix)** — replace the "singular property naming the concept" sentence with the full rule: singular concept name for an owner's concept; `.list` for a thing's own enumeration; the taken-name diagnostic (`item.Type` vs `item.list` as the worked example); never plural / flat compound / synonym.
2. **Line 10 (`app.X` collection node)** — add one clause tying it to line 32 explicitly ("the same `.list` that line 32's fix exposes"), so the two lines read as one rule instead of two directions.
3. **The stale example**: the Console rule cites `app.CurrentActor.Channels.WriteTextAsync` — actor's property is `Channel` (`actor/this.cs:74`). Fix the example in the same proposal; note the plural inventory above as the known-obsolete list so docs can state "plurals are legacy drift, not precedent."

## Acceptance

- The proposal appended to `claude-md-proposals.md` with the three items.
- No renames in this branch's diff (unless Ingi separately asks).
- `item.list` stays as landed.
