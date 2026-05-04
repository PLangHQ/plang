# auditor — runtime2-callstack — v1 plan

First (and only-needed) auditor pass on this branch. Three reviewers have already
run: codeanalyzer through v3 (PASS), tester through v1 (PASS after stale-binary
correction), security through v2 (PASS, F1 polish-pass recommended, F2 accept).
Then coder shipped one more commit (5157d10a) closing F1 + accepting F2 + the
Flags rename Ingi requested in review.

## What I'm checking that the others didn't

- **F1 close in code, not just commit message**: security v2 recommended a ~30-line
  domain-class mirror for Diffs/Tags. Verify the mirror exists and is symmetric
  with Audit/Trail/Errors/Children — same lock pattern, snapshot iteration, no
  raw `List<T>` exposed.
- **F2 acceptance materialized as documentation**: security v2 accepted F2 *if*
  documented. Verify the "accept and document" landed at the relevant property.
- **Cross-file ripple of the Flags rename**: `CallStackFlags → Flags` and
  `Debug.ParseCallStackFlags → Flags.Parse`. Verify zero stragglers (tests,
  docs, callers, project-level global usings).
- **Cross-file ripple of always-allocate Tags**: previously `Tags` was nullable
  and lazy-allocated. Now it's `public Tags.@this Tags { get; } = new();`. Any
  consumer that null-checked Tags would silently change behavior. Verify none.
- **Tags interface-shape choice**: implements IDictionary<string,string> but
  throws NotSupportedException on Add/Remove/Clear. Liskov-violating? Decide
  whether worth flagging.
- **PLang-side coverage**: `%!callStack.Caller.Tags.foo%` resolution path
  through DictionaryNavigator's third arm (`IDictionary<string, T>`). Confirm
  there is at least one PLang test exercising it end-to-end on the new type.
- **Stale doc check**: comments inside test files / module docs that referred
  to old shapes (lazy-alloc Tags, CallStackFlags type name).
- **Build + test on clean state**: don't trust prior numbers — rebuild
  PlangConsole and rerun PLang tests. Tester's stale-binary failure-mode is
  exactly the trap reviewers can fall into.

## Out of scope

- Re-reviewing the OBP refactor of Audit/Trail/Errors/Children (security v2
  closed those cleanly; codeanalyzer v3 verified).
- Re-reviewing the AsyncLocal Call-tree shape (codeanalyzer v1-v2 covered).
- Bounded-retention deferral (architect-accepted, future branch).
