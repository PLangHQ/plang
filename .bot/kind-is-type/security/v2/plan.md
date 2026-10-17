# security v2 — kind-is-type (second pass, review-line go)

Branch tip `356d9ca44` (pulled). Parent `runtime2` cf66e2d01. V1 scope was partial (stage 0, 767, 789/790, decider-key resave); F1 landed at `d9bc6aae7`.

The host (WSL mirrored networking failure, running the line while the architect is busy) handed me the v2 scope:

- the code since v1 — stage 4 (decisions 814, 822, 824–830), decision 814 (`Internal` on host kinds), file.read's Build (831, 836, 837, 838), stage 6 (833, 834), the builder's stage 5 (816, 817, 835, 839, 840) and its pick/list kind follow-up, the coder's render fix (2e83d21ff, Fluid.cs:160);
- re-check F1 fix — codeanalyzer v2 said it was closed;
- codeanalyzer v3 PASS (58348d1cc), tester v2 PASS (356d9ca44 against baseline) are already in.

Approach: static. Trust-model focus — the deltas are mostly type-system plumbing (locked-core-adjacent) and don't touch AuthGate / signing / channels / sandbox. Verify none drops a cap, moves a parse earlier, or lets a wider type reach something that trusts a narrower one. Semgrep as step 1.

## Findings preview

- No new findings. v2 PASS.
- F1 re-check: still closed; predicate unchanged since v1 recheck (`Reader.cs:69-80`).
- Stage-4 json-unread-courier: pre-use parse-on-touch, `Utf8JsonReader` default `MaxDepth=64` ≥ `data.Wire.MaxReadDepth=64` — depth cap intact; no new unbounded parse site.
- file.read Type slot: `path.Bytes(Context)` still runs through `AuthGate(read)` before the declared type's birth; no gate weakening. Build-only publishes the Type carrier; no I/O.
- Stage 5 (builder teaching) + Stage 6 (`.kind` direct, F4.2 face, F8.1 Takes door): type-system plumbing; no AuthGate / channel / signing touch; the regressed "source-born-with-full-type" was reverted at 834's #3.
- Fluid.cs render fix (`format is { Name: "item" }`): changes landed-value TYPE from a code-kinded text to a plain text; the SSTI surface, HTML encoding, `callGoal` sandbox and gated include-provider are untouched. Codeanalyzer's F3.v3 is a design-smell carry to branch A, not security.
- Decision 850 (bare-kind type-by-name): a locked-core proposal, not landed; llm-writer fixes it locally. N/A for this branch.
- Semgrep: 24 blocking, all baseline (22 serializer-hygiene + 1 gated Assembly.LoadFrom + 1 Display.Gate FP). No new.
