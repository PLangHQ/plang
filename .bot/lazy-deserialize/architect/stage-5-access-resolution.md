# Stage 5: Access-driven resolution — the access pattern decides, no guessing

> **Note for coder:** every rule below is a **suggestion** that captures architect intent — not a contract. You own the implementation. Push back if a rule reads wrong.

**Goal:** The *kind of access* decides how a lazy `Data` materializes. Scalar access decodes utf-8; navigation materializes through the known type's reader; `as <type>` reads toward that type; a property read never touches the value. When the type is unknown, a structured access errors clearly and asks for a cast — **nothing sniffs content**.

**Scope:**
- The access paths on `Data` — scalar/output read, navigation (`app/data/this.Navigation.cs`), `as <type>` / `As<T>`, property read (`!`).
- The type-unknown structured-access error.

**Dependencies:** Stage 4 (the boundary produces the lazy, type-unknown-or-typed Data these rules resolve). Stages 1–3 underneath.

**Out of scope:**
- Content sniffing of any kind — explicitly *not* built (see Design).
- Render-side kind-awareness (csv→table) — the *write* mirror of this work, a separate branch. Tracked in `Documentation/Runtime2/todos.md` (2026-06-03). This stage is read-side.

**Deliverables:**

1. **Scalar / output** (`%x%`, `write out %x%`) → if `_raw` is bytes, decode utf-8 (and stay bytes if it doesn't decode); if text, the string. No structured parse.
2. **Navigation** (`%x.field%`) → materialize through the known type's reader; `kind` says how (`(object, json)` parses json, then navigate). The **type's shape decides the navigation model**: an `object` navigates by key (`%cfg.port%`), a `table` by row/column (`%t.rows`, `%t[0].name%` — coder owns the exact surface). If the type is **unknown** → clear error: `"value has no type; add as <type>"`.
3. **`as <type>`** → read toward that type (the explicit override; the materialization path for type-unknown bytes).
4. **Property** (`%x!prop%`) → read from `Data.Properties`; never touches the value. A status check (`%response!status%`) does not materialize the body.
5. **No sniffing.** Type-unknown bytes touched as structured do not get guessed (json vs xml vs yaml vs csv) — they error per rule 2.

## Design

**Why no sniffing.** A guess at the format from raw bytes (`{`→json, `<`→xml, commas→csv) is exactly the "magic" PLang's determinism forbids, and it contradicts the branch's own thesis: "the type reads itself." When the type is unknown, *nothing* reads it — so we error and ask for `as <type>` rather than guess and silently misparse. The deterministic halves of access-driven resolution (utf-8 decode on scalar, materialize on navigation of a *known* type, `as` cast) all stay; only the guess is cut.

**The resolution table.**

| Access | Lazy Data state | What happens |
|---|---|---|
| `%x%` / output | any | utf-8 decode if bytes (else stay bytes); the string if text. No parse. |
| `%x.field%` | type known | reader for `(type, kind)` materializes, then navigate |
| `%x.field%` | type unknown | error: "value has no type; add `as <type>`" |
| `%x as json%` / `As<T>` | any | read toward that type/kind |
| `%x!prop%` | any | read from `Properties`; value untouched |

**Property reads never materialize.** This is what makes `if %response!status% == 200` cheap — the status is a property, read with `!`, and the body (the value) stays raw and untouched. Property access and value access are different doors; `!` is the property door.

**This stage is small and lands last.** Stages 1–4 deliver the whole read-side win — the symmetric reader, exact numbers, lazy Data, one boundary — without any access-pattern cleverness. Stage 5 is the thin layer that routes a touch to the right materialization and draws the line at guessing. It rides on a proven 1–4.
