# security — kind-is-type

**Version:** v2 PASS (second pass, review-line go). v1 PASS after one-cycle fix (F1 closed at `d9bc6aae7`; see `v1/recheck.md`).

## What this is

Full-branch security review of `kind-is-type` against parent `runtime2@cf66e2d01`. V1 (partial, architect's scope) covered stage 0 / 767 / 789-790 / decider-key resave and surfaced F1. V2 (review-line go, host-driven while the architect is busy) covers everything that landed since v1: stage 4 (json's uses-parse model), decision 814 (`Internal` on host kinds), file.read's `Type` slot, stage 5 (builder's pick/list kind follow-up), stage 6 (`.kind` direct, F4.2 face, F8.1 Takes door), the render fix at `Fluid.cs:160`, and the F1 re-check.

## What was done

Read-only review of each delta against the trust model (user-sovereign — defend against untrusted .pr bytes, untrusted channel receives, untrusted LLM output as structured data). Semgrep 24/24 baseline — nothing new.

**Outcome: v2 PASS, no new findings.**

The deltas since v1 are mostly type-system plumbing (locked-core-adjacent) and build-time teaching. The one trust-adjacent change (`file.read`'s `Type` slot) still gates reads through `path.Bytes → AuthGate(read)` before the declared type's birth, so an `as <type>` force cannot read bytes the step's actor isn't allowed to read. The stage-4 "json rests, uses parse" model preserves the pre-auth depth cap (`Utf8JsonReader` default `MaxDepth=64 ≥ Wire.MaxReadDepth=64`), and unread bytes do not reach a trust decision — the wire-layer signature covers them whole before any kind-level decode. The `Fluid.cs:160` render-fix lands rendered output as a plain text (instead of a code-kinded text); the SSTI surface, HTML encoding, `callGoal` sandbox and gated include-provider are untouched. Stage 5 is `os/system/builder/**/*.notes.md` and `.template` teaching plus a `KindOf` goal — no runtime C# per decision 835. Stage 6's `.kind`-direct, F4.2 face (`name<kind>` display) and F8.1 Takes-door cleanup are structural improvements; 834's #3 "source born with full type" regressed ~13 tests and was reverted (not on the tip).

Decisions 843-846 live on `llm-writer`, not here. 848 is a process complaint about ObpScan friction. 850 is a locked-core proposal routed to branch A; llm-writer applies a local workaround for image/binary. None landed in `kind-is-type`.

F1 re-check: predicate unchanged since the v1 recheck; pins at `GoalPathTests.APrFile_ThatNamesAPath_IsRefused` are intact.

Files touched (output):

- `.bot/kind-is-type/security/v2/plan.md`
- `.bot/kind-is-type/security/v2/result.md`
- `.bot/kind-is-type/security/v2/verdict.json`
- `.bot/kind-is-type/security-report.json` (updated)
- `.bot/kind-is-type/report.json` (my v2 session appended)

Next bot: **auditor**.

## Code example — the one trust-adjacent delta read in context

`file.read`'s `Start()` with a Type slot:

```csharp
return await Path.Use(async path => typed
    ? await Type!.Use(async declared =>
    {
        var bytes = await path.Bytes(Context);           // AuthGate(read) runs here
        return bytes.Success
            ? await declared.Create((await bytes.Value())?.Value, Context, path.Name.ToString(), path)
            : bytes;
    })
    : Template is { IsInitialized: true }
        ? await Template.Use(kind => path.Read(Context, kind.Value))
        : await path.Read(Context));
```

The `as <type>` force (Type slot) does not weaken the gate — bytes still have to pass `read` before the declared type gets to birth itself from them. The declared type's `Create` lifts the bytes into a plang value (text, dict, list, …) through its own birth; no extra FS reach.
