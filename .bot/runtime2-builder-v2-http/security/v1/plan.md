# Security Audit Plan — v1

## Scope

All new/changed code on `runtime2-builder-v2-http` vs `runtime2` base:

1. **HTTP module** — `modules/http/` (request, download, upload, configure, DefaultHttpProvider, types)
2. **Provider registry** — `Engine/Providers/this.cs`, `modules/provider/load.cs`
3. **Crypto module** — `modules/crypto/` (hash, verify, DefaultProvider, types)
4. **Signing module** — `modules/signing/` (sign, verify, SignedData, Ed25519Provider)
5. **Transport serialization** — `TransportPropertyFilter`, `SensitivePropertyFilter`, `View.cs` attributes
6. **Config system** — `Engine/Config/this.cs`, `ModuleView`, `Scope`
7. **Engine changes** — `Engine/this.cs` (provider registration, RunAction, KeepAlive, disposal)
8. **Path** — moved to `Engine/FileSystem/Path.cs`
9. **CallFrame** — disposal lifecycle additions

## Threat Model Reminder

- **User-sovereign**: .pr files are trusted, user actions are not attacks
- **Trust boundary = cryptographic signatures on Data**: application/plang responses MUST be signed and verified
- **Defend against untrusted external data**: HTTP responses, headers, streaming content from external APIs
- **Provider loading = intentional RCE**: Assembly.LoadFrom is by-design; the user chose to load that DLL

## Attack Surface Areas

### A. HTTP Response Handling (HIGH PRIORITY)
- Response parsing for JSON, XML, text, binary, application/plang
- Deserialization of untrusted JSON from external servers
- application/plang response verification pipeline
- Streaming response handling (SSE, NDJSON, bytes, plang)
- Error response body parsing and identity extraction
- Header injection via response metadata

### B. Signing/Verification Pipeline (HIGH PRIORITY)
- SignedData.VerifyAsync 9-step pipeline
- Nonce replay protection
- Timeout/expiry enforcement
- ToSigningBytes determinism
- X-Signature header serialization

### C. Provider Registry (MEDIUM)
- Assembly.LoadFrom for DLL loading (by-design but document)
- Provider name collision / replacement
- Default provider swap attacks
- Thread safety of ConcurrentDictionary operations

### D. Config Scope Chain (MEDIUM)
- Apply<TConfig> reflection-based property setting
- Scope chain resolution (context → parent → defaults)
- Cast<T> type coercion safety

### E. Transport Serialization (MEDIUM)
- TransportPropertyFilter re-including [JsonIgnore]d properties
- SensitivePropertyFilter stripping [Sensitive] data
- [In]/[Out] attribute semantics and bypass vectors

### F. Crypto Module (LOW)
- Hash algorithm validation
- Base64 decode error handling
- HashedData serialization

## Methodology

1. **Blue team**: Map each area's exposure, trust boundaries, existing mitigations, gaps
2. **Red team**: For each gap, describe attack vector, feasibility, severity, exploit sketch, proposed fix
3. **Cross-reference** standing findings from memory (DeserializeValue, Data.Envelope, __condition__)
4. **Rate by threat model**: User actions = not attacks; external data = the real surface

## Deliverables

- `security-report.json` — structured findings
- `verdict.json` — pass/fail
- `v1/summary.md` — human-readable summary
- `summary.md` (bot root) — cross-session index
- Updated `report.json` with actions and after data
