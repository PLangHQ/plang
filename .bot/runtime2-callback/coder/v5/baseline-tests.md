# v5 baseline (before any fixes)

Branch: runtime2-callback @ f6496cd8 (post-pull, no coder fixes since b7427751)

## C# (TUnit, .NET 10)
- `dotnet run --project PLang.Tests -c Debug`
- **2720 / 2720 passed** — 0 failed, 0 skipped, 13s

## Plang (`plang --test` from Tests/)
- 192 total, **188 pass / 0 fail / 4 stale / 0 timeout**
- 4 stale are AskVarsOnNonAsk, CallbackTimeoutSetting, DurabilityRoundTrip, TamperedSignature — preexisting per coder/handoff.md gaps, NOT my regression
- The per-test "1 fail" lines that scrolled by are intentional `[Fail]` fixtures under `_fixtures_*/` consumed by meta-tests (matches tester+security accounting).

Build: clean rebuild, 0 errors / 423 warnings (preexisting).

## What I'm fixing this version (v5)
- S-F1 — Medium: auto-EnsureSigned for ICallback in callback.run; reject when RawSignature still null after Ensure.
- S-F3 — Low: MaxBytes guard on AskCallback / ErrorCallback wire deserialization.
- S-F4 — Low: SensitivePropertyFilter.Strip on AskCallback._options, ErrorCallback._options, PlangDataSerializer._options.
- N3: wrap CLR exceptions out of Restore as Data.FromError(IError) at the callback.run public entry.
- Test rename: `CallbackRun_VerifiesSignature_BeforeDispatch` flips to a real positive (in-process EnsureSigned → verify passes); add a negative (`CallbackRun_RejectsUnsignableData_*`).

NOT fixing this version (per Ingi):
- S-F2 — `Variables.Restore` !-prefix filter — Ingi says not a security issue, leave for architect.
