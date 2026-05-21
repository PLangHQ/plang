# Stage 5: Messages end-to-end

**Goal:** Walk the Messages app through the full permission flow on a real apps tree. First cross-app read prompts; user grants "always"; subsequent reads succeed; grant persists across process restart; revocation re-prompts; narrowed grant doesn't cover a wider request.

This stage is the acceptance test for the whole branch. Real `.goal` files, real `system.sqlite` paths, real consent flow with a test-driver supplying canned answers.

## Out of scope

- Productionizing the Messages app's content logic (what it does with the messages). This stage proves the permission flow, not Messages feature-completeness.
- HTTP and Payment templates — those land with their respective branches.

## Deliverables

### 1. Test fixture: `Tests/Permission/Messages/Start.goal`

Minimal Messages-shaped goal that, on run, iterates known apps and reads `/apps/<App>/system.sqlite`. Lives under `Tests/` — it's a fixture for the integration test, not a real app shipped under `os/apps/`.

### 2. Consent prompt format

The `Ask.Value` (the question string) is built by `Path.Authorize` (stage 2b). For a single ask:

```
%appName% wants to %verb% %path%
[y]es / [n]o / [a]lways
```

For bundled multi-path asks (Move/Copy):

```
%appName% wants to:
  - %verb1% %path1%
  - %verb2% %path2%
[y]es / [n]o / [a]lways (covers all)
```

The channel renders from this string. Whether the renderer is a `.template` file, inline format, or a goal-driven path is the channel's call — the user-visible text is what matters.

### 3. Integration test (`Tests/Permission/`)

Plang `--test` style. Six steps:

1. **No grant, suspend.** Run Messages against a stateless channel. Step touching `/apps/Email/system.sqlite` returns `Data<Ask>`; step loop captures Snapshot (stage 2a); channel renders the prompt; goal suspends.
2. **Grant "a", store.** Test driver responds `"a"`. On resume, file.read re-runs; Authorize signs and stores via `user.Permission.Add`. Persisted row lands in the `permission` table (`<AppRoot>/.db/system.sqlite`).
3. **Re-query, no prompt.** Rerun Messages immediately — no prompt; grant covers.
4. **Restart, still no prompt.** Restart the process; rerun Messages — no prompt; grant persisted.
5. **Revoke, re-prompt.** Revoke via `permission.revoke`. Rerun Messages — prompt fires again.
6. **Narrowed grant, narrowed read.** Issue a `Read` grant with `Metadata: false`. A read that needs metadata surfaces a fresh `Data<Ask>` for the narrowed verb.

### 4. Short doc under `Documentation/v0.2/`

Walks a reader through what just happened. Code examples drawn from the integration test.

## Test seams

- **Prompt seam.** Channel's user-answer hook replaced with a test driver returning canned strings (`"a"`, `"y"`, `"n"`). Signing pipeline stays real — verify the signed Data actually lands and round-trips.
- **Apps-tree fixture.** Minimal `Tests/Permission/_fixtures/apps/Email/system.sqlite`, `Tests/Permission/_fixtures/apps/Calendar/system.sqlite`, etc. — under Tests, not `os/apps/`. Just enough to have something to read.

## What this stage does NOT do

- Doesn't test prompt UI rendering quality (human-readability — separate review).
- Doesn't exercise signing cryptographic guts (other suites cover that).
- Doesn't validate Messages stores the read content anywhere — integration cut ends at "read returned bytes."

## What this stage proves

- Permission types compose correctly under a real-world ask (potentially glob across multiple apps).
- Storage round-trips across process restart.
- Re-query contract holds end-to-end (the same action runs twice; second run finds the grant).
- Channel renders correctly for single and bundled `Ask` payloads.
- `Permission.Covers` semantics — narrowed grant fails wider request.
- Revocation removes the grant and triggers a fresh consent prompt.
- The new FS surface doesn't regress existing actions.

## Dependencies

Stages 1–4 complete.

## Acceptance

- The 6-step integration test passes.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` zero regressions.
- `Documentation/v0.2/<new-doc>.md` exists and walks a reader through the flow.
- Spot-check of the `permission` table after the test shows the expected signed `Data<Permission>` row (2-column shape; `data` column is a serialized Data with `Actor = "user"` in the Value).
