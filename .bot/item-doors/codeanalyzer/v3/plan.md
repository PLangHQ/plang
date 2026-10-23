# v3 plan: OBP scan before merge (decision 269)

Scope: `git diff $(git merge-base origin/item-doors origin/runtime2) origin/item-doors -- PLang PLang.Generators PlangConsole` (431 files). Weight: the 191 files changed since b96c60765 (my v2 re-check). Read only; commit only the report.
Method:
1. Mechanical scans over the added lines: type checks (`is X`, `as X`, `typeof`, `ReferenceEquals`), sentinels (`== ""`, `?? ""`, Name carrying a second answer), throwaway objects, role/origin words in names (declared, held, found, saved, own, written), verb+noun / noun+verb method names, `.Clr`, `.Value is/as`, `lock (`, `Context ??=`, `new X { Context =`.
2. Read touched files whole where the change sits; run every named smell in CLAUDE.md with yes/no.
3. Skip the accepted: Executor mode table, debug view's per-writer weak table, two `Code.Get<IStore>()` sites, `Apply`/`Save` two-input-form branches.
Output: v3/report.md (ranked findings: smell, file:line, code, root fix; checked-clean list), verdict.json, summary.md, report.json.
