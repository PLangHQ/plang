# OBP scan

The checklist + procedure for spotting Object-Based-Pattern violations in changed C#.

## Procedure — when asked to "obpscan"

1. Find the range: everything changed since the **last scanned commit** (recorded at the bottom of
   this file). `git diff <last-scanned>..HEAD --stat` for the file list, then read the diffs.
2. Walk every changed/added `.cs` against the checklist below — **forks first** (the loud alarm).
3. Report findings grouped by severity: real violations (fix), borderline (note), clean.
4. After the scan, update the **Last scanned** marker at the bottom to the current `HEAD`.

The point is to catch shape drift *as it lands*, not at the end of a stage — especially API surface
(new classes/methods) that wasn't in the plan.

## 1. Forks (first pass — the loud alarm)
- **Any `if`/`switch` that is a *behavioral* fork** — two execution paths for the *same* operation
  (`switch`=`if` smell). Reading distinct fields of a structured object is not a fork; choosing *how
  to do the same thing* is.
- **Generic / fallback / "default" path** beside per-type handlers — a second execution path + a
  fork. Every type gets its own tiny uniform file instead.
- **Type-switch inside a registry/dispatcher** (`is X.subtype` → behave differently) — behavior
  misplaced; push it onto the element as a virtual member.
- **Optional-override branch** (`is INamedThing ? declared : derived`) — two ways to get one thing.

## 2. Unplanned API surface (new — be on the lookout)
- **A new class or method that was NOT in the plan.** Every new type/member is a design decision;
  if the plan didn't call for it, ask *why it exists* before accepting it:
  - Was it born from *usage* ("we currently call it this way") rather than *domain shape*? → collapse it.
  - Is it a helper/utility that should be a member on an owning type? → move it.
  - Is it a second way to do something that already exists? → it's a fork; unify.
  - Does it widen API surface a caller will couple to (a public getter, a new interface) that the
    plan didn't intend? → flag it.
  - A new `*__Generated`/adapter/wrapper type to bypass a `[JsonIgnore]`/access problem? → that's the
    parallel-type smell; fix the owner instead.

## 3. Names (design-time + write-time)
- **Verb+Noun / Noun+Verb**: `GetParameters`, `BuildTypeEntries`, `ConvertToIdentity`,
  `ErrorCategory`, `ParseNextSegment`, `FromWireShape`. Use an honest noun, or a verb-on-the-owner.

## 4. The shape smells (CLAUDE.md #1–8)
1. **Public mutable collection, rules enforced from outside** — `public List<T>` whose
   `Add`/lock/eviction live in another file.
2. **Cross-file lock target** — `lock(other.X)` from outside `other`.
3. **Same logical thing stored twice** across types (overlapping names/role/element type).
4. **Allocate-here / mutate-there / clean-up-elsewhere** — one collection's lifecycle across three files.
5. **Producer hands back raw; consumers transform identically** — `obj.Path + "/"`, `.TrimStart('/')`,
   `.ToLower()` at N sites. The discipline belongs on the owner.
6. **Holds a reference AND a flat copy** of fields reachable through it (`Foo Foo` + scalar mirrors of
   `Foo.X/Y/Z` that drift).
7. **Courier reaches into `Data.Value`** — a relay (handler forwarding Data, callstack, channel,
   signing, wire) does `.Value is/as X`. Only leaves touch `.Value`.
8. **Decompose a value into parameters** — `Op(a.Value, b.Value)` / `await X.Value()` an operand just
   to hand the raw inside to a helper. Pass whole carriers: `await A.Add(B)`.

## 5. Value / type discipline
- **Lowering to CLR (`.Clr`)** anywhere except the .NET / 3rd-party / sqlite / STJ boundary — work in
  plang types end to end; lift via `await data.Value<T>()`, never `.Clr`-and-re-lift. High `.Clr`
  density = a CLR-centric design that's wrong.
- **Construct-then-stamp** (`new X(...) { Context = ... }`) instead of born-with-context; a
  context-less value mis-types.
- **Operation in a utility/free helper instead of on the owning type** — `Text.FromText<T>`,
  parsing a `.pr` path in `goal.list` → belongs on `Number`, `path`, etc.

## 6. The meta-test
- **"If removing one line of choreography needs edits in three files, those three files are one
  missing type."**
- **Coincidental duplication** that *vanishes when the shape is right* — don't extract it; fix the
  shape first.

## Quick triage order
forks → unplanned API surface → names → "same thing twice?" → "non-leaf touches/decomposes `.Value`?"
→ "`.Clr` at a real boundary or a CLR-centric smell?"

---

**Last scanned:** `a5f40f053` (read-path-unification, Stage 3 done — 2026-06-29). Update after each scan.
