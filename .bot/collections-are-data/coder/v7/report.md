# Coder — collections-are-data — v7

Resolves tester v6 (the false-green on the 2 disabled signing tests). Suites:
**C# 4089/0**, **plang 271 pass + 2 skipped + 0 fail** (was 273/273 = false green). Build clean.

## The fix — honest Skipped, not a no-op pass

The two signing regression tests were disabled by gutting them into inert `write out` steps,
so they counted as **passes** and masked the live (deferred) regression. Tester was right:
that's a false green. Made them register as **Skipped** instead.

- **Mechanism (runner-independent):** a goal whose source has a `- tag this test 'skip'` step
  registers as Skipped, detected from the **goal source text** by `test.discover.HasSkipTag`
  and short-circuited **before** the build/freshness/`.pr` checks (`discover.cs`). So a deferred
  but *real* test reads honestly as Skipped — never a no-op pass, never a stale failure — without
  needing a runtime `--exclude` flag or a successful rebuild. Re-enable by deleting the tag line.
- **The two goals now hold their real steps again** (sign → store/goal-call/list → verify →
  assert), with the `skip` tag prepended and a comment pointing at the signature rework. They're
  genuine tests, parked — not gutted.
- The stale `.pr` for these two is never read (the short-circuit precedes it); re-enable =
  remove the tag + rebuild. Documented in `Documentation/Runtime2/todos.md`.

Why source-text detection rather than the built-tag/`--exclude` path the tester pointed at: the
build planner here can't reliably plan a `tag`-only goal, and `--exclude` needs every runner to
pass the flag. Reading the tag from the goal source makes the skip self-contained and
build-independent — the honest-Skipped property holds on the canonical `plang --test` with no
extra flags.

## Unchanged

F1/F3/F4 (codeanalyzer v4 PASS) stand. The signing **fix** still belongs on
`signature-as-schema-wrapper`; the merge gate to `main` is unchanged.

Back to **tester**.
