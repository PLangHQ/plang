# v2 plan: re-review of the fixes (decisions 63, 65, 66, 74) and every touched file whole

Tip: 5d942c7af. Scope: the 79 files of `git diff --name-only 98624ab6a..HEAD -- PLang PLang.Generators`, each read whole (16.4k lines), older smells included and marked "older" (commit if visible) with a fix shape scaled to it; files the branch never touched are out. The 46 files changed since my v1 (4d28eda40..5d942c7af) weigh most.
1. Check my v1 findings 1-18 one by one against the coder's commits (bd4732e0e .. 5d942c7af).
2. Read each touched file whole; run every named smell (incl. creative name, verb+noun, EnsureTable-shaped setup steps, run-if-not-yet doors), the branch rules (29, 33, 35, 36, 39), sentinels, is/typeof/ReferenceEquals, throwaway objects, history comments, locked-core additions.
3. Ruled, checked as ruled: finding 8 readers on HasProperties/HasKept (69); system variables table a static readonly constant (70); finding 11 Hold stays `error?` (73); an actor's origin answers Console = true (73); finding 3's holder-vs-asker comparison is the fact (68). Known with Ingi: module.Remove answering data, the public ctor's default-true console flag (72), the channel list's late stamp in Adopt (74). Follow-up: debug's `item is goal` filters.
4. Speed record: Tools/Bench not present yet; joins the next review (67).
Output: .bot/fast-apps/codeanalyzer/review2.md, v2/verdict.json, summary.md, report.json. Read only; commit only .bot/fast-apps/codeanalyzer and the session log.
