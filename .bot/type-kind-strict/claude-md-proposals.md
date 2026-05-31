# CLAUDE.md / character proposals — type-kind-strict

Append-only. Docs bot reviews at merge time and applies the ones that hold up. Entries are mostly CLAUDE.md / character changes; one **docs-restructure** entry (below) is for the docs bot to schedule, not a CLAUDE.md edit.

---

## architect — 2026-05-31
**Target:** `characters/coder/character.md`
**Why:** OBP violations recur in coder output (e.g. `BuildTypeEntries` and the `app.type.list.@this` registry surface on this branch — collection-proxy verbs, verb+noun names that restate the class, `Get`-twins). The coder's character treats OBP as a single line ("every line must follow OBP", line 7) while codeanalyzer downstream runs it as a full yes/no pass — so shape violations are written, then caught a review round later. The coder's existing "catch yourself" guidance (line 23) is about *navigation* (don't invent parallel structures), not *shape/naming*. Give the writer the same write-time self-audit and name the specific naming tell. Ingi requested making OBP more visible in the coder character.
**Proposed change:** Add a new MANDATORY section (suggested placement: right after "How You Work — MANDATORY", or fold into it):

```markdown
## OBP is the deliverable — audit your own shape before handoff

OBP isn't a style note you nod at — it's the product. Most of your regressions are **shape** regressions, cheap to catch at write-time and expensive once codeanalyzer or the auditor finds them. So run the check codeanalyzer will run on you, *first*, on yourself.

**Re-read the canonical sources — don't work from memory:**
- Project `CLAUDE.md` → `## OBP Shape Smells` — the terse numbered checklist, always loaded (it grows; re-read it each time).
- `Documentation/v0.2/obp-smells.md` — the worked right/wrong examples, naming tells, variant design (the operational audit reference).
- `Documentation/v0.2/object_pattern_formal.md` — the formal pattern (philosophy + the 9 rules).

**Before you call a stage done, run every smell against your diff with an explicit yes/no.** Any "yes" is a structural fix, not a line edit.

**The naming tell — the one you miss most.** Names say what a thing IS (noun) or DOES (verb); the *structure* carries the meaning, so the name shouldn't have to. A **verb+noun method name** — `BuildTypeEntries`, `GetValidValues`, `GetBuilderTypeNames` — is almost always a smell:
- If the noun **restates the class** (`BuildTypeEntries` on the type list, which already *is* the type entries), the method is usually **proxying a collection that should just be exposed** — "Collections are the API; don't proxy with `Build`/`Get`/`Add`."
- A `Get`-prefixed twin beside a noun (`ValidValues` + `GetValidValues`) is the same thing exposed twice (smell #3).
- **Rule of thumb: if you can't name it in one clean word, the shape is probably wrong.** Reaching for a verb+noun is the signal to stop and ask — does this behavior belong on the owner? Should the collection be the API? Am I building a middleman?

**When you catch yourself** reaching for a `Get`/`Build`/`Manage`+noun method, a `*Helper`/`*Manager`/`*Service` class, or decomposing an object into primitive parameters — stop. Re-anchor: behavior lives on the owner, the collection is the API, names are single and honest.

**Out-of-scope shape violations** you spot in surrounding code: don't fix inline — log them to `Documentation/Runtime2/obp-cleanup.md` and keep your diff focused.
```

---

## architect — 2026-05-31 — DOCS RESTRUCTURE (not a CLAUDE.md edit)
**Target:** `Documentation/v0.2/good_to_know.md`, `Documentation/v0.2/object_pattern_formal.md`, project `CLAUDE.md` `## OBP Shape Smells`, + a new `Documentation/v0.2/obp-smells.md`.
**Why:** OBP guidance is scattered across three files and partly duplicated, and the richest material is buried in `good_to_know.md` — a 1549-line, ~70-section junk drawer. Pointing the coder character (see the character proposal above) at "good_to_know.md OBP Smell Checklist" points at noise. We need one focused, examples-rich OBP home the character can reference without bloating either the character or the reader.

**Proposed change — two parts. Do PART 1 now (it unblocks the coder character proposal); schedule PART 2 as its own docs pass.**

### PART 1 — OBP slice (do now)

Establish a three-tier OBP structure, each tier with one job:

| Tier | File | Job | Loaded |
|---|---|---|---|
| Quick list | `CLAUDE.md` `## OBP Shape Smells` | numbered smells, terse yes/no checklist | always (every bot) |
| Operational | **NEW `Documentation/v0.2/obp-smells.md`** | worked right/wrong examples, naming tells, "you're drifting" signals, variant design — the *audit* reference | on demand |
| Law | `object_pattern_formal.md` | philosophy + the 9 rules + why | on demand |

Moves:
- Create `obp-smells.md`. Pull the four scattered OBP sections **out of** `good_to_know.md` into it: `## OBP Naming Principle` (~L121), `## OBP Smell Checklist — When a Collection Should Be Its Own Type` + `### Worked example` (~L140/172), `## OBP Variant Design` (~L203), and the OBP parts of `## Source Generator — OBP shape` (~L839) / the "Tells that you're drifting" + "When shape #1 IS the right answer" bits (~L1402/1414).
- Consolidate the *examples* from `CLAUDE.md` `## OBP Shape Smells` into `obp-smells.md`; keep the CLAUDE.md list terse and add a one-line "full treatment + examples → `obp-smells.md`" pointer.
- Leave `object_pattern_formal.md` as the law (unchanged).
- Net: one place for OBP examples, de-duplicated; `good_to_know.md` drops ~250 lines.

Include in `obp-smells.md` the worked **naming** case surfaced on this branch (it's a clean teaching pair): the `app.type.list.@this` registry — `BuildTypeEntries` (a verb proxying a collection; "TypeEntries" restates the class) is the WRONG form; the RIGHT form is the list owning its entries and returning itself, with derived views as their own named members (`...entry.list.catalog`), each owning its shaping — **name matches work**. (See `Documentation/Runtime2/obp-cleanup.md` entry #1, which records both the smell and a subtly-wrong "fix" as a cautionary example.)

**Dependency:** the filed coder-character proposal points at `obp-smells.md`; that pointer only resolves once this part lands. Land PART 1 with (or before) the character change.

### PART 2 — `good_to_know.md` decomposition (follow-up docs pass)

`good_to_know.md` is a junk drawer: ~70 sections that are really ~8 topics. Decompose into topic docs and demote `good_to_know.md` to an **index** that links them. Suggested split:
- `conventions.md` — folder/namespace/`@this`, goal resolution & relative paths
- `test-architecture.md` — test isolation, builder caching, mock module, the test-module invariants
- `data-internals.md` — `As<T>` (cycle/depth/wrap rules), `AsCanonical`, `Variables.Set`, identity, `variable.set` mint site, string-not-iterable
- `wire-serialization.md` — domain-types-on-wire, `[Sensitive]`, transport filters, serializer/ISerializer, multi-segment matching
- `type-system.md` — typed values per-`<name>` renderers, `type` + `kind`, `app.X` collection node, producer-stamping invariant, `type.@this.Null`
- `bans.md` — System.IO ban, Console ban
- `code-modules.md` — `app.module.code` (ILlm/IHttp/IBuilder/ISettings→IConfig/IConfigure)
- `builder-runtime.md` — error reporting, condition orchestration, sub-step gating, Build()-time stamping/BuildWarning
- `obp-smells.md` already carved in PART 1
- source-generator notes can stay near the generator docs

This is a real docs project — keep it separate from PART 1 so the OBP slice isn't blocked on it.
