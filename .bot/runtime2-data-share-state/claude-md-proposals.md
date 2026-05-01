# CLAUDE.md proposals — runtime2-data-share-state

## coder — v2 — 2026-04-30
**Target:** /CLAUDE.md (top-level, near the `## Build` / `## Debugging` sections)
**Why:** While running `plang --test` from the project root, the discoverer
crawled the whole tree and surfaced an old test file inside
`.bot/runtime2-settings/scaffolder/v1/tests/plang/Start.test.goal` as a
stale entry, plus duplicate stale `.pr` files in lowercase `tests/`. Those
weren't real test failures — they were noise from non-canonical locations
being treated as test sources. User noted: tests live ONLY under `Tests/`
(uppercase), and that should be the cwd when running the suite, so
discovery is bounded to the canonical location.

**Proposed change** (add as a new subsection under `## Build`):

```markdown
## Running plang Tests

- All plang tests live under `Tests/` (uppercase). Never under `tests/`,
  `.bot/`, `.build/`, `os/`, or any other tree.
- When running `plang --test`, change directory into `Tests/` first so
  discovery is bounded to the canonical location:

  ```bash
  cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
  ```

  Running `plang --test` from the project root will surface stale
  `.test.goal` files under `.bot/` (old bot output) as failures or stale
  entries — those aren't real test results.
- C# tests run from project root via `dotnet run --project PLang.Tests`
  (different runner, different rules).
```
