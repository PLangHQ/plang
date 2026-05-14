# Stage 4: PermissionRequired Error & Prompt Escalation

**Goal:** Define the `PermissionRequired` typed error returned by `Permission.Check` when no grant matches. Wire it so PLang's "ask user, permission:high" escalation picks it up, gathers the user's response, produces a signed `Data<Permission>`, calls `Permission/@this.Add(...)`, and retries the original operation.

**Scope:** The error type, the retry contract, the round-trip from action call → Check fail → escalation → grant → retry → success.

**Excluded:** The signing mechanism (PLang plumbing — out of scope as Ingi confirmed). The prompt UI rendering (PLang plumbing). The Messages-specific flow (stage 5).

**Deliverables:**

- `PermissionRequired` typed error in the appropriate Errors folder. Carries the requesting `Path` (which carries the calling Goal via Context), the requested `Verb.@this`, and any other display fields the prompt needs.
- Stage 3's placeholder error replaced everywhere with this real type.
- Documentation of the retry contract — how PLang's runtime catches `PermissionRequired`, how it invokes the prompt, how the prompt response converts to a signed `Data<Permission>`, how `Add` is called, and how the original action retries.
- Tests: an FS call against an unauthorized Path produces `Data.Fail(PermissionRequired{...})` with the right calling-goal, path, and verb fields. A grant-then-retry flow succeeds.

**Dependencies:** Stages 1–3 complete.

## Design

The error type carries everything the prompt needs to render:

- `Path Path` — the requested path. Path carries `Context.Goal` for the calling-goal display ("goal `/apps/Messages/PullInbox.goal` wants to read…").
- `Verb.@this Requested` — what the call asked for. The prompt walks Read/Write/Delete and their sub-options to render the verb cleanly (e.g. "wants READ, WRITE (append-only)").
- (Possibly) `Match suggested` — what kind of grant match the prompt should default to. For an app reading a single file, Exact. For a path the runtime detects as pattern-shaped, Glob. The prompt can confirm.

The retry contract is what most of stage 4 documents. The shape:

1. Action calls `FileSystem.Read(path)`.
2. FileSystem calls `Permission.Check(path, requested)`.
3. Check returns `Data.Fail(PermissionRequired{...})`.
4. FileSystem returns that Data up unchanged.
5. PLang's permission-escalation handler catches it.
6. Handler raises the prompt via the user channel (sees path, sees requested verb, sees calling goal).
7. User answers y/n/a/days.
8. On y/a/days: PLang plumbing constructs and signs a `Data<Permission>`; calls `Permission.Add(signed, persist: a-or-days)`.
9. PLang re-invokes the original action. Permission.Check now succeeds. Read returns content.

The architect doesn't own steps 5–8 (those are PLang plumbing per Ingi). Stage 4 owns the *contract*: what fields `PermissionRequired` must carry, what `Add` accepts, what retry-once expects to happen.

## What stage 4 does NOT do

- Doesn't implement the prompt UI.
- Doesn't implement signing.
- Doesn't implement the runtime's "ask user, permission:high" detection — but the error has to have the right shape for that detection to work, so coder must confirm by reading the existing escalation pattern.

## Acceptance

End-to-end test: an action calls Read on an unauthorized Path; the test injects a fake prompt response; a grant is added; the action's retry succeeds. No production code needs the prompt UI to run — the test seam is between Check-fail and Add.
