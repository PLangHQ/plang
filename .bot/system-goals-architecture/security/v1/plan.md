# Security Audit Plan — system-goals-architecture v1

## Scope

Full blue+red team security audit of the new `PLang/App/` architecture. This branch is a massive restructuring (809 production C# files changed) from Runtime2→App namespace, introducing:
- New App root with Actor model (System/Service/User)
- New execution dispatch (app.Run → Goal.RunAsync → Step.RunAsync → Action.RunAsync)
- New Variables system (ConcurrentDictionary-based)
- New Events/Lifecycle system
- New Channels/Serializers pipeline
- New Data.Envelope transport (compress/sign/encrypt pipeline)
- New module/provider loading system
- New builder integration

## Approach

1. **Blue Team**: Map attack surface area-by-area against the PLang threat model (user-sovereign, .pr files trusted, defend against external data)
2. **Red Team**: For each gap, describe attack vector, preconditions, severity, exploit sketch
3. **Cross-reference**: Check against previous branch findings (from memory) — what carried over, what's new, what regressed
4. **Report**: Write security-report.json and verdict.json

## Key Areas to Audit

| Area | Files | Risk Level |
|------|-------|------------|
| Assembly loading (module.add, provider.load) | modules/module/add.cs, modules/provider/load.cs | Accepted risk (user-sovereign) |
| Execution dispatch | modules/app/run.cs, Goal/Methods.cs, Step/this.cs, Action/this.cs | High |
| Error handling | modules/error/check.cs, Step/this.cs | Medium |
| Variable resolution | Variables/this.cs, Data/this.Navigation.cs | Medium |
| Data envelope (compress/decompress) | Data/this.Envelope.cs | Medium |
| HTTP module (SSRF, size limits) | modules/http/providers/DefaultHttpProvider.cs | Medium |
| Signing/verification | modules/signing/*, Ed25519Provider.cs | Low (well-implemented) |
| File operations | modules/file/read.cs, modules/file/save.cs | Medium |
| LLM tool execution | modules/llm/providers/OpenAiProvider.cs | Medium |
| Event system (re-entrancy) | Actor/Context/this.cs, modules/event/* | Low-Medium |
| CallStack (depth limits) | CallStack/this.cs | Low (has MaxDepth=1000) |
| Actor identity | Actor/this.cs | Medium |
| Serializers/Channels | Channels/Serializers/* | Medium |

## Deliverables

- `.bot/system-goals-architecture/security-report.json`
- `.bot/system-goals-architecture/security/v1/verdict.json`
- `.bot/system-goals-architecture/security/v1/summary.md`
- `.bot/system-goals-architecture/security/v1/result.md` (detailed findings)
- `.bot/system-goals-architecture/security/summary.md` (cross-session)
