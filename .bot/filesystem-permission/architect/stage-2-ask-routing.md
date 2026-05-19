# Stage 2: `Ask` Marker + `error.handle` Built-In Routing

**Goal:** Define the `Ask` marker (interface or base record) that any "I need someone to answer something" error implements. Extend `error.handle`'s built-in path to recognise Ask-marked errors and run the consent/input flow: render via template, collect user response, sign if a permission, store the signed grant, report handled/not-handled. On handled, engine re-runs the same action. The `output.ask` action emits Ask-marked errors when it suspends; permission asks share the same path.

This stage builds shared infrastructure used by every permission kind and by `output.ask` itself. Filesystem (stage 4) is the first consumer of permission asks. HTTP and Payment (future branches) re-use it without modification.

**Scope:**
- `Ask` marker (interface or base record) — what concrete error types implement to opt into ask routing.
- `error.handle` built-in path that recognises Ask-marked errors and runs the consent/input flow.
- Template loader under `os/system/permission/<kind>.template`.
- Per-actor lock so concurrent asks on one actor serialise.
- Engine retry: on `error.handle` reporting "handled," dispatch loop re-runs the same action with the same params; "not handled" leaves the Fail to the user-configured `error.handle` or normal propagation.
- Migrate `output.ask` to emit Ask-marked errors when it can't return synchronously.

**Excluded:**
- The `Actor.@this.Permission` typed view (stage 3) — Stage 2's built-in path inside `error.handle` calls `actor.Permission.Add(signed)` to store the signed grant, but the view's body is stage 3.
- IPLangFileSystem v2 (stage 4).
- Filesystem-specific templates (`os/system/permission/file.template`) — final templates are a stage 5 deliverable. Stage 2 ships a stub template that proves the loader works.

## Deliverables

- **`Ask` marker.** A small public interface (or sealed-hierarchy base record) lives where reviewer can find it — likely `PLang/App/Errors/Ask/this.cs` or similar. Concrete Ask types (e.g. `FilePermissionAsk`, `UserInputAsk`) implement it. Coder picks the exact shape during stage 2 — important properties: discoverable, can carry kind-specific payload, plays nicely with the existing error hierarchy.

- **Built-in `error.handle` path that recognises Ask.** Inside the runtime's error-handle dispatch (wherever it lives today), add a branch: if the error implements `Ask`, run the consent/input flow before any user-configured handler. If the flow doesn't resolve the ask (denial, no actor channel, etc.), fall through to the user-configured handler / propagate.

- **Template loader.** Reads `os/system/permission/<kind>.template` (or `.goal`). Kind discriminator comes from the concrete Ask type (the existing TypeMapping/Kind infrastructure). PLang developers customise per app or globally.

- **Per-actor lock.** Two parallel goals on the same actor that both surface Ask-marked errors don't get two concurrent prompts. Second queues until the first resolves.

- **Engine retry inside the dispatch loop.** When `error.handle` reports "handled," the dispatch loop re-runs the same action (same `Action` instance, same params). One re-run attempt; if it asks again, propagate. The Action instance and original params are already in scope at the dispatch site — no new continuation machinery needed.

- **Stub template at `os/system/permission/file.template`** — minimal placeholder so end-to-end tests in stage 2 prove the loader works. Final template is a stage 5 deliverable.

- **`output.ask` migration.** When `output.ask` can't return synchronously (waiting on user input over HTTP, etc.), it returns `Data.Fail` with a `UserInputAsk`-marked error carrying the prompt. Same machinery handles it — render to the actor channel, collect the answer, re-run.

- **Tests:**
  - Concrete Ask error round-trips through serialization.
  - `error.handle` routes Ask-marked errors to the consent flow (mock the flow, assert dispatched).
  - Non-Ask errors fall through to user-configured handler / propagate as today.
  - Consent flow signs and calls `Permissions.Add` (mock the store) for permission asks.
  - "handled = true" path: engine re-runs the action; "handled = false" path: original Fail propagates.
  - "n" path: handler returns "not handled," engine's conversion path verified.
  - Concurrent asks on one actor serialise.
  - `output.ask` migration: synchronous answer still returns Ok; suspended answer returns Fail+UserInputAsk and resumes correctly.

## Dependencies

- Stage 1 (Permission types exist as concrete records so the consent flow can inspect them).
- Existing signing infra (`signing.sign` action, `Data.Signature` field). Pre-existing on runtime2; no changes needed.

## Design

The runtime flow is documented in [v1/plan/runtime-flow.md](v1/plan/runtime-flow.md). Read that for the full narrative; this stage file is the unit of work.

### Three things to get right

1. **`Ask` is part of the error hierarchy, not a new Data terminal state.** All asks are `Data.Fail` whose Error implements `Ask`. Anything that has to check "is this an ask?" does so by looking at the Error's type.

2. **Built-in handling lives inside `error.handle`.** No parallel router. The consent/input flow IS `error.handle`'s built-in behaviour for Ask-marked errors. User-configured `on error, call ...` modifiers see Ask-marked errors only after the built-in path didn't resolve them (e.g. user denied).

3. **Retry is the engine's job.** The handler reports handled/not-handled. The engine — which already has the original Action instance and params in scope at the dispatch site — re-runs the action on "handled." The handler never references the action.

### What stage 2 does NOT do

- Doesn't implement `Permission/@this.Add` — stage 3. Stage 2 uses a mock.
- Doesn't implement any filesystem operation — stage 4.
- Doesn't ship the real `file.template` — stage 5.
- Doesn't redesign `error.handle`'s overall structure — only adds the Ask-aware built-in branch inside it.

## Acceptance

- `Ask` marker compiles and is implemented by at least one concrete error type (`UserInputAsk` from the `output.ask` migration).
- `error.handle` tests pass: Ask-marked errors route to consent flow; non-Ask errors do not.
- Engine re-run tests pass for "handled" outcome.
- Stub template loader proves per-kind file resolution.
- No FS or runtime regressions; existing `dotnet run --project PLang.Tests` stays green.
- `output.ask` keeps existing synchronous-answer behaviour green; new suspended-answer path proven in test.
