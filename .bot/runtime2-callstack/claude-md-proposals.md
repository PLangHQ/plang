## tester — v1 — 2026-05-04
**Target:** /CLAUDE.md
**Why:** Tester v1 filed a wrong `needs-fixes` verdict on this branch — 5 phantom PLang failures (`Action 'debug.tag' not found`, `%!callStack.Audit` null/cyclic) that vanished after rebuilding PlangConsole from a clean `bin/obj`. Root cause: bot runners inherit a pre-built `PlangConsole/bin/Debug/net10.0/plang` across sessions; reflection-based module discovery scans the loaded assembly, so a stale binary produces phantom failures for symbols that already exist in source. The C# suite is immune (`dotnet run` recompiles in-place); only `plang --test` is exposed. Affects every bot that runs the PLang test suite, not just tester. User explicitly approved adding this to `/CLAUDE.md` after we tracked down the env-divergence between coder's 181/181 and tester's 176/181.

**Proposed change:** Append to the existing "## Running plang Tests" section in `/CLAUDE.md`, after the existing bullets:

```markdown
### Stale-binary trap

`plang --test` uses `PlangConsole/bin/Debug/net10.0/plang` — a pre-built
executable, **not recompiled per session**. Bot runners inherit this binary
across sessions. Phantom failures with shapes like `Action '<module>.<action>' not found`
or `(null)` reads of `%!<infra>%` properties — for symbols that exist in
source on the current commit — mean a stale binary scanned via reflection,
not a real bug.

Before claiming any PLang test result, rebuild from clean:

```bash
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

The C# suite is immune (`dotnet run --project PLang.Tests` recompiles
in-place). Only `plang --test` is exposed to the trap.

Do **not** delete `Tests/**/.build/` — those are tracked `.pr` files, not
build artefacts. The "NEVER delete .build folders" rule above applies.
```

---

*Note: per the reviewer-bot rule in /CLAUDE.md ("Reviewer bots … do NOT propose CLAUDE.md changes"), this entry is filed at the user's explicit request after a real incident on this branch, not at the tester's discretion.*

---

## codeanalyzer — v1 — 2026-05-04
**Target:** /CLAUDE.md (project root — repo CLAUDE.md, persistent and auto-loaded for every agent)
**Why:** Coder's d2d9d2be did the structural OBP refactor (promoted `Audit`/`Trail`/`Errors`/`Children` to their own `@this` types) and added an "OBP Smell Checklist" to `Documentation/v0.2/good_to_know.md` with a worked example. Per the user's earlier guidance ("stronger directing than just good_to_know.md, it should be baked into your fundamentals"), the high-level rule needs to be in `CLAUDE.md` itself — `good_to_know.md` is read on demand, `CLAUDE.md` is auto-loaded for every agent. The CLAUDE.md entry should be short and point to `good_to_know.md` for the full checklist + worked example, not duplicate it.

The miss this catches: codeanalyzer ran v1/v2/v3 against this branch with line-level passes (race, off-by-one, dead code) and did not flag the three `List<IError>` sites (`stack.Audit`, `app.Errors.All`, `call.Errors`) or the cross-file `lock (caller.Children)` choreography between `CallStack.Push` and `Call.DisposeAsync`. Security v1 caught the resulting concurrency findings but a shape pass would have caught it earlier and as one finding instead of four.

**Proposed change:**
Insert a new section before `## Source Generator`:

```markdown
## OBP Shape Smells (audit before writing or reviewing C#)

When reading or writing C#, run this checklist. Each item is a yes/no question; any "yes" means the shape is wrong and the fix is structural, not a line edit.

1. **Public mutable collection with rules enforced from outside.** A type exposes `public List<T>` / `Dictionary<K,V>` / `HashSet<T>` and the `Add` / `Remove` / locking / eviction lives in another file. The collection should become its own `@this` type with private lock and `Add(...)` / `IReadOnlyList<T>` surface.
2. **Cross-file lock target.** `lock (other.X)` taken from outside `other`'s class — the type that owns the data isn't the type that owns the discipline.
3. **Same logical thing stored twice across types** (overlapping semantics, similar names, same element type, same role).
4. **Allocate-here / mutate-there / clean-up-elsewhere.** One collection's lifecycle split across three files.

If removing one line of choreography requires editing three files, those three files are one missing type.

Full checklist and worked example: `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist".
```

---

## codeanalyzer — v1 — 2026-05-04
**Target:** /characters/codeanalyzer/character.md
**Why:** Pass 1 ("OBP Compliance") today checks folder layout and `@this` convention but doesn't run a structural-shape checklist with line-cited findings, so shape-wrong code with line-correct internals passes (this branch is the case study — three v1/v2/v3 passes returned CLEAN/MINOR while the `Audit`/`All`/`Errors` triple and `lock (caller.Children)` choreography sat in plain sight). Splitting Pass 1 into 1a (rules) + 1b (shape smells) makes the shape audit a *separate, mandatory, line-cited* sub-pass. Also fixes two existing inconsistencies in the file: it references `/PLang/App/CLAUDE.md` "OBP — the rules in one breath" which doesn't exist on disk, and disagrees with itself on rule count ("the 6 OBP rules" then "which of the 5").

**Proposed change:**
Replace the current Pass 1 block with:

```markdown
### Pass 1: OBP Compliance

Two sub-passes — both mandatory, both produce line-cited findings. Don't skip 1b just because 1a came back clean; they catch different things.

**1a. OBP rules** — check every file against the OBP rules (project `CLAUDE.md` "OBP Shape Smells"; full checklist in `Documentation/v0.2/good_to_know.md`; formal treatment in `Documentation/v0.2/object_pattern_formal.md`). For every violation, output:
- **File:line** — exact location
- **Rule violated** — which one
- **Current code** — the offending snippet
- **OBP-correct form** — what it should look like
- **Why it matters** — what coupling or complexity this introduces

**1b. Shape smells** — run the four-item checklist from project `CLAUDE.md` "OBP Shape Smells" against every file in scope, with explicit yes/no per item:

1. Public `List<T>` / `Dictionary<K,V>` / `HashSet<T>` with `Add` / `Remove` / locking / eviction in a different file?
2. `lock (other.X)` from outside `other`'s class?
3. Two collections of the same logical thing across types (similar names, same element type, same role)?
4. Allocate / mutate / clean-up split across three files for one collection?

For each "yes," cite all participating files and lines, name the missing type that would absorb the discipline, and list the call sites that would collapse. A clean Pass 1a does NOT imply a clean Pass 1b — line-correct code can still be shape-wrong.
```

---

## codeanalyzer — v1 — 2026-05-04
**Target:** /CLAUDE.md (project root)
**Why:** Two failure modes converge on the same fix. (1) Direct edits to `characters/*/character.md` hit `EROFS: read-only`. (2) Edits to *agent-level* CLAUDE.md (the per-agent system-prompt CLAUDE.md, not the repo CLAUDE.md) get overwritten on next session restart and silently lost. The only durable channel for changes to any CLAUDE.md or character file is `.bot/<branch>/claude-md-proposals.md` for **docs** to pick up. Tester's entry in this same file references "the reviewer-bot rule in /CLAUDE.md" — a rule that does not actually exist in `CLAUDE.md`. The convention is operating on hearsay; an agent without prior context defaults to direct edits and the change vanishes. Documenting both the workflow and the reviewer-bot exception in repo `CLAUDE.md` (which persists, and is auto-loaded) closes the loop.

**Proposed change:**
Add under `## Learning` (or as a new sibling subsection):

```markdown
## Proposing CLAUDE.md / character changes

Do **not** edit CLAUDE.md or character files directly. Two reasons:

- **Agent-level `CLAUDE.md` files are overwritten on next restart** — edits are silently lost. (This applies to per-agent CLAUDE.md, not this repo CLAUDE.md.)
- **`characters/*/character.md` is read-only** on most workspaces (`EROFS`).

The repo `CLAUDE.md` you're reading right now does persist, but it's docs-owned — same proposal workflow.

**To propose any change** to a CLAUDE.md or character file, append to `.bot/<branch>/claude-md-proposals.md`:

    ## <author> — v<N> — <date>
    **Target:** <path>
    **Why:** <one paragraph — what gap, what evidence, why now>
    **Proposed change:** <exact text to add/replace, in a fenced block>

See prior branches' `claude-md-proposals.md` files for examples. Docs picks proposals up during a docs pass and applies the ones that hold up.

**Reviewer bots** (codeanalyzer, security, tester) do NOT propose CLAUDE.md changes on their own — only on explicit user request after a real incident on the branch. When filing under that exception, note it in the proposal footer.
```

---

*Note: codeanalyzer is a reviewer bot. These three entries are filed at the user's explicit request after the user identified the OBP-shape audit gap on this branch and asked for stronger directing into agent fundamentals — not at the analyzer's discretion.*
