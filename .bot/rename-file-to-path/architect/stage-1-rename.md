# Stage 1: Rename file → path

**Goal:** Move the PLang module currently called `file` to `path`, so the catalog name reflects the protocol-agnostic abstraction the type layer already provides.

**Scope:** C# folder + namespace rename; markdown teaching folder rename; `.goal` source sweep in `Tests/`; `.pr` regeneration via `plang build`; documentation sweep; clean rebuild + test verification.

**Deliverables:**
- `PLang/app/modules/path/` exists with the seven handler files; `PLang/app/modules/file/` no longer exists.
- `os/system/modules/path/` exists with all teaching docs; `os/system/modules/file/` no longer exists.
- `module.description.md` reads protocol-agnostic.
- `grep -rIn 'app\.modules\.file\|modules\.file\.' PLang/ Tests/ Documentation/ os/` returns nothing (except possibly in `.bot/` which is fine).
- `grep -rIn 'file\.\(read\|save\|list\|exists\|copy\|move\|delete\)\b' Tests/ Documentation/ os/ --include="*.goal" --include="*.md"` returns nothing.
- All `.pr.json` files regenerated; new ones reference `"module": "path"`.
- `PlangConsole` C# builds clean. `dotnet run --project PLang.Tests` passes. `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` passes after a clean rebuild.

**Dependencies:** None.

## Note to coder

Everything below is one way to do this. The order I propose builds confidence incrementally — namespace bugs surface before they cascade — but if you find a faster path that hits the same Deliverables, take it. The Deliverables list is the contract; the steps are the suggestion.

## Design

### What the source generator does (so you can trust it)

The action catalog derives the module name from the C# namespace: `namespace app.modules.file` → module `file` in the catalog; `namespace app.modules.path` → module `path`. No separate registration table, no attribute that hardcodes the string. Renaming the namespace renames the catalog entry automatically.

That means the rename can be done in one mechanical sweep — no string table to keep in sync, no attribute to update. The only places that *also* mention `file.X` are (a) handler-side comments, (b) C# code that constructs handlers directly, (c) `.goal` sources that explicitly write the action name, and (d) docs.

### Order of operations

The order isn't arbitrary — each step makes the next step's failures readable.

**1. C# rename.**
- Move folder: `git mv PLang/app/modules/file PLang/app/modules/path` (use `git mv` so blame survives).
- In each of the seven handler files (`copy.cs`, `delete.cs`, `exists.cs`, `list.cs`, `move.cs`, `read.cs`, `save.cs`): `namespace app.modules.file;` → `namespace app.modules.path;`.
- `PLang/app/goals/goal/GoalCall.cs:123`: `new modules.file.Read` → `new modules.path.Read`.
- `PLang/app/modules/code/this.cs:251`: stale comment referencing `modules.file.code.IFile`. Update the comment to reference the new namespace, or delete the line — it's referring to already-removed code. Coder's call.

**2. Build C#.**
- `dotnet build PlangConsole` — must succeed with zero errors. Any failure here is a missed namespace reference; fix and rebuild before moving on.

**3. Markdown teaching rename.**
- `git mv os/system/modules/file os/system/modules/path`.
- `os/system/modules/path/module.description.md`: replace the single-line content. Current: *"Read, write, copy, move, delete, and list files through the configured filesystem abstraction"*. Suggested: *"Read, write, copy, move, delete, and list resources addressable by a path — local files, HTTP URLs, and other schemes supported by `path.@this`."* You own the exact wording.
- Sweep all `*.description.md` and `*.examples.md` files under the new `os/system/modules/path/` for the literals `file.read`, `file.save`, `file.list`, `file.exists`, `file.copy`, `file.move`, `file.delete` → `path.<same>`.

**4. `.goal` source sweep in Tests/.**

44 files contain `file.<action>` literals. The full list is in `plan/file-references.md` (write it during the sweep if useful, or work from the grep below). The sweep:

```bash
grep -rIln 'file\.\(read\|save\|list\|exists\|copy\|move\|delete\)\b' \
  Tests/ --include="*.goal"
```

For each file, replace `file.<action>` with `path.<action>`. Watch for the one prose-shape match: `Tests/Modules/Event/Override/Start.goal` line 2 — `- before action file.read call OverrideFileRead`. Same pattern, same fix (`file.read` → `path.read`); the action-name-as-string still needs renaming.

Comments inside `.goal` files (lines starting with `/`) that mention `file.read.Build()` are documentation — update them too for accuracy. Don't leave a `.goal` file with prose claiming `file.read.Build()` does something when the action no longer exists by that name.

**5. Documentation sweep.**

Replace `file.<action>` → `path.<action>` across:
- `Documentation/v0.2/action-catalog.md`
- `Documentation/v0.2/architecture.md`
- `Documentation/v0.2/builder-data-t-roadmap.md`
- `Documentation/v0.2/building-the-builder.md`
- `Documentation/v0.2/code-vs-goals.md`
- `Documentation/v0.2/data-generic-design.md`
- `Documentation/v0.2/debug.md`
- `Documentation/v0.2/execution-flow.md`
- `Documentation/v0.2/filesystem-permission.md`
- `Documentation/v0.2/good_to_know.md`
- `Documentation/v0.2/path-polymorphism-plan.md`
- `Documentation/v0.2/todos.md`
- `Documentation/v0.2/building_plang_tests.md`
- `os/system/actions/v2/summary.md`
- `os/system/builder/BuildGoal/LlmFixer.goal`
- `os/system/modules/http/download.description.md`
- `os/system/modules/variable/set.notes.md`

Be careful in prose: "the file module" → "the path module" (or rephrase). Don't blindly s/file/path/ — `file` appears as an English noun in many places ("read a file", "the file at this URL"), and those readings stay correct. Only swap when `file` is naming the *module*.

**6. Clean rebuild for plang binary.**

```bash
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole
```

This is the stale-binary trap protection — `plang --test` runs the pre-built executable, so without a clean rebuild any reflection-based catalog lookup uses the old `file` name and produces phantom failures.

**7. Regenerate `.pr` files.**

```bash
./PlangConsole/bin/Debug/net10.0/plang build
```

Run from the project root so it picks up every `.goal` source it knows about. Every `.pr.json` should now emit `"module": "path"` for the renamed actions. Spot-check one before moving on.

**8. Run both test suites.**

```bash
dotnet run --project PLang.Tests
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

C# suite first — it's fastest and isolates C#-side regressions. PLang suite second.

### What can go wrong

- **Missed namespace reference** — surfaces as `dotnet build` failure in step 2. Easy to find: the compiler tells you the line.
- **Forgotten `.goal` literal** — surfaces as `plang build` failure ("unknown action `file.read`") or as a test failure with a clear "module not found" message. Search again with the grep from step 4 if it happens.
- **`.pr` not regenerated** — surfaces as a test failure that says `"module": "file"` from a stale file. Re-run `plang build`.
- **Stale binary** — phantom failures with no clear cause. Re-do step 6 from a clean state.
- **Touching `app/types/path/file/`** — this is the `FilePath` *type*, not the module. Leave it alone. The fact that the type namespace contains the word `file` is correct (it's the disk-scheme variant of `path`).

### Things that look like work but aren't

- **No catalog teaching layer changes beyond the file/folder moves.** The `module.description.md` reword is the only content edit. Action-level descriptions (`read.description.md`, etc.) don't need rewording for the rename itself — only sweep them for `file.<action>` literals if any exist.
- **No source generator changes.** The generator already reads the namespace; the rename rides through automatically.
- **No `.bot/` cleanup.** Historical bot output stays as-is. `.bot/` content is by-branch frozen.
- **No CLAUDE.md edits.** Per project convention, propose via `.bot/<branch>/claude-md-proposals.md` if the rename creates a canonical rule worth recording. For this branch, probably not — the rename is the rule, and it lives in the codebase.

## Verification checklist

Run these in order at the end. All must pass.

```bash
# 1. No leftover file-module references in C# or docs (excluding .bot/ history and app/types/path/file/)
grep -rIn 'app\.modules\.file\|modules\.file\.' PLang/ Tests/ Documentation/ os/ \
  --exclude-dir=.bot --exclude-dir=bin --exclude-dir=obj | grep -v 'app/types/path/file'

# 2. No leftover file.<action> in .goal or .md sources
grep -rIn 'file\.\(read\|save\|list\|exists\|copy\|move\|delete\)\b' \
  Tests/ Documentation/ os/ --include="*.goal" --include="*.md" \
  --exclude-dir=.build

# 3. New module is registered (path.read appears in a .pr file)
grep -rIln '"module": "path"' Tests/ --include="*.pr" | head -1

# 4. Clean rebuild + tests
dotnet build PlangConsole
dotnet run --project PLang.Tests
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

If any of the first three return results, the sweep isn't done. If the last two fail, the failure message tells you where to look.
