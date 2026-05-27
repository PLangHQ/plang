## 2026-05-27 — Design settled, stages carved, ready for downstream

Worked through the cross-cutting decisions interactively with Ingi via the review server. Five settled rules:

1. **Lazy normalize** — runs at serialize-time only; `data.Value` in-memory stays `object?`.
2. **Bounded cycle detection** — visited-set + max-depth cap; hard error on violation.
3. **`[Out]` is the wire whitelist** (reuse existing `PLang/app/View.cs` attribute, no new `[WireIgnore]`).
4. **Debug mode bypasses `[Out]`** — but `[Sensitive]` and `[Masked]` still apply.
5. **`[Masked]` — new attribute.** Property name on wire, value replaced with `"****"`. Canonical use: `setting.value` — receivers know that `DATABASE_URL` is configured without seeing the secret.

Plus: **`Data.RawSignature` is deleted.** Legacy from when `Signature.get` had a lazy-populate side effect (gone after stage 2a.7). Four callers migrate to `Signature` directly. Folded into Stage 1.

Worked through the 13 in-scope `Data<T>` payload types and tagged every public property in [`plan/wire-out-attributes.md`](plan/wire-out-attributes.md). Headline calls: path on wire is `{Scheme, Relative}` (Absolute skipped — leaks server filesystem layout); Identity is Name + PublicKey only; setting is `{key, value: "****"}` (the masking case that motivated the new attribute).

The IDataNormalizable escape-hatch question that was open across two rounds — answered itself once `[Out]` discipline landed: every domain type reduces to a clean property bag, no type needs to collapse to a non-object wire form. Path round-trips via `path.Resolve(Relative, ctx)` from the As<T> hook.

Stages carved as 4 files at the architect root, linear dependency chain:

| Stage | File | Status |
|-------|------|--------|
| 1 | [stage-1-out-discipline.md](stage-1-out-discipline.md) | pending |
| 2 | [stage-2-normalize-jsonwriter.md](stage-2-normalize-jsonwriter.md) | pending |
| 3 | [stage-3-as-tree-walker.md](stage-3-as-tree-walker.md) | pending |
| 4 | [stage-4-second-format.md](stage-4-second-format.md) | pending |

Test material for test-designer in [`plan/test-strategy.md`](plan/test-strategy.md) (narrative, 4 integration cuts) and [`plan/test-coverage.md`](plan/test-coverage.md) (per-stage behavior matrix, failure matrix, new-surfaces inventory).

**Ownership callout in every handoff doc.** Per Ingi's "each bot owns his code so they are responsible" — every stage and test file opens with a labelled note to the downstream bot that snippets / signatures / file paths shown are suggestions, not contracts. Standing rule saved to memory.

**Next:** `run.ps1 coder data-normalize "Implement Stage 1 of the data-normalize plan" -b data-normalize`

---

## 2026-05-26 — Initial plan: structural normalization (option 3)

Placeholder branch created off `data-serialize-cleanup`. Captures the design conversation that surfaced while working on the cleanup: how does PLang carry arbitrary objects to non-reflection formats (protobuf, MsgPack, CBOR)? Answer is structural normalization — `Data.Value`'s contract narrows to `primitive | byte[] | Data | List<>`, a `Normalize()` step walks any C# object into that uniform tree once at the boundary, and per-format encoders become trivial walkers (no reflection).

The plan captures the Value contract, the Normalize sketch, the bare-when-possible wire shape rule (primitives ride bare in their parent's value slot when the parent's `type` describes them), the As<T> reverse direction (tree-walker, not STJ-deserializer), and a rough five-stage outline.

**Not started yet.** Depends on `data-serialize-cleanup` merging first. Stages will be carved when work begins; the spine and plan are placeholders so the design conversation isn't lost.

Stage status: pending stage carve-out when work begins.
