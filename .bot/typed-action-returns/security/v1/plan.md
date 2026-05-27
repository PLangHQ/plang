# Security v1 — typed-action-returns

## Scope
Diff `origin/runtime2..HEAD` (~190 files). Focus on security-relevant surfaces:

- **Serializer Data<T> refactor** — `serializers/serializer/{Json,Text,plang/Data,plang/this}.cs`
  + did the typed-error wrapping preserve [Sensitive] discipline and signing inflow?
- **HTTP size-cap consolidation** — `modules/http/code/Default.cs`
  + `ReadLimitedBytesAsync`/`ReadLimitedStringAsync` collapsed; verify no regression of MaxResponseSize/slow-loris.
- **Ask shape change** — `modules/output/ask.cs`, `data/ShouldExit.cs`
  + Resolved Ask now flows through; `Ask.ToString()` leaks Answer (already documented by codeanalyzer v3 F7).
- **Build() compile-time hook** — `modules/IClass.cs`, `Generators/Emission/Action/this.cs`, `file/read.cs`, `llm/query.cs`, `http/HttpBuildHelpers.cs`
  + Runs handler code at validate. Does it do IO on attacker-controlled .pr inputs?
- **Channel(name) no-op fallback** — `channels/this.cs`, `channels/channel/noop/this.cs`
  + Silent drop semantics. Could mask audit writes.
- **PathHelper / path canonicalization** — `Utils/PathHelper.cs`, `types/path/*`
  + Already-closed dot-dot finding still holds? PathHelper.GetExtension change.
- **Mock rename** — `mock/Mock/this.cs`, `modules/mock/intercept.cs`
  + Regex ReDoS? Trust boundary?

## Method
1. Run semgrep baseline (must stay at 15).
2. Read every changed C# production file with security relevance.
3. Cross-check against standing-findings memory.
4. Mutation-test if any new claim depends on a particular code path firing.

## Verdict
PASS if no new critical/high open.
