# CLAUDE.md proposals — navigation-driven-record-builder

## architect — v1 — 2026-07-09 — **APPLIED DIRECTLY** (Ingi instructed in-session: "update your claude.md as you would like to have it"; kept here as the record/rationale)

**Target:** `/workspace/plang/CLAUDE.md` (repo root)

**Why:** Ingi and architect went over the full OBP doc set (2026-07-09 session) and settled several rulings: smells are **named, not numbered** (LLMs work better with text; numbers desynced between CLAUDE.md, `obp-smells.md`, and `obp-scan.md`); the pattern doc now separates the 3 unbreakable laws from the breakable rules and is plang-agnostic; new naming rulings (caller-intent verbs, `IsX`/`HasX` the only sanctioned compound, verb+noun never); collections are `X.list` types exposed as singular properties. `Documentation/v0.2/{object_pattern_formal,obp-smells,obp-scan}.md` are rewritten accordingly on this branch. Two CLAUDE.md sections now disagree with them: the numbered `## OBP Shape Smells` list, and a Key Files pointer at a file that no longer exists.

**Proposed change 1 — replace the entire `## OBP Shape Smells` section body** (keep the heading) with:

````markdown
When reading or writing C#, check the diff against the named smells. A hit means the shape is wrong and the fix is structural, not a line edit. Names are canonical; worked examples and grep tells live in `Documentation/v0.2/obp-smells.md`.

**Shape:**
- **naked collection** — bare `List<T>`/`Dictionary` as public state; its add/lock/evict discipline lives in other files. Fix: own `X.list` type (private backing, own `Add`), exposed as a singular property (`callStack.Error`).
- **middleman** — a parent proxying what it owns (`AddError(...)` wrapping `Error.Add(...)`). Expose the node.
- **cross-file lock** — `lock (other.X)` from outside the owner.
- **stored twice** — the same logical thing held in two types with overlapping meaning.
- **split lifecycle** — allocate here, mutate there, clean up elsewhere.
- **flat copy** — holds a reference AND scalar mirrors of properties reachable through it; the mirrors drift.
- **raw hand-off** — producer returns raw, every consumer applies the same transform (`.TrimStart('/')` at N sites). The discipline belongs on the owner.
- **stray helper** — `Helper.X(thing)` that should be `thing.X()`; every private static helper is a suspect.

**Value layer:**
- **broken seal** — a courier (non-leaf) reads `Data.Value` mid-flight. Only leaves open the package.
- **opened box** — a leaf cracks carriers into primitives for a helper. If you opened the box to pass what was inside, pass the box.
- **clr leak** — `.Clr` lowering anywhere but a real .NET/3rd-party/storage boundary.
- **late stamp** — construct-then-stamp; born without the context it needs to reach out. Context is a private non-nullable field set at construction.

**Design alarms:**
- **fork** — two execution paths for one operation: behavioral `if`/`switch`, generic fallback beside per-type handlers, type-switch in a registry, optional-override branch.
- **verb+noun** — any compound name where one half is a verb (`BuildTypeEntries`, `CoerceToKind`); only `IsX`/`HasX` booleans are exempt. Never allowed — always diagnostic of an object doing another object's job.

Meta-test: if removing one line of choreography requires editing three files, those three files are one missing type.

Full catalog with worked examples: `Documentation/v0.2/obp-smells.md`. The pattern (3 laws + rules): `Documentation/v0.2/object_pattern_formal.md`. Audit procedure: `Documentation/v0.2/obp-scan.md`.
````

**Proposed change 2 — Key Files section**, replace the stale line:

```markdown
- For full OBP details: `Documentation/Runtime2/plang_object_based_pattern.md`
```

with:

```markdown
- For full OBP details: `Documentation/v0.2/object_pattern_formal.md` (3 laws + rules), `Documentation/v0.2/obp-smells.md` (named smell catalog)
```

(`Documentation/Runtime2/plang_object_based_pattern.md` no longer exists.)
