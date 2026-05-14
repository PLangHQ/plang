# Stage 5: Messages End-to-End

**Goal:** Walk the Messages app through the full flow on a real apps tree. Install (nothing special), first cross-app read prompts, user grants "always," subsequent reads succeed, grant persists across restart.

**Scope:** The acceptance test for the whole branch. Real `.goal` files, real `system.sqlite` paths, real consent flow (with a test-driver prompt).

**Excluded:** Productionizing the Messages app's content logic (what it does with the messages). This stage proves the *permission flow* works, not that Messages is feature-complete.

**Deliverables:**

- `os/apps/Messages/Start.goal` — a minimal Messages app that, on run, iterates known apps and reads `/apps/<App>/system.sqlite` (or whatever the agreed convention is).
- An integration test (plang `--test` style under `Tests/`) that:
  1. Runs Messages with no grant — verifies the prompt fires for `/apps/Email/system.sqlite` (and others).
  2. Test-driver responds "always" to each prompt with a glob-shaped grant `/apps/*/system.sqlite`.
  3. Verifies the read succeeds and the grant lands in `app.System.filesystem.permission`.
  4. Restarts the process. Re-runs Messages. Verifies no prompt this time — grant persisted.
- A short doc under `Documentation/v0.2/` walking through what just happened, for future readers.

**Dependencies:** Stages 1–4 complete.

## Design

This stage is the integration cut that proves the design. The test seams:

- **Prompt seam.** The "ask user, permission:high" handler is replaced with a test driver that returns canned answers. The signing mechanism stays — we want to verify the signed Data actually lands and round-trips.
- **Apps tree.** The test sets up minimal `os/apps/Email/system.sqlite`, `os/apps/Calendar/system.sqlite`, etc. — just enough that there's something to read.

What this test does NOT do:

- Doesn't test the prompt UI rendering.
- Doesn't test the cryptographic guts of signing (other suites cover that).
- Doesn't validate that Messages stores the read content anywhere — the cut ends at "read returned bytes." Messages-the-app-logic is separate work.

## What this stage proves

- Permission types compose correctly under a real-world ask (glob across multiple apps).
- Storage round-trips across process restart.
- The PermissionRequired retry contract holds end-to-end.
- The Path foreign key (calling goal) makes it to the prompt and into the audit data.
- The new FS surface (Path-shaped) doesn't break existing actions — they had to keep working through stages 3-4.

## Acceptance

The integration test passes. A second test removes the persisted grant (edits the variable) and re-runs; the prompt fires again. A third test issues a narrower grant (`Read` only with `Metadata: false`) and verifies that a read attempt asking for full content fails the right way.

This stage is also the natural moment to write the user-facing doc — once the flow runs, the doc writes itself from the test.
