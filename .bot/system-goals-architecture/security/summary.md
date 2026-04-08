# Security Bot — system-goals-architecture

## v1 (2026-04-08)
Full blue+red team audit of PLang/App/ architecture (809 C# files changed). **Verdict: FAIL** — 2 high, 4 medium, 6 low. High findings: (1) Binding.Run missing try-finally permanently disables event guards on exception, (2) Variables.Resolve expands %!app% from untrusted sources enabling info disclosure. Signing, CallStack, decompression, LLM tool whitelist all solid. See [v1/summary.md](v1/summary.md).

## v2 (2026-04-08)
Re-audit after coder fixes. All 3 fixes correct: try-finally on Binding.Run, skipInfrastructure on Resolve(), CRLF stripped from HTTP headers. **Verdict: PASS** — no critical/high open. Ready for auditor. See [v2/summary.md](v2/summary.md).
