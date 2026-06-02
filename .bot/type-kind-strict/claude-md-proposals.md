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

**Second section to add (comment hygiene — separate concern from OBP shape):**

```markdown
## Comments say what the code IS, not what it WAS

A comment describes current behavior and the *current* reason for a choice — never the history of how the code got here, and never a reference to the task that produced it. The past lives in git; narrating it in the source is noise to the next reader (and to you, six months on).

Don't write:
- **History:** "renamed from `Foo`", "moved from `app/Memory/`", "was nullable before the merge", "pre-`filesystem→path`".
- **Task / review / branch references:** "fixes codeanalyzer v2 #1", "per the auditor F3 finding", "stage 7 deliverable", branch names, dates, ticket numbers.

Both are meaningless to a reader without that context and stale the moment the task is forgotten. If a line genuinely needs a *why*, state it as a present fact about the code ("nullable so an unset verb is omitted from the wire"), not the story of who asked for it or what it used to be. Provenance is `git blame`'s job, not the comment's.
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

---

## architect — 2026-05-31
**Target:** `characters/codeanalyzer/character.md`
**Why:** The doc restructure moved the OBP checklist/examples to `obp-smells.md`, so codeanalyzer's Pass 1 pointer is now stale (required fix). Two further additions tie codeanalyzer to this session's work: the `obp-cleanup.md` collection (so it doesn't re-raise parked violations as new) and the comment-hygiene rule just given to coder (codeanalyzer is the natural enforcer). Filed at Ingi's explicit request (architect proposing for a reviewer bot).

**Proposed change 1 — REQUIRED (stale pointer).** In Pass 1a (line ~15), replace the source parenthetical:

> (project `CLAUDE.md` "OBP Shape Smells"; full checklist in `Documentation/v0.2/good_to_know.md`; formal treatment in `Documentation/v0.2/object_pattern_formal.md`)

with the current three-tier:

> (terse list — project `CLAUDE.md` `## OBP Shape Smells`; worked examples + naming tells — `Documentation/v0.2/obp-smells.md`; formal pattern + the 9 rules — `Documentation/v0.2/object_pattern_formal.md`)

**Proposed change 2 — RECOMMENDED (don't re-raise parked violations).** Add to Pass 1, after 1b:

```markdown
**1c. Check the parked list.** Before reporting an OBP shape finding, read `Documentation/Runtime2/obp-cleanup.md`. If the violation is already a parked entry there, don't re-raise it as a new finding — note "tracked: obp-cleanup #N" so the report stays signal. New *systemic* shape violations (not local to the diff) belong on that list; flag them and say so, but leave the write to architect/docs.
```

**Proposed change 3 — RECOMMENDED (comment hygiene; pairs with the coder change above).** Add to Pass 2 or Pass 3:

```markdown
- **Comments that narrate history or reference tasks.** Flag any comment that says "renamed from / moved from / was X / pre-merge", or references a task/review/branch ("fixes codeanalyzer v2 #1", "per auditor F3", "stage 7", branch names, dates, tickets). The code states what IS; provenance is `git blame`. A *why* is fine as a present fact, not as the story of who asked for it.
```

---

## architect — 2026-05-31
**Target:** `characters/coder/character.md` (the App-tree "Before summarizing a problem" bullet, ~line 17)
**Why:** The example exists to teach "use the *real* names from the tree" — but its own names are stale, which undercuts the lesson. `app.Modules` is plural (rename → `app.Module`, confirmed `app-tree.md` L249); `app.oi.serializers.parsers` is not a real path at all (no `app.oi`, no `parsers` folder — serializers live at `app.channel.serializer`).
**Proposed change:** replace

> your summary names `app.Modules.file.read`, the parser path under `app.oi.serializers.parsers`, the `Data.@this` return shape

with

> your summary names the `file.read` action (`app.Module["file"]`), the serializer under `app.channel.serializer`, the `data.@this` return shape

---

## architect — 2026-05-31
**Target:** `characters/docs/character.md`
**Why:** The comment-hygiene rule ("state what is, not what was; no task/review references") was added to `coder` (the writer) and `codeanalyzer` (the enforcer), but `docs` writes the most XML doc comments + architecture markdown and has none of it — the biggest gap. One carve-out: `docs` also writes CHANGELOG, where history is the whole point.
**Proposed change:** add a section (near "### XML Doc Comments" / "You write documentation directly"):

```markdown
## Docs and comments state what IS

Doc bodies and XML doc comments describe the current system and the current reason for a choice — not how it got here, and not the task that produced it. No "renamed from / moved from / was X / pre-merge"; no task/review/branch references ("per auditor F3", "stage 7", branch names, dates, tickets). The reader needs the present fact; provenance lives in `git`. A *why* is fine stated as a present fact about the code, never as the story of who asked for it.

The one exception is the **CHANGELOG** — recording what changed and when is its entire job. Everywhere else (API docs, architecture markdown, XML comments) states current truth only.
```

## builder — v1 — 2026-05-31
**Target:** characters/builder/character.md
**Why:** Building the builder (PLang's self-hosted bootstrap) is the single most central procedure to the builder role, yet the character file does not contain the cwd + ordered-file-list recipe. In this session I built from the wrong cwd (`os/system/` instead of `os/`) and swept the whole system tree instead of the builder's 8 goal files, then had to grep `Documentation/v0.2/building-the-builder.md` to find the correct invocation. That doc must be referenced AND its cardinal rules inlined into the character so the builder never improvises this. Adds a new "Building the Builder (bootstrap)" section.
**Proposed change:**

````markdown
## Building the Builder (bootstrap) — READ BEFORE ANY SELF-REBUILD

Rebuilding `os/system/builder/` is PLang building PLang. Get it wrong and the
builder either won't run or silently produces broken `.pr` files that look like
LLM hallucinations. Full doc: `Documentation/v0.2/building-the-builder.md`
(read it when touching builder prompts/validator). The non-negotiable recipe:

### The recipe
```bash
cd os/        # NOT os/system/, NOT os/system/builder/, NOT repo root — ONLY os/
plang '--build={"files":["Build.goal","BuildGoal.goal","BuildGoal/Start.goal","BuildGoal/Plan.goal","BuildGoal/Validate.goal","BuildGoal/LlmFixer.goal","BuildStep/Start.goal","BuildStep/Validate.goal"]}' build
```

Two things are load-bearing and non-obvious:

1. **cwd = `os/`.** Builder `.pr` files stamp paths like
   `/system/builder/.build/buildgoal.pr` that resolve relative to cwd. Only
   `cd os/` lands them on the real files. `os/.build/app.pr` is the app-root
   marker (`name: "os"`). Wrong cwd → `File not found: /.build/buildgoal.pr` or
   short-form path stamps that break the next run. **Never** rebuild the builder
   by running `plang build` from `os/system/` — that sweeps the whole system
   tree (every goal, incl. non-builder goals like `Run.goal`) instead of the
   builder's 8 files, and produces misleading failures from unrelated goals.

2. **File order = call chain, outer→inner** (entry first, leaves last). The
   running app uses the *previous* in-memory pipeline during rebuild; if
   `BuildGoal`'s `.pr` is rewritten before its deps are stable, later goals pick
   up a half-updated pipeline. Order: `Build.goal` → `BuildGoal/Start` →
   `BuildGoal/Plan` → `BuildGoal/Validate` → `BuildGoal/LlmFixer` →
   `BuildStep/Start` → `BuildStep/Validate`.
   Wrong-order symptom: every goal logs `Validation failed: StepResults or Goal
   is null — retrying...`, LlmFixer fires, build saves with empty-action
   regressions.

### Cardinal rule
**Never hand-edit a builder `.pr` after a self-rebuild produced an error.**
Surface the error loudly; fix it in the prompt (`os/system/builder/llm/*.llm`),
the validator (`PLang/app/module/builder/code/Default.cs:Validate`), or the
action/type teaching — **never** in the `.pr` (it's downstream of the builder).
If a self-rebuild errors, `git checkout --` the dirtied `.pr` files; do not keep
them.

### Pre-flight + verify
- Before: confirm `os/system/builder/.build/` is committed & clean (rollback
  point). Audit existing builder `.pr` path stamps
  (`path` = `/system/builder/<Goal>.goal`,
  `prPath` = `/system/builder/.build/<goal>.pr`); anything short means a prior
  wrong-cwd run — fix stamps first.
- After: `cd Tests/Simple && plang build` should report `Saved` with
  `Kept prior mapping for step N` for every step (no LLM re-call). If it fails,
  the rebuilt builder has a regression — revert before pushing.

### Scope note
`os/system/Run.goal` and other `os/system/*.goal` files are NOT the builder. The
builder is `os/system/builder/**` only. A failure in a non-builder goal during a
whole-tree sweep is not a builder problem.
````

---

## architect — 2026-06-01
**Target:** `CLAUDE.md` (project) — the `## OBP Shape Smells` / module-markdown area, as a new sub-point under "Action prose lives in markdown, not attributes."
**Why:** The coder (authoring prose) and the builder (validating the catalog) both write and tune `os/system/modules/<module>/*.md`. There's no rule against copying a closed set's members into that prose, and they do. Concrete: `os/system/modules/event/on.description.md` says "Event types include BeforeGoal, AfterGoal, BeforeStep, ..." and lists 11 — but `app/event/Trigger.cs` defines 21 (`BeforeAppStart`, `OnError`, `OnCacheHit`, `On*GoalLoad`, ... are all missing). The list was wrong the day a member was added. It's also redundant: the catalog already derives enum members live from the enum (`app.type.GetValidValues` → `type.GetEnumNames()`) and prints them once in the Type Information block — `PLang/app/module/this.cs:336` explicitly removed per-parameter inlining for exactly this reason. So a prose copy buys nothing and rots. This is the doc-level form of OBP smell #3 (same thing stored twice) — the enum owns its members; any md that re-lists them is a second source of truth that silently drifts. One shared home rather than a copy in each of `coder` and `builder` character.md — duplicating the rule across two consumers is the very thing it warns against.

**Proposed change:** append this sub-point to the "Action prose lives in markdown, not attributes." bullet:

```markdown
**Closed sets stay in the catalog, never the prose.** Do not enumerate the members of a closed set in the `.md` teaching files — enum values, valid-value lists, type-kind vocabularies, MIME maps. The catalog injects them live from the source of truth (`app.type.GetValidValues` → the enum's own members) into the Type Information block; per-parameter inlining was removed (`PLang/app/module/this.cs:336`). A prose copy duplicates what the LLM already gets and goes stale the moment a member is added (today `event/on.description.md` lists 11 of `Trigger`'s 21). Prose names the type (`trigger`, `operator`) and teaches what the catalog can't: shape and behavior — how the value is emitted (a bare member string, not a wrapped object), and what it must not be confused with. At most one representative member as illustration, never framed as "the valid set is X, Y, Z." Grep tell: a `.md` listing three or more `PascalCase` members of one type, or a comma-run that mirrors an enum body.
```

---

## architect — 2026-06-01 — plan must trace the incumbent (leaf trace)
**Target:** `characters/architect/character.md`
**Why:** `type-kind-strict` shipped the type *model* (`{name, kind, strict}`), the consumer, the producers, the LLM surface — all per plan — but the plan never designed **how a type builds a value of itself from raw input**. It designed only how a type *validates* its kind (`IKindValidatable`), and silently assumed value *construction* (string→int, dict→record) rode the same seam. It doesn't: construction is a separate job whose real home is `TryConvertTo` (`type/list/Conversion.cs`, ~400-line target-type switch). Evidence of the half-migration that resulted: the incumbent `TryConvertTo`/`ConvertTo` has ~15 call sites including `Data.As<T>` (the path every lazy parameter funnels through); the new type-owned `type.Convert` has 2, and falls straight back to the switch for every type but `text`. A divergent duplicate (`channel/serializer/Text.cs` `ConvertFromString`/`IsSimpleType`) carries a live locale bug. All of this was found *in usage* and then in an ObpScan sweep (parked as obp-cleanup #5) — never at plan time. Root cause: "Play the movie" (MANDATORY step 4) traces the *new feature's forward path* (developer → builder → .pr → runtime → handler) but never asks where the behavior already lives. A plan that gives a new owner to an existing behavior is not finished until it accounts for the behavior's old home — and top-down feature decomposition (model → consumer → producer) structurally cannot find that home, because it's orthogonal infrastructure sitting under all the new nouns.

**Proposed change — two coordinated edits.**

**Edit 1 — append a paragraph to the MANDATORY "Play the movie" step (step 4 of the numbered list):**

```markdown
   **Then trace the incumbent — backwards.** The movie above traces where the *new* design flows. Run it a second time the other direction: when the design gives a new owner to a behavior that already exists somewhere — value conversion, validation, serialization, routing, caching, parsing — find where that job is done *today*, before you plan the new home. Grep for the incumbent (`== typeof(`, `value is <type>`, the existing method name, `Convert`/`TryParse`-shaped code) and find *all* of them, including divergent duplicates. Then the plan must state, per call site and per branch: **migrated / kept as the primitive leaf / deleted.** "Each type grows its own X over time" is a non-decision — it ships half-migrated and the gap surfaces in usage, not review. And split the two jobs that masquerade as one: how a type *validates* itself and how a type *constructs a value of itself from raw input* are different seams — design both, not just the one the new nouns happen to surface.
```

**Edit 2 — add a bullet to `plan.md`'s "Contains:" list in the "Two-layer plan" section (Layer 1 — the spine):**

```markdown
- A **leaf trace** whenever the plan gives a new owner to existing behavior: name the incumbent code (file + method), list its call sites, and state each one's disposition — migrate / keep as the leaf / delete. If the disposition is incremental, name which cases land *this branch* and file the rest as an explicit parked backlog before coding starts.
```


---

## builder — v1 — 2026-06-01
**Target:** `characters/builder/character.md` — the `## Reference Docs` list ("Read these before working on anything builder-related").
**Why:** This session showed that without a held mental model of how the builder works (two-phase Plan→Compile, the bootstrap that runs from its own `.pr`, the catalog from `app.Module.Describe()`, the single OpenAi-layer LLM cache, and which recovery loop fires for which failure), I burned several turns rediscovering it the hard way — e.g. not realizing a `.goal` edit doesn't affect the running build, or inventing a non-existent "keep-prior-mapping" cache separate from the LLM cache. That conceptual model now lives in one place: `Documentation/v0.2/understanding-the-builder.md` (written and verified against the source + the creator's corrections this session). The existing Reference Docs list points at `build.md`/`debug.md`/`building_plang_tests.md` (the *how-to* references) but nothing gives the *why/what* orientation. Loading the orientation doc at session start is the cheapest way to avoid re-deriving it every time. Belongs in the builder character (loads for builder sessions), not the repo CLAUDE.md, since it's role-specific reading.

**Proposed change:** add this bullet to the `## Reference Docs` list, as the first entry (read it before the how-to refs):

```markdown
- `Documentation/v0.2/understanding-the-builder.md` — **read this first.** The conceptual model: two-phase Plan→Compile, the bootstrap (the builder runs from its own `.pr`, so `.goal` edits aren't live until rebuilt), the catalog from `app.Module.Describe()`, the single LLM cache at the OpenAi layer (`cache:false` to bypass), and the recovery loops (`LlmFixer`/`RefineActions`/`FixValidation`/`HandleStepFailure`). Orientation; the docs below are the how-to references.
```

---

## coder — v10 — 2026-06-02
**Target:** /CLAUDE.md
**Why:** Stage 10 made "a type owns how a value of itself is built" a load-bearing convention with a concrete shape (per-type `Convert` hook + a routing table + two doors + a residual leaf). Future type work must extend the hook, not the converter — without a stated rule the next bot will reach back into the central switch (the exact regression this stage removed).
**Proposed change:** Add under `## Runtime2 Conventions`:

```
- **Type value-construction is type-owned (`Convert` hooks).** A type owns how a value of
  itself is built from raw input via a `public static data.@this Convert(object? value,
  string? kind, context)` hook on its family class (`number`, `datetime`, `duration`, `path`,
  `image`, `GoalCall`, …), discovered by `app.type.convert.@this`. Output is the CLR value the
  alias targets (e.g. `number.Convert` returns a CLR `int`/`decimal`, not `number.@this`); a
  hook returns `null` to *decline* a value shape (falls through), an Error `data.@this` to fail
  authoritatively. The central converter `app.type.list.@this` holds **no per-type arms** — it
  is the dispatcher (`internal TryConvert`) + a residual primitive leaf (bool/guid/enum/
  `ChangeType`/string-ctor/json-structural) only. Two public doors share it: **primary**
  `type.@this.Convert(value, ctx)` (a PLang type entity in hand, carries Kind) and **infra**
  `App.Type.Convert(value, clrTarget, ctx, slot)` (only a CLR target in hand). `convert.@this.
  OwnerOf(clrTarget)` is the App-less routing table (CLR primitive → family + number-kind; path
  subclass → path; any type declaring a hook → itself). Adding a type = add a `Convert` hook +
  (if it constructs a raw CLR primitive) an `OwnerOf` row — never edit a target switch. String→T
  is invariant-culture; a channel serializer that needs a locale/empty/utf8 nuance keeps it
  serializer-local, it does NOT belong in the converter.
```
