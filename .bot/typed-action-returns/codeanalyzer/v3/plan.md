# codeanalyzer v3 plan — typed-action-returns

**Trigger:** v2 review left 10 findings (F1-F11, F8 closed won't-fix). Coder
returned 4 commits addressing them:

- `fe0c25245` — F2-F5, F9-F11
- `2553dd7f2` — PLang test stubs + 2 fixes (test infra, not in v3 scope)
- `bc794aea2` — F1 + intercept naming + F6/F7 docs
- `3ccf77ecf` — F8 closed (doc edit only)

**Scope:** verify each v2 finding was addressed cleanly; spot any
regressions or new smells introduced by the fixes.

## Production source diff (117b754f3..HEAD)

| File | What changed | Maps to |
|---|---|---|
| `PLang/app/mock/Mock/this.cs` | `ActionPattern` → `Pattern` | beyond F2 — owner-side rename for symmetry |
| `PLang/app/modules/builder/code/Default.cs` | `(handler, err)` → `(handler, _)` | F9 |
| `PLang/app/modules/file/read.cs` | TrimStart('.'); registered-types gate; narrowed catch | F5 + new gate |
| `PLang/app/modules/goal/getTypes.cs` | typeParam precedence swapped; `%__data__%` → `%!data%` literal | follow-on (architect) |
| `PLang/app/modules/http/code/Default.cs` | Leaf helpers return Data; outer catch shrunk; F6 docstring | F3, F4, F6 |
| `PLang/app/modules/mock/action.cs → intercept.cs` | file+class renamed; uses `Pattern` field | F2 |
| `PLang/app/modules/mock/verify.cs` | uses `Mock.Pattern` | symmetry |
| `PLang/app/modules/output/ask.cs` | docstring note on ToString answer leakage | F7 |
| `PLang/app/modules/test/discover.cs` | comment `File.Goal` → `Test.Goal` | F11 |
| `PLang/app/modules/test/report.cs` | `run.File` → `run.Test` (8 sites) | F1 propagation |
| `PLang/app/modules/test/run.cs` | comment stale ref fixed | F10 |
| `PLang/app/tester/Run.cs` | property `File` → `Test` | F1 |
| `PLang/app/types/this.cs` | csv/txt/xml/yaml/yml aliases registered | enables file.read gate |

## Five-pass + leaf-Data + Data<T> footgun

- Pass 1a/1b: nothing new structurally; rename is owner-internal.
- Pass 2: F4 collapsed cleanly; verify no other duplication slipped in.
- Pass 3: stale-comment hunt across all renamed files; `%__data__%` → `%!data%`.
- Pass 4: behavioral — does the new file.read.Build() gate change observable warning behavior?
- Pass 5: deletion test on the new gate + the new typeParam-first branch.
- Leaf-Data: re-verify HTTP body helpers + new Data<HttpContent> return on ResolveUpload.
- Data<T> footgun: spot new typed forwarders.

## Build status

`dotnet build PlangConsole` clean (454 warnings, 0 errors). Same warning count
pre/post — none introduced.
