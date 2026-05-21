# claude-md-proposals — app-lowercase branch

## coder — v3 — 2026-05-18
**Target:** /CLAUDE.md
**Why:** The repo CLAUDE.md still references old PascalCase namespaces (`global::App.Channels.@this.Output`, `Data<App.Variables.Variable>`, `PLang.Tests.App.Data`, `PLang/App/Data/`). After the app-lowercase rename + 7 collision-folder merges, every one of those paths is stale. Three specific lines need updating so future bots reading this CLAUDE.md don't propagate the old naming.

**Proposed change:**

Line 18 — Console.* rule section:
```diff
-User-facing chatter → `await app.CurrentActor.Channels.WriteTextAsync(global::App.Channels.@this.Output, ...)` (do **not** route through `Debug.Write` ...
+User-facing chatter → `await app.CurrentActor.Channels.WriteTextAsync(global::app.channels.@this.Output, ...)` (do **not** route through `Debug.Write` ...
```

Line 39 — Property kinds gate (PLNG001):
```diff
-use `Data<App.Variables.Variable>`. `Variable` implements `IRawNameResolvable`, ...
+use `Data<app.variables.Variable>`. `Variable` implements `IRawNameResolvable`, ...
```

Line 41 — Test alias clash:
```diff
-Convention: use `*Tests` suffix on folder/namespace when mirroring `PLang/App/Data/` etc. → `PLang.Tests/App/DataTests/`, `PLang.Tests/App/VariablesTests/`.
+Convention: use `*Tests` suffix on folder/namespace when mirroring `PLang/app/data/` etc. → `PLang.Tests/App/DataTests/`, `PLang.Tests/App/VariablesTests/`. (Test folder names under `PLang.Tests/App/` stay PascalCase — only the source paths under `PLang/app/` are lowercase.)
```

Also worth adding to the "Runtime2 Conventions" section after the namespace lowercasing landed:

```markdown
- **Top-level `app/` is lowercase** for plang vocabulary (`actor`, `goals`, `variables`, `channels`, `errors`, `events`, `filesystem`, `formats`, `keepalive`, `snapshot`, `tester`, `types`, `config`, `callstack`, `data`) and PascalCase for C# infrastructure (`Attributes`, `Diagnostics`, `Services`, `Statics`, `Utils`). Seven engine concepts (`Cache`, `Builder`, `Callback`, `Settings`, `Modules`, `Code`, `Debug`) merged with their action-module counterparts under `app/modules/<name>/` — no separate top-level folder remains for those. Property names on `app.@this` stay PascalCase (`.Cache`, `.Builder`, `.Code`, `.Modules`, etc.); only the *types* live in lowercase namespaces.
```

(Reason for the property note: distinguishing property-access vs type-reference is the single most confusing thing about the rename, and the docs sweep made clear that without an explicit rule, every reviewer will get it wrong.)
