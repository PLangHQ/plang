# Stage 6 — OBP pass, docs, full test sweep (no code death)

**Goal:** prove the redesign holds the invariants, leaves no OBP smell, and is documented where future devs look. No behavior changes — this is the close-out.

**Depends on:** Stages 1–5 complete and green.

---

## Step 6.1 — OBP scan (the changed surface)

Run the obp-scan discipline over all C# changed since `3ddcdb17f`. Forks first, then unplanned new classes/methods, then names, then the 8 shape smells. Specifically check:

- **Verb+Noun (flashing sign):** the case-2b op name. `type.Convert(item)` is owner-first and honest; reject any `Retype`/`ConvertValue`/`BuildValue`/`MaterializeSource` that crept in. Scan the new reader class names (`Reader` is the convention — fine).
- **Smell #1 (public mutable collection):** the reader registry growth — did construction add a public mutable table beside the existing `_runtimeTyped`/`_generatedTyped`? It must reuse the registry, not add a parallel one.
- **Smell #4 (allocate/convert/clean across files):** confirm the `set` ↔ ctor convert split is *gone*, not moved. The conversion lives in one place (case 2b in the ctor).
- **Smell #7/#8 (courier touches `.Value` / decompose at a leaf):** the ctor's fork is a *shape* question on the value (is it raw / built / which type), not a `.Value` read. The from-raw arm passes the **whole** value to `source(value, type)`. Confirm no `.Value()` decompose-into-primitives crept into the ctor or `Declare`.
- **`.Clr` density:** case 2b's `Convert` unwraps a built leaf via `leaf.Clr<object>()` to feed the family hook — this is the unavoidable re-type cost of a built-but-wrong-type value (a built leaf has no clean raw). It is one `.Clr` at the convert seam, not a CLR-centric design. Confirm no *new* `.Clr` appeared on the from-raw path (that path is pure plang types: raw → `source` → reader → value).

Record the scan result; update the obp-scan "Last scanned" marker to HEAD.

---

## Step 6.2 — invariants, proven

Add/confirm tests (or trace notes) that each invariant holds:

1. **One door:** no construction path reaches a value except through the ctor's fork. Grep for any residual eager `Build`/`Convert(object?)` construction caller — none.
2. **One conversion:** `"5" as number` hits the number reader exactly once and never makes a `text`. Assert with a materialization probe or by asserting the from-raw path produces a `source` whose first `.Value()` is the only parse.
3. **No context fork:** the ctor has no `_context != null ? Build : Judge`. Grep proves it.
4. **Hooks reached two ways:** a from-raw value reaches `number.Convert` via the reader; a built-wrong-type value reaches it via case 2b. Both tested.

---

## Step 6.3 — docs

- **`Documentation/v0.2/good_to_know.md`** — add the construction-door rule: a declared-type value is born via the four-case fork; from-raw defers through `source` + reader (format by type); a built-but-wrong-type value is re-typed via the type's `Convert` hook (case 2b — the one thing that did NOT dissolve). Note the lazy-failure semantics (`MaterializeFailed` at first use) and why case 2b is what makes build-time bad-literal detection sound.
- **`Documentation/v0.2/action-catalog.md`** or the data-spec — if reader coverage / the `(type,kind)` reachable map is referenced, link `stage-1-reachable-set.md`'s outcome.
- **good_to_know** — record the format-by-type rule (scalar→text/plain, container→application/plang, bytes→kind→mime) so the next reader author picks the right format.
- If anything corrected a live-code contradiction in the architect plan, note it (CLAUDE.md "when corrected about architecture, add to good_to_know").

---

## Step 6.4 — full test sweep

- C# suite: `./dev.sh full` (or `dotnet run --project PLang.Tests`).
- PLang suite: clean rebuild (stale-binary trap) → `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.
- Targeted regression set authored across stages, re-run together: scalar from-raw, container from-raw, native hold, case-2b re-type, typed-null, JSON-null, empty-string, `"abc" as number` build failure, bad-enum build failure, `keepAsIs`, `%var%` same-type short-circuit, `%ref%` template, Variable target, kind-validatable deferral.

---

## Exit criteria

- [ ] OBP scan clean (or findings fixed); "Last scanned" marker at HEAD.
- [ ] Four invariants proven (tests/traces).
- [ ] Docs updated (good_to_know construction-door + format-by-type rules).
- [ ] Both suites green from a clean build.
- [ ] Final report in `.bot/value-construction-redesign/coder/v?/report.md`; branch pushed.

## What must NOT happen

- No new behavior in this stage — pure verification + docs.
- Do not skip the clean rebuild before the PLang suite (stale-binary trap produces phantom failures).
