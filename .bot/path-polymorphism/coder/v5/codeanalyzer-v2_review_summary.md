# Review summary — codeanalyzer v2

Source: `.bot/path-polymorphism/codeanalyzer/v2/report.md` · verdict **NEEDS WORK**.

codeanalyzer v2 re-reviewed coder v3's response to the v1 findings. **All eight
v1 findings (F1–F8) confirmed genuinely fixed** — structural, not suppression.
The polymorphism leak is closed. Three new findings:

| # | Sev | Finding |
|---|-----|---------|
| N1 | Medium | The F3 refactor made `file.exists` a passthrough; existence is now answered by `FilePath.AsBooleanAsync()` which **skips `AuthGate(Read)`**. Out-of-root file-existence probing went from gated → silent. Asymmetric: `HttpPath.AsBooleanAsync()` *does* gate (routes through `ExistsAsync` → HEAD). A permission gate changed by side effect — needs an explicit decision. |
| N2 | Low | `path.Equals` / `GetHashCode` still hard-code `OrdinalIgnoreCase` — the exact case-sensitivity drift `RootComparison` (introduced by F5) was created to kill. On Linux, case-distinct paths falsely compare equal / hash-collide. |
| N3 | Low | `assert.ResolveTruthy` re-implements the `IBooleanResolvable` dispatch that `Data.ToBooleanAsync()` already owns — duplicated logic that can drift. Coder's-judgement, not blocking. |

## Decision taken (with Ingi)

**N1 → gate it.** Add `AuthGate(Read)` to `FilePath.AsBooleanAsync` — symmetric
with the http scheme, restores pre-branch behavior. Chosen over keeping it
ungated: a permission gate should not vanish as a side effect.
