# Auditor v1 Plan — HTTP Module

## Scope
Full cross-cutting audit of the HTTP module (piece 4) on `runtime2-builder-v2-http`. All three prior reviewers passed: codeanalyzer v2, tester v4, security v2.

## Focus Areas

### 1. Cross-File Contracts
- **Signing integration**: HTTP provider creates `signing.sign` and `signing.verify` action records and calls `engine.RunAction`. Verify the action properties match what SignedData.CreateAsync/VerifyAsync expect. Trace the full sign→serialize→header→deserialize→verify round-trip.
- **Provider registry**: HTTP provider registered in Engine constructor. Verify disposal path (Engine.DisposeAsync → Providers.All() → IDisposable.Dispose). Check `_client ??=` thread safety.
- **Config scope chain**: HTTP uses `engine.Config.For<Config>(context)` and `config.Resolve(...)`. Verify Config.cs properties match what DefaultHttpProvider resolves.
- **Transport filters**: `_transportInOptions` uses `TransportPropertyFilter.ForInbound` to deserialize `Data.Signature` from wire. Verify [In]/[Out] attributes on Data.Signature match what the filter expects.

### 2. Architectural Fit
- OBP compliance: actions delegate to provider, provider navigates action records
- Static helper methods in DefaultHttpProvider — are any hiding behavior that should be on another object?
- Lazy-load convention: no eager loading issues expected in HTTP but verify

### 3. Review Quality Assessment
- Codeanalyzer: did they catch all OBP issues? Did the v2 pass miss anything?
- Tester: 95.9% coverage sounds good — but are the assertions strong? Any false greens?
- Security: size limits added — but is the streaming path bounded? SSE buffer cap verified?

### 4. Foundation Ripple
- Changes to Engine.this.cs (provider registration, disposal)
- Changes to Data.Envelope.cs (Signature property)
- Changes to CallFrame.cs (disposal lifecycle)
- Config rename (Settings → Config) — verify all consumers updated

## Non-Goals
- Re-checking individual file-level code quality (codeanalyzer did this)
- Re-checking test quality line-by-line (tester did this)
- Re-checking attack surfaces (security did this)

## Approach
1. Read the git diff for actual code changes (excluding .bot/)
2. Trace cross-file contracts manually
3. Run tests to verify green
4. Write findings, verdict, and report
