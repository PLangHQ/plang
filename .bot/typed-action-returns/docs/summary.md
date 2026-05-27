# docs summary — typed-action-returns

## Version
v1

## What this is
Final documentation gate on the `typed-action-returns` branch — the foundation pass that gives every action handler a typed `Run()` return + an optional compile-time `Build()` hook that stamps the result type on the step's terminal `variable.set`. Adds `(type)` hint syntax in PLang, an http `Response` record with Content-Type body dispatch, multi-segment serializer extensions, and `Serializers/ISerializer` returning `Data` end-to-end. Three internal renames (`MockHandle` → `Mock.@this`, `tester.File` → `Test.@this`, `Schema.@this` → `builder.Types.@this`) — no PLang catalog impact.

Auditor v2 PASS, tester v2 PASS (3136 C# + 221 PLang), security PASS. Docs is the merge gate.

## What was done

**XML doc coverage:** verified clean on every new public type. Coder already wrote substantive `///` docs for `IClass.Build`/`SetAction`, `BuildWarning`, `Response`, `Test.@this`, `Mock.@this`, `Ask`/`IExitsGoal`, and `Channel(name)`. Nothing to flag back.

**Stale references chased (3 + 1 orphan):**
- `Documentation/v0.2/good_to_know.md` — `MockHandle` → `Mock.@this`; `app.tester.File` → `app.tester.Test.@this`.
- `Documentation/v0.2/architecture.md:221` — `Schema.@this.Build()` → `builder.Types.@this.Build()`.
- `os/system/modules/mock/action.description.md` → renamed to `intercept.description.md` (the action moved). `MarkdownTeaching.ScanOrphans` would have flagged it.

**Architecture documentation added** to `Documentation/v0.2/good_to_know.md` — four new sections covering the build-time side of the typed-returns story:
1. *Build()-time type stamping — `IClass.Build()`, `(type)` hints, and `BuildWarning`* — precedence (user `(type)` > `Build()` > LLM Type), `SetAction` priming seam, `Channels.Channel(name)` no-op fallback for opportunistic writes.
2. *`Serializers/ISerializer` returns `Data` — no throws* — closed exception list (`JsonException`/`NotSupportedException`/`IOException`); http response dispatch via `Serializers.GetByContentType` + `TextFallback`.
3. *Multi-segment serializer extension matching* — `.junit.xml` → `.xml` fallback; `path.Extension` carries no leading dot.
4. *`IExitsGoal.ShouldExit()` — Value-side opt-out for resolved sentinels* — Ask precedent for two-state records on the suspend/resume boundary.

**Proposals:** `claude-md-proposals.md` and `character-proposals.md` both absent on this branch — nothing to evaluate.

**Verdict:** PASS — ready to merge.

## Code example — the (type) hint flow

```
- read file.csv, write to %rows%             ; file.read.Build() infers Type="csv"
- ask llm "summarise these", write to %s%(json)  ; user (type) hint wins → Type="json"
- post https://x/upload, write to %r%        ; http.request returns Data<Response>;
                                             ;  %r.Body% is JsonNode / string / byte[]
                                             ;  by Content-Type
```

PLang gets the right `Type` stamped on each terminal `variable.set` at build time — runtime hits the typed Data with no inference. Build-time inference is opportunistic; the user hint is final; LLM-emitted Type fills the gap when neither fires.
