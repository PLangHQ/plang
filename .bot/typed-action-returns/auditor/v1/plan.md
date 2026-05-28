# Auditor v1 — typed-action-returns

## Inputs

- codeanalyzer v3 → PASS (all v2 findings closed; N1 stale comments, N2 file.read warning gate)
- tester v2 → PASS (mutation-validated, 3136 C# + 221 PLang green)
- security v1 → PASS (3 low/info opens; semgrep baseline unchanged)
- coder handoff dated, 944-file diff, four merge commits since runtime2

## What I'm NOT re-checking

- File-by-file shape, OBP, attribute discipline (codeanalyzer v3 just did this)
- Mutation soundness of test bodies (tester v2 just did this)
- Auth boundaries, serializer trust shape, semgrep regressions (security v1 just did this)

## What I AM checking (the seams)

1. **Cross-file contract: Build() inference → variable.set Type stamp.** `file.read.Build()` learned a registered-types gate; do siblings (`http.request`, `http.upload`, `llm.query`) carry the same discipline, or did the lesson stop at one file?
2. **Ask.ToString() contract change.** Consumers in `path/this.Authorize.cs` and `path/file/this.Operations.cs` were named in the handoff — verify all sites actually read `.Answer` rather than relying on `ToString()`.
3. **ShouldExit flow with typed Ask.** Engine step loop must use the `IExitsGoal` value-side opt-out, not the old Type-only check.
4. **Build() exception swallow patterns.** `file.read.Build()` swallows broad exceptions; verify other Build()s don't throw uncaught at validate time.
5. **Channel("builder") no-op fallback discipline.** Security F1 flagged it as forward-risk; check whether any new code routes audit-class events through `Channel(name)` rather than `Resolve+null-handle`.

## Findings (preview)

See `result.md`. Headline: HttpBuildHelpers.InferTypeFromUrl is missing the
registered-types gate that file.read.Build() carries — literal URLs ending in
.pdf/.html/.png/.docx pass MIME but not Types.Get, producing a Build()-clean
stamp that runtime variable.set then rejects with "Unknown type 'X'". One
major; rest pass.

## Verdict expected

FAIL (one major: cross-file contract gap in Build() inference).
