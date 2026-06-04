# RFC: a deterministic, observable, self-verifying build

**Status:** proposal for architect review · **Author:** builder bot · **Origin:** a long
session hardening the builder's self-build (`os/system/builder`). Most of that session was
*not* spent fixing the builder — it was spent **compensating for a build that is opaque,
non-deterministic, and unverifiable**. This RFC proposes making those problems
unrepresentable rather than accumulating more coping rules.

It is a vision, not a spec — sequencing and mechanism are the architect's call. The "Suggested
mechanism" notes (events, interception) are starting points, not decisions.

---

## The principle

> The LLM should do the **minimum creative act** — recall intent and commit to a chain.
> Everything deterministic — structure, validation, replay, attribution — belongs to the
> **runtime**.

Today the split is inverted: the LLM is asked to emit flawless deeply-nested JSON (a
deterministic burden), while the runtime gives almost no determinism or observability back.
Flip it.

## What it cost us (grounded)

Every item in my hard-won "diagnostic discipline" is a symptom of a missing runtime capability:

| Coping rule I learned | Missing capability it compensates for |
|---|---|
| "Run N times, a single green build is misleading" | builds aren't deterministic/replayable |
| "Suspect cache/state/binary before the model" | failures aren't attributed to a layer |
| "Audit the oracle `.pr` step-by-step by hand" | no build-vs-known-good diff |
| "`awk` response blocks out of 157k-line logs" | no structured per-step explain |
| "A 104/104 step-match with buildOk=false is a false pass" | success isn't a first-class signal |

(The recently-fixed `cache:false` bypass is the proof case: a single stale cached response
silently failed every build and *looked* like an 80%-flaky model. A deterministic, replayable
build would have made that a one-line diagnosis.)

---

## Capability 1 — the build is a pure, replayable function (a *build journal*)

Every build writes a **journal**: per step, the exact `(system, user, raw response, parsed
actions, validation result)` keyed by `goal + step + input-hash`. Then:

```
plang build --replay <journal>     # re-run with recorded responses — zero LLM, byte-identical
```

This retires *most* of the discipline above: no "is it flaky or real", no N-runs, no
cache-as-accidental-repro. **A failure becomes a file you hand to anyone and it reproduces
instantly** — the clean version of the snapshot/handoff dance we hit with the FixAndResume work.

**Suggested mechanism (per your "use events" instinct):** the build is just PLang goals
executing, and the LLM provider already exposes `OnBeforeRequest` / `OnAfterResponse`
(`OpenAi.cs`). A build-scoped subscriber records each exchange, keyed by the current
`CallStack` position (goal/step). **Replay** = an interception layer (the existing
`mock.intercept` seam, or a "replay" `ILlm` provider) that, instead of calling the API,
returns the journaled response for the matching key. Journaling/replay stays **decoupled** from
the builder goals — it's an event/interception concern, not pipeline code. `event.on`
(before/after goal/step/action) is the natural subscription surface.

## Capability 2 — LLM emits `formal` only; the runtime parses it to `actions[]`

This is the deferred "lever #4", and I now believe it's *the* fix, not an optimization. Nearly
every failure I chased — empty `{}` responses, `String→LlmMessage`, `type`-as-array,
`goal.call(X)` name leaks, dropped `Trigger` — is the **same class: nested-JSON serialization
failure**. Shrink the LLM's output surface to one small, regular string (`formal`) and that
whole class disappears. The LLM commits to a chain; a deterministic C# parser owns the
structure (`formal` is already a real grammar — `Documentation/v0.2/formal-plang.md`).

This also removes the redundant `formal`↔`actions[]` "must mirror exactly" burden (the worst
combination for a small model): there's one source of truth, parsed once.

## Capability 3 — failures are typed and self-attributing

Every build/runtime failure carries a layer tag:
`planner-underpick | compiler-mismap | validator-reject | parse-error | runtime-conversion |
runtime-behavior`. I spent real time reverse-engineering "is this mine or coder's, mapping or
runtime" from stack traces — the runtime *knows* which stage failed; make it a field, not a
forensic exercise. (Concrete examples this session: the `llm.query` NRE was *runtime*, the
`event.on` Trigger-drop was *compiler-mismap*, `AddBeforeWrite` was *runtime-behavior* — all
looked similar until traced by hand.)

## Capability 4 — the build self-verifies (mirror + diff-vs-oracle)

- `builder.validate` checks *shape* today (action exists, params present). It should also
  enforce the `formal`↔`actions` mirror (catches drift) — moot once #2 lands, but until then
  it's the cheap guard.
- `plang build --diff <goal>` shows what a rebuild changed vs the committed `.pr`. "Did this
  rebuild stay correct?" should be one command, not hand-dumping `.pr`. The builder becomes its
  own regression oracle.

**Suggested mechanism (events again):** an `after goal build` event compares the produced `.pr`
to the committed one and emits a typed diff — no diffing logic threaded into the builder
pipeline.

## Capability 5 — a real `--explain`, not a megabyte of stderr

`plang build --explain <goal>` → structured, per step: intent, planner set, chosen actions,
confidence, retries, validation outcome. This is largely the existing trace, surfaced as a
first-class queryable artifact instead of something I `grep`/`awk` out of `--debug` logs.

---

## If you only do two

**#2 (formal-only output)** kills the largest failure class at the source; **#1 (replayable
build journal)** makes every remaining failure deterministic and instantly shareable. Together
they retire ~all of the diagnostic discipline — there's nothing flaky left to discipline. #3–#5
are the observability layer that turns "spelunk and guess" into "ask the build."

## Suggested sequencing

1. **Build journal + replay** (#1) — cheap, decoupled (events/interception), and it makes
   everything *else* (including validating #2) testable. Do this first.
2. **formal-only output** (#2) — the big reliability win; #1 makes it safe to land because you
   can replay the before/after deterministically.
3. **Typed failures + diff + explain** (#3–#5) — the observability layer, also event-driven.

## Open questions for the architect
- Journal scope/format: per-run file vs a queryable store? Keyed by step-hash — what's the hash input (goal text + prompt + catalog version)?
- Replay seam: extend `mock.intercept`, add a `replay` `ILlm` provider, or an event-level interceptor?
- Does the `event.on` surface already fire at the granularity needed (per-compile-call), or does the build need finer hooks?
- `formal` parser ownership: a new C# parser module; how does it report a parse failure as a *typed, replayable* build error (ties back to #1/#3)?
