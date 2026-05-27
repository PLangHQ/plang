# docs v1 — typed-action-returns

## Context

- Auditor v2 **PASS** (7f635bd9b). Tester v2 PASS 3136/3136 C# + 221/221 PLang. Security PASS.
- No `claude-md-proposals.md` or `character-proposals.md` on this branch.
- Coder handoff is rich: Stage 0–4 (build hook, renames, http Response, per-action Build(), serializers→Data, multi-segment extension).

## What I checked

### XML doc coverage on new public surface — clean

Every new public type already has substantive `///` docs that explain what + why:

- `PLang/app/modules/IClass.cs` — Build()/SetAction() roles, default behavior, three return shapes.
- `PLang/app/modules/builder/warning/this.cs` — channel pattern + Action attribution rationale.
- `PLang/app/http/Response/this.cs` — Content-Type → Body shape dispatch.
- `PLang/app/tester/Test/this.cs` — discovery record + per-property docs.
- `PLang/app/mock/Mock/this.cs` — handle/spy semantics.
- `PLang/app/modules/output/ask.cs` — Ask suspend/resolved states, ToString carve-out, two-channel paths.
- `PLang/app/IExitsGoal.cs` — ShouldExit() default + override rationale.
- `PLang/app/channels/this.cs:99-112` — `Channel(name)` vs `Resolve` distinction documented.

No new XML doc gaps to fill.

### Stale references (renames not chased)

| File | Stale | Replacement |
|---|---|---|
| `Documentation/v0.2/good_to_know.md:99-109` | `MockHandle` (5 refs) | `Mock.@this` (`app.mock.Mock.@this`) |
| `Documentation/v0.2/good_to_know.md:166` | `app.tester.File` | `app.tester.Test.@this` |
| `Documentation/v0.2/architecture.md:221` | `app.modules.Schema.@this.Build()` | `app.modules.builder.Types.@this.Build()` |
| `os/system/modules/mock/action.description.md` | action renamed to `intercept` | rename file to `intercept.description.md` (orphan otherwise — `MarkdownTeaching.ScanOrphans` will warn) |

### Missing architecture documentation

Five new concepts ship on this branch with **zero coverage** in `Documentation/v0.2/`:

1. `IClass.Build()` compile-time hook + validate-pass that stamps `Type` on terminal `variable.set`.
2. `(type)` hint syntax in PLang (`write to %x%(json)`) and user-hint-beats-Build()-inference precedence.
3. `BuildWarning` channel-write pattern (out-of-band advisory; uses no-op `Channel(name)` fallback).
4. `Serializers/ISerializer` now returns `Data`/`Data<T>` end-to-end (no throws).
5. `app.http.Response.@this` typed-response record + Content-Type dispatch through serializer registry, plus multi-segment `GetByExtension` (`.junit.xml` → `.xml` with single-segment fallback).
6. `IExitsGoal.ShouldExit()` virtual-override pattern (Ask returns false when Answer bound — Value-side opt-out, Type-side still exits).

`Documentation/v0.2/good_to_know.md` already has the *Action `Run()` returns are typed* section. The new compile-time machinery is the missing companion piece — same root convention, the build-time side.

## Plan

1. Update stale references (4 spots above).
2. Rename `os/system/modules/mock/action.description.md` → `intercept.description.md`. Sharpen text to describe the rebranded action.
3. Add one consolidated section to `good_to_know.md` — *Build()-time type stamping — `IClass.Build()`, `(type)` hints, and `BuildWarning`*. Covers the compile-time hook, validate-pass flow, precedence (user `(type)` > Build() > LLM-emitted Type), and the `BuildWarning` channel-write pattern. One section, not five, because they're one mechanism.
4. Add a small section: *`Serializers/ISerializer` returns `Data` — no throws*. Captures the exception-list carve-out (`JsonException` / `NotSupportedException` / `IOException`) and the http response dispatch flow through `Serializers.GetByContentType` + `TextFallback`.
5. Add a small section: *Multi-segment serializer extension matching*. One paragraph + `.junit.xml` example.
6. Add a small section: *`IExitsGoal.ShouldExit()` — Value-side opt-out for resolved sentinels*. Documents the Ask precedent so the next type that wants the same pattern doesn't reinvent it.
7. CHANGELOG entry — recorded in `v1/result.md` per docs spec (no repo-root CHANGELOG file exists on this branch).

## CLAUDE.md / character proposals

None to process — `.bot/typed-action-returns/claude-md-proposals.md` and `character-proposals.md` are both absent.

## Out of scope

- Per-action `description.md`/`examples.md` for the new typed shapes are already present (test, mock, http) and accurate — no rewrites needed.
- `data-generic-design.md` and `builder-data-t-roadmap.md` describe the same arc at design-doc altitude; they're snapshots from earlier branches and don't need synchronisation with branch-level micro-changes.
- `file.save` cross-type coercion follow-up is already logged in `todos.md`.

## Verdict target

**PASS** — fill the gaps, update the stale refs, commit.
