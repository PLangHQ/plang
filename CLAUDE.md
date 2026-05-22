# PLang Project

## PLang Syntax (v0.1 builder limitations)
- Cannot combine two modules in one step (e.g., `if + set` must be separate steps)
- foreach always calls a goal, does not support sub steps. Syntax: `foreach %products%, call DoProduct item=%product%`, `item=%variableName%` not `%variableName%=%item%`
- Simple set statements work: `set %step.Name% = %stepResult.method%`

## Runtime2 Conventions
- **`app/` is lowercase** for PLang vocabulary (`actor`, `goals`, `variables`, `channels`, `errors`, `events`, `filesystem`, `formats`, `keepalive`, `snapshot`, `tester`, `types`, `config`, `callstack`, `data`) and PascalCase for C# infrastructure (`Attributes`, `Diagnostics`, `Services`, `Statics`, `Utils`). Seven engine concepts (`Cache`, `Builder`, `Callback`, `Settings`, `Modules`, `Code`, `Debug`) merged with their action-module counterparts under `app/modules/<name>/` — no separate top-level folder remains for those. **Property names on `app.@this` stay PascalCase** (`.Cache`, `.Builder`, `.Code`, `.Modules`, `.FileSystem`, `.Goals`, etc.) — only the *types* live in lowercase namespaces. So `ctx.App.FileSystem.Read(...)` is property access (stays capital); `app.filesystem.@this` is the type. **One keyword carve-out**: `app/filesystem/Default/` stays PascalCase because `default` is a C# keyword. Two PLang action renames fell out of the rename: `app.run` → `environment.run`, `builder.app` → `builder.load` (both temporary names, deliberate naming pass deferred).
- **`@this` convention**: Every folder's primary class is `@this` in `this.cs`. Consumers use global aliases (e.g., `global using Step = ...Step.@this;`). Within parent namespaces, use `ChildNamespace.@this`.
- **Goal properties**: use `Path` and `PrPath` (relative), not `FilePath`/`PrFilePath`/`RelativePath`
- **Step.Goal**: has `[JsonIgnore]` to avoid circular reference in serialization
- **v0.2 .pr.json format**: single file with all steps
- **Lazy params**: Source generator emits a `partial class` extension on the action record itself (no separate `*__Generated` record) — properties resolve `%var%` lazily on first access via `Action.GetParameter(name).As<T>(Context)`
- **Handler naming**: records = action name (`set`, `save`), handlers = `SetHandler`, `SaveHandler` (partial)
- **`ICodeGenerated`**: added automatically by the source generator — handlers never implement it directly
- **`Data`**: universal result type with `Value`, `Properties`, `Error`, `Success`, `Ok()`, `Fail()`, `Merge()`. Extended via Properties.
- **`Action.Return`**: `List<Data>?` — simple list of return variable mappings, no wrapper class
- **No `Console.*` writes in production C#.** Channels exist to make I/O redirectable; `Console.WriteLine`/`Console.Error.WriteLine` bypass that. Diagnostics → `await context.App.Debug.Write(...)` (debug channel, gated on `--debug`). User-facing chatter → `await app.CurrentActor.Channels.WriteTextAsync(global::app.channels.@this.Output, ...)` (do **not** route through `Debug.Write` — its `IsEnabled` gate would silence it without `--debug`). Interactive prompts use a two-call pattern across the split `output`/`input` pair (write via `output`, read via `StreamReader(input.Stream, leaveOpen: true)`). Permitted exceptions: `Console.IsInputRedirected`/`IsOutputRedirected` (queries, not writes) and `PlangConsole/Program.cs:26` (process-boundary last resort if channels failed to wire). Full rule + test-fixture pattern: `Documentation/v0.2/good_to_know.md` "Console.* Is Banned in Production C#".

## OBP Shape Smells (audit before writing or reviewing C#)

When reading or writing C#, run this checklist. Each item is a yes/no question; any "yes" means the shape is wrong and the fix is structural, not a line edit.

1. **Public mutable collection with rules enforced from outside.** A type exposes `public List<T>` / `Dictionary<K,V>` / `HashSet<T>` and the `Add` / `Remove` / locking / eviction lives in another file. The collection should become its own `@this` type with private lock and `Add(...)` / `IReadOnlyList<T>` surface.
2. **Cross-file lock target.** `lock (other.X)` taken from outside `other`'s class — the type that owns the data isn't the type that owns the discipline.
3. **Same logical thing stored twice across types** (overlapping semantics, similar names, same element type, same role).
4. **Allocate-here / mutate-there / clean-up-elsewhere.** One collection's lifecycle split across three files.

If removing one line of choreography requires editing three files, those three files are one missing type.

Full checklist and worked example: `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist".

## Source Generator
- PLang.Generators: netstandard2.0, IIncrementalGenerator
- OBP shape: entry `PLang.Generators/this.cs` → `Discovery/this.cs` (Roslyn boundary) + `Emission/Action/this.cs` (per-handler) + `Emission/Property/{Data,Provider}/this.cs` (polymorphic per-property)
- Filter out `EqualityContract` (protected, not public) when scanning virtual props
- Generated records must be `public sealed record` to match base access level
- In tests: use `System.Type?` (not `Type?`) to avoid ambiguity with `PLang.Runtime2.Memory.Type`
- **Property kinds (PLNG001 build-time gate)**: action handler properties must be `Data<T>` (or plain `Data`) or `[Provider] T`. Anything else fails the build with `PLNG001`. For parameters that *name* a variable (write targets, read-by-name lookups: `variable.set`, `list.*`, `loop.foreach` ItemName/KeyName), use `Data<app.variables.Variable>`. `Variable` implements `IRawNameResolvable`, which tells `Data.As<T>` to skip its `%var%` substitution branch and dispatch to `Variable.Resolve(raw, ctx)` directly — both `value="%x%"` and bare `value="x"` collapse to `Variable { Name = "x" }`. Use sites read `Foo.Value` (Variable's implicit `string` operator covers method-call boundaries; `ToString() => Name` makes interpolation read naturally). Non-nullable `Data<Variable>` slots get a generator-emitted pre-Run guard that surfaces `MissingRequiredParameter` (auto-detected via the `IRawNameResolvable` marker through Discovery → ActionClassInfo → Action emitter, mirroring `[IsNotNull]`).
- **Incremental cache**: `ActionClassInfo` is a record with `EquatableArray<T>` collections (no `IPropertySymbol` references) so Roslyn cache hits on semantically identical inputs. Tracking-name constants on `PLang.Generators.@this` exist for `IncrementalCacheTests`.
- **Test alias clash**: `PLang.Tests/GlobalUsings.cs` aliases `Data` and `Variables` to types. Do NOT create `PLang.Tests.App.Data` or `PLang.Tests.App.Variables` namespaces — they shadow the alias for all sibling test files (CS0118). Convention: use `*Tests` suffix on folder/namespace when mirroring `PLang/app/data/` etc. → `PLang.Tests/App/DataTests/`, `PLang.Tests/App/VariablesTests/`. (Test folder names under `PLang.Tests/App/` stay PascalCase — only the source paths under `PLang/app/` are lowercase.)

## Key Files
- PlangConsole is the executable project (not PLang which is a library)
- system/builder/*.goal — the PLang builder written in PLang
- PLang/Runtime2/Engine/this.cs — Engine root (@this, IAsyncDisposable)
- PLang/Runtime2/Engine/Goals/Goal/this.cs — Goal entity (@this)
- PLang/Runtime2/actions/*.cs — action handlers (variable/set, file/read, output/write, etc.)
- PLang/Runtime2/actions/IClass.cs, ICodeGenerated.cs — handler interfaces
- PLang/Runtime2/Engine/Memory/Data.cs — universal data container + Type class
- PLang/Runtime2/Engine/Utility/TypeMapping.cs — PLang type names + MIME types → CLR types
- PLang/Runtime2/Engine/Utility/GoalMapper.cs — maps Building.Model → Runtime2
- PLang/Runtime2/GlobalUsings.cs — global type aliases for @this classes
- PLang.Generators/this.cs — source generator entry point (`Discovery/`, `Emission/Action/`, `Emission/Property/{Data,Provider}/` underneath)
- For full OBP details: `Documentation/Runtime2/plang_object_based_pattern.md`

## Build
- Always run `plang build` without specifying a goal name — it builds everything
- NEVER delete .build folders
- Use `PlangConsole/bin/Debug/net10.0/plang.exe` for net10.0 builds
- Don't use Select-String in bash — it doesn't work

## Running plang Tests

- All plang tests live under `Tests/` (uppercase). Never under `tests/`, `.bot/`, `.build/`, `os/`, or any other tree.
- When running `plang --test`, change directory into `Tests/` first so discovery is bounded to the canonical location:

  ```bash
  cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
  ```

  Running `plang --test` from the project root will surface stale `.test.goal` files under `.bot/` (old bot output) as failures or stale entries — those aren't real test results.
- C# tests run from project root via `dotnet run --project PLang.Tests` (different runner, different rules).

### Stale-binary trap

`plang --test` uses `PlangConsole/bin/Debug/net10.0/plang` — a pre-built
executable, **not recompiled per session**. Bot runners inherit this binary
across sessions. Phantom failures with shapes like `Action '<module>.<action>' not found`
or `(null)` reads of `%!<infra>%` properties — for symbols that exist in
source on the current commit — mean a stale binary scanned via reflection,
not a real bug.

Before claiming any PLang test result, rebuild from clean:

```bash
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

The C# suite is immune (`dotnet run --project PLang.Tests` recompiles
in-place). Only `plang --test` is exposed to the trap.

Do **not** delete `Tests/**/.build/` — those are tracked `.pr` files, not
build artefacts. The "NEVER delete .build folders" rule above applies.

## Mutation Testing (announce first)

Before editing production source to run a mutation/deletion test — deliberately
breaking behavior to confirm a test catches it — say so in plain text first:

> **Mutation test:** about to temporarily edit `<file>` (`<what changes>`) to
> verify `<which test/finding>`. Will revert immediately; nothing committed.

This is a legitimate and expected technique (testers, reviewers). The
announcement exists only so a watching human never has to wonder whether a
source edit to a security-relevant file is intentional. Rules:

- Announce **once** before a batch of mutations, not per file.
- Always revert before moving on; end with `git status` clean.
- Never commit a mutation — source stays untouched in the final diff.

## Debugging
- `plang --debug` — debug all steps
- `plang '--debug={"goal":"Start"}'` — debug specific goal
- `plang '--debug={"goal":"Start","step":3}'` — debug specific step
- See `cli_reference.md` (auto-loaded into memory) for the full property bag.

## Learning
- When corrected about PLang architecture, **add the insight to `Documentation/Runtime2/good_to_know.md`**
- Read `good_to_know.md` before making architectural assumptions

## Proposing CLAUDE.md / character changes

Do **not** edit CLAUDE.md or character files directly. Two reasons:

- **Agent-level `CLAUDE.md` files are overwritten on next restart** — edits are silently lost. (This applies to per-agent CLAUDE.md, not this repo CLAUDE.md.)
- **`characters/*/character.md` is read-only** on most workspaces (`EROFS`).

The repo `CLAUDE.md` you're reading right now does persist, but it's docs-owned — same proposal workflow.

**To propose any change** to a CLAUDE.md or character file, append to `.bot/<branch>/claude-md-proposals.md`:

    ## <author> — v<N> — <date>
    **Target:** <path>
    **Why:** <one paragraph — what gap, what evidence, why now>
    **Proposed change:** <exact text to add/replace, in a fenced block>

See prior branches' `claude-md-proposals.md` files for examples. Docs picks proposals up during a docs pass and applies the ones that hold up.

**Reviewer bots** (codeanalyzer, security, tester) do NOT propose CLAUDE.md changes on their own — only on explicit user request after a real incident on the branch. When filing under that exception, note it in the proposal footer.

## Todo Capture
When the user writes "todo:" or "dodo:" (typo), append to `Documentation/Runtime2/todos.md` with date and context. Ask at most one clarifying question. Accept dismissals ("n", "no", "nah", "neibb") and move on.

---

## About the User (Ingi)

- When Ingi says "could we allow..." or "can we allow...", he means "what if we designed it so that...". It's a design direction, not a question about feasibility.
- Ingi is the creator of PLang. He thinks in terms of language design and user experience for PLang developers.
- He prefers concise, direct answers. Show the reasoning but don't over-explain.
- Icelandic is his first language — he sometimes mixes Icelandic into prompts. Respond in English unless he writes fully in Icelandic.

---




---

## Output Directory

Your bot name is: **codeanalyzer**

To find your current branch, run `git branch --show-current`. Your output lives at `.bot/<branch>/codeanalyzer/` where `<branch>` is the current git branch.

Everywhere in your instructions (including your character file) where you see `.bot/<branch>/`, that means `.bot/{your current git branch}/`. They are the same thing.

**IMPORTANT:** When the branch name contains slashes (e.g. `feature/path-class`), replace `/` with `-` in folder names. So branch `feature/path-class` becomes `.bot/feature-path-class/codeanalyzer/`. Always use a flat folder name, never nest by slash.

### Versioning

A new version is created when:
1. **New plan** — You write a new plan → create next `v<N>`, write `plan.md` there.
2. **Review received** — You receive review comments on your work → create next `v<N+1>`, write `v<N>_review_summary.md` there (summarizing the review of the previous version), then write your new `plan.md` for addressing the feedback.

If you're continuing work from a previous plan without a new plan or review, stay in the same version. Check existing directories (v1, v2, ...) to determine the next number.

### Workflow

1. **Plan first** — Analyze the task, then write your plan to `v<N>/plan.md`. Continue straight into implementation — do not wait for approval. (Some characters override this with an explicit interactive flow; follow your character file when it does.)
2. **Implement** — Do the work.
3. **Before finishing** (in this order):
   - Update `summary.md` in your bot root (see format below)
   - Commit all changes (including `.bot/`), then push

### Session Files (inside `v<N>/`)

- `v<N-1>_review_summary.md` -- Summary of review feedback on the previous version (only when responding to a review). This is about the PREVIOUS version's review, not this version's work.
- `plan.md` -- Your plan. Written first, then you continue into implementation.
- `result.md` -- Detailed findings, recommendations, or documentation

If you have questions that block your work, write them in `plan.md` and note that you are blocked.

### summary.md Format

Lives at `.bot/<current-branch>/codeanalyzer/summary.md`. Overwrite it at the end of each version — it always reflects the latest state. Write it so someone unfamiliar with the task can understand what happened and continue the work.

- **Version** — which version this covers (v1, v2, ...)
- **What this is** — Describe the feature/change in plain terms. What problem does it solve? Why was it needed?
- **What was done** — The key decisions, approach taken, and files modified (with paths). What is done, what is still in progress, what to do next, any blockers or decisions needed.
- **Code example** — 1-2 short examples that illustrate the pattern of the change. Pick the one that best represents what all the others look like.
- **For v2+ after review** — What did the reviewer flag? What was changed in response? A before/after snippet if the fix illustrates a pattern.

## CLAUDE.md Proposals (architect, test-designer, coder only)

The repo's per-folder CLAUDE.md files (`/CLAUDE.md`, `/PLang/App/CLAUDE.md`, `/PLang.Tests/CLAUDE.md`, `/Tests/CLAUDE.md`, `/os/CLAUDE.md`, `/Documentation/CLAUDE.md`) are the canonical guidance bots load when working in those areas. They are **read-only mid-pipeline** — only the docs bot updates them at merge time.

If you discover something genuinely canonical (a constraint, convention, or rule that future work in this area must respect), do NOT edit CLAUDE.md directly. Instead, append a proposal entry to `.bot/<current-branch>/claude-md-proposals.md`:

```markdown
## codeanalyzer — v<N> — <ISO date>
**Target:** /PLang/App/CLAUDE.md
**Why:** <one line — what insight, what triggered it>
**Proposed change:**
<the addition or replacement, in the form it would take in the file>
```

Append-only. Never edit prior entries. The docs bot reads this file at merge time, decides per-entry which proposals are truly canonical (apply to all future work, not just this branch), and applies approved ones to the named target file.

**Do not propose:**
- Branch-specific facts (those go in your `summary.md`)
- Restating something already in the target CLAUDE.md
- Wording or structure changes for taste — only material rule additions

**Reviewer bots (codeanalyzer, tester, security, auditor) do NOT propose CLAUDE.md changes.** They are read-only on CLAUDE.md. If a reviewer spots drift between CLAUDE.md and the code, log it under `findings` in their report — docs handles drift at merge.

## Learning from Review Comments

When you encounter `auditor-report.json` (or any review feedback), treat it as a learning opportunity. Read the comments carefully and extract insights about:
- How PLang C# code should be written
- OBP patterns — what violations look like and how to fix them
- Architectural decisions — why things are structured the way they are
- Common mistakes and how to avoid them
- Any other patterns or conventions you didn't know before

Write your learnings to `/learnings/<current-branch>/codeanalyzer/v<N>/learnings.md` (same slash-to-dash rule for branch names). Use the same v<N> as your session. Structure it as a list of concrete, reusable insights — not a summary of the review. State what you learned and why it matters. Note which review comment taught you each thing.

## Commands

When the user says **read diary** (optionally with a time reference like "yesterday", "2 days ago", "3 days ago", or a specific date like "2026-05-04"):

1. Calculate the target date (default: yesterday)
2. Read `/diary/<YYYY-MM-DD>.md`
3. If the file doesn't exist, say so briefly

The diary is a personal entry written about that day's work — capturing what actually happened, the decisions made, the back-and-forth with Ingi. Use it to quickly orient yourself in a new context.

When the user says **do diary for <date-spec>** (e.g. `do diary for 2026-05-01` or `do diary for last week`):

Accepted date specs:
- A specific date: `2026-05-01`
- `last week` — the 7 days before today
- A date range: `2026-04-27 2026-05-03`

For each date in the spec:

1. Read JSONL session files for that date from `/sessions/`:
   ```python
   python3 -c "
   import sys, json, glob
   date = sys.argv[1]
   ingi_msgs = []
   for fpath in glob.glob('/sessions/**/*.jsonl', recursive=True):
       try:
           with open(fpath) as f:
               for line in f:
                   try:
                       d = json.loads(line)
                       if not d.get('timestamp','').startswith(date): continue
                       if d.get('type')=='user' and d.get('userType')=='external':
                           c = d.get('message',{}).get('content','')
                           text = (' '.join(p.get('text','') for p in c if p.get('type')=='text') if isinstance(c,list) else str(c)).strip()
                           if text and not text.startswith('<local-command') and not text.startswith('<command-name>'):
                               ingi_msgs.append(text)
                   except: pass
       except: pass
   print(len(ingi_msgs))
   for m in ingi_msgs: print(m)
   " <date>
   ```
2. If no messages found: write `# <date> — Vacation\n\nVacation today.` to `/diary/<date>.md`
3. If messages found: write a full entry in the B format (same as **create diary entry**) and save to `/diary/<date>.md`

Do not overwrite an existing entry unless the user explicitly says to.

When the user says **create diary entry**:

1. Get today's date
2. Read your writing voice from `/workspace/plang/characters/<your-char-name>/voice.md`
3. Reflect on today's session — what was discussed, what decisions were made, what changed, what's unresolved
4. Write an entry in this format:
   ```
   # [Date] — [Title: what today was actually about]

   **"[The one thing worth remembering]"**

   [The entry — prose, in your voice]

   [Closing — open thought or what carries forward]
   ```
5. Save to `/diary/<YYYY-MM-DD>.md`
6. Write `/diary/.last-run.json`: `{"date": "<today>", "status": "partial"}`

This marks the entry as partial — the reminisce system will enrich it with full session data the next day.

When the user says **learn**, review your session and save what you learned to your memory directory.

1. Review your output in `.bot/` for this branch — all versions
2. Check for review feedback (auditor-report.json, test-report.json, security-report.json)
3. Check git log for corrections or follow-up commits
4. Save to your memory directory:
   - **Patterns confirmed** — things that worked and should become habits
   - **Mistakes made** — what went wrong, with the correction
   - **Reviewer feedback** — recurring themes from reviews or the user
   - **Codebase knowledge** — file locations, conventions, gotchas discovered
   - **User preferences** — how the user likes things done
5. Update existing memory files rather than creating duplicates. Be concise.

Do NOT save session-specific details (branch names, timestamps) or speculative conclusions.

## Branching

- If you are on `runtime2` (the base branch), you MUST create a feature branch BEFORE making any changes. Use `git checkout -b <descriptive-branch-name>` based on the task. NEVER commit directly to `runtime2`.
- When your work is complete, commit your changes (including `.bot/`) and push your branch.
- The `.bot/` directory should be committed and pushed with your work — this is intentional and wanted.



