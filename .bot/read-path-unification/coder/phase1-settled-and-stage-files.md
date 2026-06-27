# Read-path unification — Phase 1 settled + stage-file request

**Branch:** `read-path-unification`. Follows `response-to-architect-v1.md` (corrections C1–C3 + Q4–Q7, all folded into `../architect/v1/plan.md`). This file records the design points Ingi settled in the last round, plus what I'd like the architect to do about per-stage files.

---

## Settled with Ingi — the generic default reader + consistency

### 1. The generic default reader is thin — string-scalar only

- It handles **only string-raw scalars** (`number`, `bool`, `guid`, `date`, `datetime`, `time`, `url`, `text`, `primitive`). One delegation: `source.DeclaredType.Convert(raw)` (the per-type hook, `type/this.cs:573`). **Zero type-branching.**
- **Rule (Ingi):** if the generic reader starts wanting `if (type == …)` branches, that is the signal to **split a specific reader file** instead. It stays genuinely thin or it stops being the generic reader. We do not grow a god-reader full of `if`s.

### 2. `byte[]` never reaches the generic reader — it is the `binary` family

- A raw `byte[]` is born **`binary`**: `type = binary`, `kind = mimetype / .extension` (Ingi).
- Handled by the **`binary`** specific reader (and `image` / `table` / `archive` / `file` / `directory` specific readers when the kind names one).
- Decode-to-text stays the **explicit `as text`**, consistent with the existing `source` doc — bytes don't silently become text on read.
- So the split is by **raw shape**: string-raw → generic reader; byte-raw → binary-family specific readers. No overlap, no byte→string normalization branch inside the generic reader.

### 3. Consistency — `source.Value` collapses to one line

Today `source.Value` carries **three mechanisms that behave differently** — `Of` (reader), `Convert` (string), direct `new binary` (bytes), plus a second `Of` for kind-narrowing. Under the total registry they unify into one selection:

```
source.Value(data):
    (item, err) = app.type.Create(this)        // = App.Type.Reader(this).Read(this)
    if (err != null) { data.Fail(err); return Absent; }
    return item
```

- `Of` → registry selection
- `Convert` → the generic reader (selection lands there when no specific reader)
- direct binary → the `binary` reader (selection by `type = binary`)
- kind-narrowing (`json→item`, `jpg→image`) → **inside `App.Type.Reader`'s lookup**, NOT a `source` branch

No `if string` / `if byte[]` / `if kind` survives in `source`. Everything is "select reader by `(type, kind)`, `Read(source)`." **That is the consistency** Ingi asked for.

### 4. What I verify in Phase 1 before deleting anything

That every `(type, kind)` reachable today via `Of` / `Convert` / direct-binary maps to **exactly one** reader (specific or generic) — so the collapse loses no case. Trace the current call paths against the reader-coverage table first; delete `Readers.Of` only after the map is proven total.

---

## Stage files — what I'd like the architect to do

I'd find per-stage files useful, **as thin execution checklists, not a re-statement of the design.** To avoid two sources of truth:

- **`plan.md` stays the single design authority.** Stage files reference it, never re-derive it.
- **Each `StageX.md` is the execution layer:** entry criteria (what's green before), exit criteria (what's green after), the `dies`/`stays` list **re-verified against current line numbers** (the plan warns they drift), and a "what actually shipped + deltas from plan" section the coder fills in.
- **Seed them lightweight, and do NOT fully spec Stages 2–6 upfront.** Each later stage's exact shape depends on what the prior stage actually lands (line drift, whether kind-narrowing fell cleanly into the registry, etc.). Firm up `StageX+1.md` only once `StageX` is green and pushed.

**Concrete ask of the architect:** seed one `StageX.md` per phase with just the entry/exit + the `dies`/`stays` pulled from the phase he already wrote in `plan.md` — nothing more. The coder firms up and logs each stage as it lands.

(Alternative, if the architect would rather not: the coder writes the StageX files while working each phase — same outcome, one less round-trip. Ingi to pick.)

---

## Status

Phase 1 design is settled. Awaiting Ingi's **go** to start cutting code (Phase 1 first, green + pushed before Phase 2), and his decision on who seeds the stage files.
