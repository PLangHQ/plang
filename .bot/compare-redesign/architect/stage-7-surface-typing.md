# Stage 7: Full public-surface typing (the `!` plane) — FINAL, large

**Goal:** Every public property/method on a value type returns the **PLang equivalent**, not raw CLR — completing the `!` plane. Raw CLR survives only at the gated per-type interop inch. A build gate enforces it and makes the conversion converge.
**Scope:** The type system's public surface + the gate. Deliberately **last** — it's the bulk of the diff, and it rides behind the gate so it lands incrementally rather than as one mega-change.
**Deliverables:**
- Convert public CLR-returning members of `item.@this` subtypes to PLang types: `path!absolute` → `path`, `text!length` → `number`, `dict!keys` → `list<text>`, `file!size` → `number`, `list!count` → `number`, predicates like `IsTruthy` → `@bool`, … The `!` accessors a developer can reach all return PLang values.
- The **build gate** (PLNG-style, like the `System.IO` PLNG002 gate): a **public** member of an `item.@this` subtype that returns a raw CLR type is an error. Internal/private C# is untouched. **Warning during this stage, error once the surface is clean.**
- Move truly engine-internal plumbing (`IsLeaf`, normalize dispatch) to `internal` — out of the public-only scope — rather than exempting it.
**Dependencies:** Stages 2–6 (the typed value model + comparison must be in; the `!` plane resolver exists). Standalone after that — converts the surface under the gate.

## Design

**The gate's scope is the line that keeps it sane** (the worry from review: don't police all C#). It fires only on **public members of `item.@this` subtypes** returning raw CLR (`string`/`int`/`long`/`bool`/`byte[]`/`Dictionary`/`List`/…). Out of scope: private/internal members, non-value-type C#, `System.String.Substring` (not a PLang type), a handler's internal string work. So `public string Absolute` on `path` ✗ flagged → must be `public path Absolute`; a `private string` helper inside `path` ✓ fine.

**No carve-out for bool markers.** `IsTruthy` returns `@bool` — the rule applies to predicates too. Truly engine-internal dispatch (`IsLeaf`, normalize) is made `internal` (so the public-only gate never sees it), not exempted. The **only standing exemption** is the gated per-type interop accessor.

**The interop inch — where raw CLR legitimately survives.** A type bridging to a take-over C# API (sqlite, `Assembly.LoadFrom`, a regex lib) needs the raw `string`/`byte[]` at the call. That lives in *that type's own* gated accessor — `path.Absolute` after `Authorize`, exactly the existing `System.IO` discipline (the accessor is the type's, gated, enforced). It is not a generic `ToRaw`, and it is not on the public navigable surface; it's the type handing its own raw to the API at its own edge.

**Convergence (the reason it's last and gated, not a mega-diff).** Stand the gate up as a **warning**, then walk the surface type by type, flipping each member to its PLang return. The gate's warning list *is* the worklist; the thrown framework methods (Stage 2) surface any consumer still expecting raw. When the surface is clean, flip the gate to **error** so regressions fail compilation — same trajectory PLNG002 took. This is the largest chunk of the branch, but each step is local (one member → its PLang type) and the gate tells you what's left.

**Verifications to fold in here** (architect-flagged, run before/while converting): confirm raw-CLR access really is bounded to these leaves (sample handlers — the no-`ToRaw` premise); confirm `number` over a boxed numeric is acceptable. If a handler genuinely needs raw CLR somewhere that isn't a gated interop edge, that's the signal to add a typed method to the type instead.
