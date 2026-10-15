# v4 — "do everything", Wave B: the remaining concepts

Finish the in-tree developer tree. v3 did the 7 core concepts; v4 writes the
smaller/leaf concepts so every top-level `app` concept has a `start.md`.

Same contract: in-tree `start.md`; C#-developer audience; what → where → plang;
point to teaching layer; verify every precise claim.

## Pages (one Haiku writer each, parallel)

| Page | Spine source |
|---|---|
| `PLang/app/setting/start.md` | setting/this.cs (+ module/setting, actor/setting, call/setting as references) |
| `PLang/app/store/start.md` | store/this.cs, store/sqlite/this.cs |
| `PLang/app/snapshot/start.md` | snapshot/this.cs |
| `PLang/app/task/start.md` | task/this.cs, task/list/this.cs, task/run/this.cs |
| `PLang/app/test/start.md` | test/this.cs, test/list/this.cs, test/report/this.cs |
| `PLang/app/keepalive/start.md` | keepalive/this.cs |
| `PLang/app/service/start.md` | service/this.cs, service/list/this.cs |
| `PLang/app/warning/start.md` | warning/this.cs, warning/list/this.cs |
| `PLang/app/parent/start.md` | parent/this.cs |
| `PLang/app/shortcut/start.md` | shortcut/this.cs, shortcut/list/this.cs |
| `PLang/app/type/item/variable/start.md` | variable/this.cs, variable/list/this.cs, variable/parser/this.cs, variable/code/* |

These are smaller than the core concepts; pages are proportionally shorter. The
`variable` page is the exception — it is the value side of memory (the call page
covers the store), so it gets a fuller treatment (the parser, the hops/code,
Start/Set).

## Method

Same as v3: parallel Haiku writers, each with the style brief + spine files + an
existing page as voice reference; verify + reconcile cross-links + commit.

## After v4

The tree is complete for every top-level concept. Remaining follow-ups (not v4):
retire/redirect the stale `Documentation/architecture.md` + `type-system.md`;
optional CLAUDE.md `Merge()` cleanup; a root `PLang/app` index refresh if wanted.
