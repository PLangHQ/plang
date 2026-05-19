# Open Questions

After the 2026-05-19 design pass, most of the originally-open questions are settled or out of scope. This file is what's *actually* left open.

## Settled in this pass

- **Verb specification** → Implicit at the action layer, explicit inside the FS layer. Each `IPLangFileSystem` method bakes its verb into the implementation (e.g. `ReadText` checks `Verb.Read`). Action handlers don't pass verbs.
- **Variable attachment timing** → Lazy. First check on a variable reads the store; populates `var.Properties["permission"]`. Standard PLang lazy pattern.
- **Multi-verb / multi-path operations** → Batched check. The FS method (Move, Copy) collects all required permissions, returns one bundled `Data.Fail` whose Ask carries all needed permissions. Fail-fast.
- **Path normalization for matching** → Always absolute. The Path class already exposes `Absolute`; that's what `path.CheckPermission` matches against. Raw form is irrelevant to permission matching.
- **Per-process vs persisted grants** → Encoded in the signature expiry. No expiry = in-memory. Long expiry = sqlite-backed. Two stores, one shape, one source of truth per grant.
- **Throw vs return** → Return. `IPLangFileSystem` v2 surface returns `Data` from every method. Permission miss is `Data.Fail` whose Error implements the `Ask` marker. No `PermissionRequiredException`, no exception machinery.
- **Where the permission types live in the C# tree** → Each subsystem owns its own `Permission/` folder (`App/FileSystem/Permission/`, `App/Http/Permission/` future, etc.). No shared base class.
- **Generic Permission base class** → Not introduced. Each kind is an independent record. Pattern is documented, not codified.
- **Ask return shape** → `Data.Fail` with an Error implementing the `Ask` marker. No new Data terminal state. Built-in path inside `error.handle` recognises Ask-marked errors; non-Ask errors propagate normally. Same mechanism serves permission asks and free-text user-input asks from `output.ask`.

## Out of scope for this branch (filed elsewhere)

- **HTTP and Payment permission kinds** — follow-up branches. The pattern named in `plan.md` is what they instantiate.
- **Service-permission wire flow** (signed permissions traveling along with outbound Data to PLang servers) — http/payment follow-ups own this.
- **App-side cascade for requested verb config** — out of scope entirely. App-level policy ("this app's writes are append-only by default") sits above grants; not part of permission machinery.
- **Settings encryption-at-rest** — `Documentation/Runtime2/todos.md` 2026-05-11 entry. Permission signing is the integrity story; encryption is confidentiality. Decoupled.
- **Goal-mapped FS provider (Code routing for virtual filesystem)** — parked from the original plan. Default disk is the only Code variant in this branch.
- **`Read.Content` sub-option** (distinguish stat-only from content read) — defer. Today's `Read(Recursive, Metadata)` is enough for the Messages use case. Add `Content` as a separate pass when a real consumer needs it.

## Actually still open

### 1. Glob library choice (minor, blocks stage 1)

`Microsoft.Extensions.FileSystemGlobbing` is the BCL-family option. Alternatives: hand-roll a simple `*`/`?`/`**` matcher (~20 lines). The semantics needed for permission matching don't require every globbing edge case.

Leaning `FileSystemGlobbing` for free correctness. Coder can default to it; if a constraint surfaces (e.g. AOT compatibility), hand-roll.

### 2. Signature verification result caching (minor, blocks stage 3)

Per-Data verification cache: once a signed Data has been verified in this session, cache the result on the Data instance. Avoids re-verifying every access in tight loops (e.g. listing 1000 files all matching the same grant).

Where the cache lives: a non-serialized flag on the Data instance, or on a side-table keyed by signature bytes. The former is simpler (no separate state). The latter is cleaner from a "Data carries no transient state" standpoint.

Leaning flag-on-Data. Decide in stage 3 during the manager implementation.

### 3. Apps-without-identity edge case (blocks stage 5)

The Messages use case assumes every app has a stable identity (the `AppId` used as Subject in `FilePermission`). What if an app is running without identity setup (development scaffolding, anonymous mode)? Options:

- **Reject all out-of-root reads.** Strict. Forces identity setup before doing anything privileged.
- **Allow with a synthetic identity** scoped to the process. Permissive but breaks audit ("which app got that grant?").
- **Skip permission checks for in-root paths only; require identity for out-of-root.** Middle ground — matches the current behavior closely.

The Identity foundation work (see foundation-verify branch) probably already settled this for itself. Stage 5 verifies by reading the Identity layer.

### 4. UI for "y/n/a" outside interactive console (blocks stage 5)

The Messages case is interactive — actor talking to a user. What about non-interactive cases (cron-scheduled goals, server flows where no human is at the keyboard)? The ask flow has nothing to ask.

Two possible answers:
- **Configurable default per app.** App declares "all permission requests auto-deny" or "auto-grant for these paths." Removes the human from the loop with explicit setup.
- **Fail with `Data.Fail(no consenter available)`** when ask is invoked but no actor channel can prompt.

The second is honest about what's happening. The first might surface later as a real feature. Decide in stage 2 (ask handler implementation) — likely just fail-with-clear-error for now, configurable default later.

### 5. Concurrent permission requests across goals (blocks stage 2)

Two parallel goals both hit out-of-root paths simultaneously. Each returns `Data.Fail` with an Ask. `error.handle`'s built-in path catches each. Now: two consent prompts in flight at once on the same actor's output channel? Sequential? Coalesced?

Lean sequential — only one ask-in-flight per actor at a time. Second arrives, queues, displayed after first is answered. Honest about the UX (user sees prompts in order), no race conditions. Implementation: the built-in ask flow holds a per-actor lock.
