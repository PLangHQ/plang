# security — runtime2-callback

Latest: **v1 — pass with 1 medium + 3 lows.**

| Version | Verdict | Highlights |
|---|---|---|
| v1 | pass | F1 (medium): callback.run skips signing.verify when RawSignature==null — wire bypass for any future channel ingest. F2/F3/F4 (low): mirror missing on Variables.Restore for !-prefix filter; no JSON size caps; `[Sensitive]` filter not on callback wire serializers. Auditor F1/F2 closures re-verified. |

See `v1/summary.md` for the full walkthrough and `v1/verdict.json` for
the structured verdict. Top-level machine-readable report at
`../security-report.json`.

The Medium is a design-pattern bypass that activates as soon as any
channel deserializes a callback wire — *fix before Stage 5 / HTTP wiring
lands callback receive*. Today no path feeds wire bytes into
`AskCallback.Deserialize` or `ErrorCallback.Deserialize` outside tests,
so the bypass isn't exploitable; the architecture already bakes it in.
