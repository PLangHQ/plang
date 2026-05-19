# Stage 5: Messages End-to-End

**Goal:** Walk the Messages app through the full flow on a real apps tree. Install (nothing special), first cross-app read prompts, user grants "always," subsequent reads succeed, grant persists across restart. Also ship the final `os/system/permission/file.template` that renders the consent prompt for filesystem permissions.

**Scope:** The acceptance test for the whole branch. Real `.goal` files, real `system.sqlite` paths, real consent flow (with a test-driver prompt). Final template work for the file kind.

**Excluded:**
- Productionizing the Messages app's content logic (what it does with the messages). This stage proves the *permission flow* works, not that Messages is feature-complete.
- HTTP and Payment templates — out of scope (those land with their respective branches).

## Deliverables

- **`os/apps/Messages/Start.goal`** — a minimal Messages app that, on run, iterates known apps and reads `/apps/<App>/system.sqlite` (or whatever the agreed convention is).

- **`os/system/permission/file.template`** — the user-facing prompt. Renders the FilePermission(s) carried by a `FilePermissionAsk` into a clear consent question. For a single ask:
  ```
  %appName% wants to %verb% %path%
  [y]es / [n]o / [a]lways
  ```
  For bundled (multi-path) asks:
  ```
  %appName% wants to:
    - %verb1% %path1%
    - %verb2% %path2%
  [y]es / [n]o / [a]lways (covers all)
  ```
  Whether it's a `.template` file or a `.goal` file depends on what the ask handler from stage 2 ended up loading. Either is fine — the user-visible text is what matters.

- **Integration test** under `Tests/Permission/` (plang `--test` style) that:
  1. Runs Messages with no grant. Verifies the `FilePermissionAsk`-marked Fail fires for `/apps/Email/system.sqlite` (and others), `error.handle`'s built-in path renders the consent prompt correctly.
  2. Test-driver responds "always" with a glob-shaped consent. Signed grant lands in `app.System.filesystem.permission`.
  3. Reruns Messages immediately. Verifies no prompt this time — grant covers all matching paths.
  4. Restarts the process. Reruns Messages. Verifies no prompt this time either — grant persisted.
  5. Revokes the grant via `permission.revoke`. Reruns Messages. Verifies prompt fires again.
  6. Issues a narrower grant (`Read` only with `Metadata: false`, no Content access). Verifies read attempt asking for full content surfaces a fresh Ask-marked Fail for the narrowed verb.

- **Short doc under `Documentation/v0.2/`** — walks through what just happened, for future readers. Code examples drawn from the integration test.

## Dependencies

Stages 1–4 complete.

## Design

This stage is the integration cut that proves the design end-to-end. Test seams:

- **Prompt seam.** The output.ask handler is replaced with a test driver that returns canned answers. The signing pipeline stays real — we want to verify the signed Data actually lands and round-trips.
- **Apps tree.** The test sets up minimal `os/apps/Email/system.sqlite`, `os/apps/Calendar/system.sqlite`, etc. — just enough that there's something to read.

What this test does NOT do:
- Doesn't test the prompt UI's actual rendering (the human readability question — that's a separate review).
- Doesn't exercise the cryptographic guts of signing (other suites cover that).
- Doesn't validate Messages stores the read content anywhere — the integration cut ends at "read returned bytes."

## What this stage proves

- **Permission types compose correctly** under a real-world ask (glob across multiple apps).
- **Storage round-trips across process restart.** Persisted grants survive shutdown.
- **Re-query retry contract holds end-to-end.** The same action runs twice; second run finds the grant.
- **Templates render correctly** for both single and bundled Ask payloads.
- **`Permission.Find` correctly applies `Covers`** — narrowed grant fails wider request.
- **Revocation** removes the grant and triggers a fresh consent prompt.
- **The new FS surface doesn't regress existing actions.** Stages 3–4 had to keep tests green, but the integration cut confirms it at the app level.

## Acceptance

- The 6-step integration test passes.
- `plang --test` from `Tests/` reports zero regressions (or only the intentional pre-existing failures from `_fixtures_*`).
- `Documentation/v0.2/<new-doc>.md` walks a reader through the flow.
- A spot-check of the `permission` table in `App.SettingsStore` (`<AppRoot>/.db/system.sqlite`) after the test shows the expected signed `Data<FilePermission>` row (2-column shape; `data` column is a serialized Data with `Actor = "user"` in the Value).
