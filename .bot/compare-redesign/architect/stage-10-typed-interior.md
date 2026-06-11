# Stage 10: The runtime goes typed interior (mapped 2026-06-11 — runs after Stage 9 closes)

**Why:** Stage 9 makes the VALUE PATH typed — Data holds an instance, the doors hand out items, consumers stop decomposing. But a value's life doesn't end at the consumer: it lands in runtime classes — a tag bag, an error's message, a step's text, a channel's mime — and today those interiors are raw CLR (`string`/`int`/`bool` properties and fields). Every landing is a forced decompose: `Tag(number)` storing `int` defeats the typed flow one hop after it was won (Ingi, 2026-06-11). The endpoint of the model is that **plang values stay plang-typed for their whole life inside our process** — entry lift once, exit lowering once, typed everywhere between, including at rest inside runtime classes.

**Goal:** every runtime class's value-carrying members — properties, fields, method returns, parameters — are plang-typed. **The presumption is typed; staying CLR is the argued exception** (true engine plumbing that never touches value-flow: a loop counter, a lock object, a Roslyn handle). Stage 7's worked example (`channel.Mime` → `text`, the registry keyed on text) is the per-member playbook; this stage runs it across the runtime.

**Opening inventory (2026-06-11):** 291 raw-CLR public properties outside `app/type`, by area: module 98, goal 45, error 32, data 25, channel 22, Attributes 10, tester 9, event 9, mock 6, callstack 6, actor 6, variable 5 (+ smaller). Fields, internals, returns and params are on top of that — the gate (below) enumerates them as it widens.

**Scope:** the runtime's class interiors, area by area. **Out of scope:** `app/type/**` (already governed by PLNG003 + the stage-9 pins), the source generator's emitted shapes (own design, `Data<T>` slots already typed), third-party adapter interiors (Fluid, sqlite — the edge lowers, their objects are theirs).

## Mechanism — the Stage 7 playbook, widened

1. **Widen the PLNG003-style gate beyond item subtypes**: a value-carrying member of a runtime class with a raw CLR type is a finding. Stand it up as WARNING; the warning list IS the worklist; flip to ERROR per area as each area goes clean. (How "value-carrying" is expressed — opt-in attribute per class, namespace allowlist, or analyzer heuristic + suppression-with-reason — is a design decision for the coder to propose before building; the wrong choice makes the gate either noisy or gameable.)
2. **Walk area by area** in dependency order: error → callstack/channel → goal/step → variable/event/tester → module (the bulk, last — many of its 98 are action-param shapes that may dissolve under the generator's typed slots first).
3. **Per class, a demolition-style member audit**: each member → typed / argued-CLR-exemption (one line why it never touches value-flow) / dies. The audit file rides in the stage folder; exemptions are part of the contract, reviewable.
4. **No half-flips** (the 2c rule, stage-wide): a member flip includes its backing store and its readers — the value stays typed at rest. `missing-typed-ops.md` keeps collecting: every "I needed raw here" is a candidate method on the type.
5. **Wire/.pr round-trip per area**: typed members serialize through their types (domain types ride the wire as property bags; `[Out]` filtering unchanged) — each area's flip ends green on the wire tests, not just compile.

## Dependencies

Stage 9 closed (2b + 2c — the doors hand out items and the visible decompose points are gone, so flipped interiors have typed values arriving). The PLNG003 worklist walk (Stage 7) can interleave; same gate family, different surface.

## You own this

Area order inside the walk, the gate's opt-in mechanism (propose before building), and each member's typed-vs-exempt judgment are the coder's — with exemptions written down, one line each, reviewable. The fixed contract: presumption typed, no half-flips, gate converges warning → error per area, wire tests green per area.
