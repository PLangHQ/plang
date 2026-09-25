# Stage 4: the plang builder builds in formal

**Goal:** `plang build` runs the decider + prompt C + the formal parser, writes the `.pr` as formal in a thin envelope, and the builder builds itself — the goal of `get-builder-running`, on the design proven in python.
**Scope:** the formal serializer in C#, the `.pr` envelope, the builder's goals (Decide, Properties, Settle) on the new pipeline, the bootstrap, tests. **Excluded:** the parked drafts (action-as-value, timeout), snapshot, security, splitting long goals, 20–30-step goldens.
**Dependencies:** stages 1–3 (python), and round 4 at the model choice (C + nano).

Written by the architect while in charge (Ingi away, 2026-09-25). Design narrative and rulings: [vision.md](vision.md) §3–§4b.

> **Coder, you own this.** The sub-steps are the order; the shapes are sketches. Each sub-step starts with a short design check-in to me where marked **(check-in)** — trace the incumbent, show the shape, then build.

## Why

The python lab proved the shape: the decider picks, the LLM fills formal, the parser types it, the checks make every miss loud. None of it runs in plang yet: the builder's own `.pr` are in an old format and cannot load, the plang template lists no actions (F1), and the `.pr` stores JSON rows the LLM had to type itself. Stage 4 moves the proven pipeline into the builder and makes formal the one representation of a program's actions.

## Sub-steps

### 4a. The formal serializer — reader and writer in C# **(check-in)**

- Formal is a **serialization of the program graph**, beside the existing serializers (`app/channel/serializer/…`): values write themselves (a text quoted, a number bare, a list `[…]`, a dict `{k: v}`, a choice quoted, an action `module.name(Name: type = value, …)`), and the action writes itself — the same OBP rule as the wire: the value writes itself, no parallel structure, no converter per type. Trace the serializer architecture (`IWriter`, the `application/plang` serializer, how a reader is registered) and show where formal fits before building.
- **Reader:** formal text → typed actions. The type of each value comes from the property's declared type (the action catalog, `%action.Property%`) or, in an `item` slot, the literal — never from the text except a written `Name: type` (which must match). `?=` → a frozen default (`action.Default`). `{ }` → a condition's body (`action.Child`) or the action a modifier wraps. `Recovery=[…]` → `action.Recovery` (`goal/step/action/this.cs:67`). Bare choice values accepted. Errors carry line, column and the fix, as python's do.
- **Writer:** always writes types; frozen defaults as `?=`; a modifier wraps its action, nesting in modifier order; argument rows typed (`{kind: text = "x"}`); dict literals with quoted keys.
- **Tests:** the 58 golden steps round-trip (formal → actions → formal) byte-equal with python's `formal_golden.txt`; untyped input parses to the same actions; every python error case has a C# twin.

### 4b. The `.pr` envelope **(check-in)**

- The goal/step facts stay JSON (name, path, hash, visibility, flags; each step's index, text, lineNumber, comment, warnings), and each step's `action` is **one formal string**. Indented child steps keep their own index/text/line — decide with me how the envelope carries a condition's child *steps* (from indentation) versus an inline `{ }` body.
- The action reader's JSON keys (`property`, `default`, `modifier`, `recovery`, `child` under an action) go; the step reader reads `action` through 4a's reader. An old-format `.pr` is refused loudly (`PrFormatOutdated`), as today.
- **Modifier order:** the written nesting is the truth; `modifier.list` owns wrapping in that order (the `modifier.list` refactor queued on `get-builder-running` lands here: the list owns `Wrap`, groups catch clauses, `action.Run` asks it). The `[Modifier(Order=…)]` numbers become the default the prompt teaches, not a runtime sort — confirm with me when you trace `step.Nest`.
- **Tests:** a `.pr` written and read back is equal; a goal loaded from a formal `.pr` runs through the engine (test through the PR, not around it).

### 4c. The builder's goals on the new pipeline

- **Decide.goal:** decider v5's shape in plang templates — one choice per step (the main module, bare names, descriptions once in the state), yes/no for the 6 common actions (their example step texts beside them), the runner-up and main-module yes/no, stage 2 for the main module's action, `else`/`elseif` by name. Steps numbered from 0. TypeSafe is already the registered `IDecider`.
- **Properties.goal:** prompt C (`PropertiesC.llm` + a C user template — the goal as written, picks ≥ 0.5 with scores, the pre-filled formal line with known values, the types, each action once, held-action definitions). The answer is text (formal); no JSON schema. F1 disappears: the template walks each step's picks, not the catalogue.
- **Settle:** parse (4a) → `build.match` (step for step, child-over-indent, drop copied bodies incl. partial copies) → the chain rule (ElseWithoutIf → SourceError; BodyBesideCondition / BodyMissing → retry) → the double check (a ≥ 0.9 pick missing, an unlisted action → retry; after the retry a certain contradiction fails loudly, an unsure pick builds with a warning — none for a known-value pick) → fold → `build.goalsSave` writes the envelope.
- **The catalogue filter** (`type/property/list/this.cs:82`): only `clr` stays hidden; action- and goal-typed properties reach the prompt. Held-action definitions (goal.call) are added when a listed action takes actions.

### 4d. Bootstrap — the builder builds itself (#12)

1. The python stand-in (`build_pr.py`, now decider v5 + prompt C + the formal writer) builds the builder's own goals into the envelope format, into `tools/decider/out/`.
2. Review against the source and install over `os/system/builder/**/.build/` (the reviewed install; no shell rewrite of `.pr`).
3. `plang build` loads: verify layer 3 (the channel), then build `Tests/BuilderSanity/AddItem.goal` to completion — `get-builder-running`'s "done".
4. The plang builder rebuilds its own goals; diff against python's output — same pipeline, same prompt. Every difference is explained.

## Demolition (what must not survive)

| Dies | When |
|---|---|
| the JSON action rows in `.pr` (`property` `{name, type, value}`, `default`, `modifier`, `recovery`, `child` under an action) and their reader branches | 4b |
| the stage-3 JSON schema `llm/Properties.schema`; prompts A and B (`Properties.llm`, `PropertiesB.llm`, `propertiesUser.template`, `propertiesUserB.template`) | 4c, once C runs in plang |
| the catalogue walk + `menu[s.Index] contains choice` in the stage-3 template (F1) | 4c |
| `tools/decider/pr_bootstrap.py` (key renames of old JSON) | 4d |
| the old `formal` field in old `.pr` files | 4d (every `.pr` regenerated) |
| `step.Nest`'s sort by `[Modifier(Order)]` as a runtime rule | 4b (confirm at check-in) |

**Stays:** `goal.step.list.Match`, the chain rule, `build.fold`, `SourceError`, the decider eval tools and runs (the lab), `build_pr.py` as the stand-in until the plang builder matches it.

## OBP validation

| Surface | Check | Result |
|---|---|---|
| formal | a serialization, owned like the others | the value/action writes itself; no parallel structure (4a) |
| the formal reader | typing has one owner | the property's declared type; the literal only in `item` slots |
| the `.pr` envelope | nothing stored twice | facts in JSON, actions only in formal |
| `modifier.list` | naked collection → X.list | owns wrapping and catch grouping (4b) |
| the double check | one door | beside `build.match`, in Settle |
| names | no verb+noun | to check at each check-in |
