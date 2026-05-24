# Coder plan — compile-llm-notes-per-action

This branch moves per-action LLM teaching (Notes, Examples, Description) out of the C# action-handler attributes and into markdown files at `os/system/modules/<module>/`. The Compile-step system prompt loses ~5 KB of action-specific text; each step's user message gains only the teaching for actions the planner picked.

## Read first

1. **`.bot/compile-llm-notes-per-action/builder/notes-per-action.md`** — builder's brief; the why and the migration map. 5-minute read.
2. **`.bot/compile-llm-notes-per-action/architect/plan.md`** — design decisions on top of the brief. **This is the canonical plan; everything below is just navigation.**
3. `Documentation/v0.2/good_to_know.md` and `Documentation/v0.2/architecture.md` — auto-loaded, but worth a fresh read.

## Order of work

The 7-step sequence is in `plan.md` under "Order of work (suggested)". Follow it; do not reorder unless you find a hard dependency the architect missed.

| Step | Owns | Approx size |
|---|---|---|
| 0 | Rename legacy PascalCase `os/system/modules/*Module/` folders to lowercase (`AiModule/` → `ai/`, …). Fix path references. Flag ambiguous mappings before proceeding. | Small |
| 1 | Loader: `Modules.Describe()` reads `os/system/modules/<module>/{module,<action>}.{notes,examples,description}.md` into catalog entries | Medium |
| 2 | Renderer: per-action Description/Notes/Examples blocks in `stepActionDetails.template` (or its equivalent — locate it) | Small |
| 3 | Migration script: extract `[Example]` + `[Description]` from `PLang/app/modules/<module>/<action>.cs` → markdown files; delete the attribute usages | Medium |
| 4 | Author the new Notes files by hand from `plan.md`'s migration table (this is content work, not code) | Medium |
| 5 | Delete the corresponding action-specific sections from `os/system/builder/llm/Compile.llm` | Small |
| 6 | Run verification (see below) | Small |
| 7 | Orphan-file validation at catalog load | Small |
| 8 | Rename `[Provider]` → `[Code]` (attribute + source generator + PLNG001 text + `CLAUDE.md`) | Small, mechanical |

## Things easy to get wrong

- **Legacy PascalCase `*Module/` folders are renamed to lowercase in step 0** (`AiModule/` → `ai/`, `OutputModule/` → `output/`, etc.). Content there is deprecated; rename, don't delete, unless you find it's already dead. Path references in `.goal` / `.llm` / `.cs` files need to follow. Flag any ambiguous mapping (e.g. `EventsModule/` plural vs `PLang/app/modules/event/` singular) before guessing.
- **`module.` is the reserved prefix.** `module.notes.md` is module-wide. Bare `notes.md` is not a valid file in this scheme — don't accept it as a fallback.
- **Concat merge, module first.** `module.notes.md` text precedes `<action>.notes.md` text in the rendered output, separated by a blank line. Not override; not action-first.
- **Render only for actions in the planner's set.** Same path as today's per-step action details. Modifiers (`error.handle`, `cache.wrap`, `timeout.after`) render through this path uniformly; no special case.
- **All prose moves.** `[Description]` leaves the C# class along with `[Example]`. Don't half-migrate.
- **Do not turn `Describe()` into a plang goal in this pass.** `plan.md` "Loader" section is explicit; that is a separate, later refactor. The win here is C# reading files.
- **Cross-cutting kernel stays in `Compile.llm`.** Do not delete anything not listed in `plan.md`'s "What to delete from `Compile.llm`" table.
- **Path resolution.** Reuse the existing helper that resolves `os/system/builder/llm/Compile.llm`. Do not introduce a new convention.

## Conventions to know

- **OBP folder layout:** singular folder name, `@this` is the type. See `CLAUDE.md` "OBP Shape Smells".
- **No `Console.*` writes in production C#.** The orphan-file warning at startup uses `app.CurrentActor.Channels.WriteTextAsync(Output, …)`, not `Console.WriteLine`. See `CLAUDE.md` "Console.* Is Banned in Production C#".
- **Stale-binary trap:** `plang --test` uses `PlangConsole/bin/Debug/net10.0/plang`. Rebuild from clean (`rm -rf` the bin/obj trees, `dotnet build PlangConsole`) before claiming any `plang --test` result. C# tests are immune.
- **PLNG001 rename in this branch:** `[Provider]` → `[Code]` across the attribute, the source generator, the PLNG001 build-warning text, every action-handler call site, and the `CLAUDE.md` paragraph that mentions `[Provider] T`. Mechanical rename, no behavior change. Step 8 in the order of work.

## Verification

From `plan.md` and the brief, unchanged:

1. **System prompt size on a `Tests/Simple` step compile drops from ~20.8 KB to ~15 KB.** Trace inspection.
2. **The two drift cases are fixed end-to-end, across 3 fresh-cache builds of `Tests/Simple`:**
   - `write out %message%` → `formal='output.write(Data=%message%)'`. No `channel=%!data%`.
   - `assert %message% equals 'hello plang'` → `Message` omitted from `parameters[]` and from `formal`; `Expected='hello plang'` matches between `formal` and `parameters[]`.

Test-designer is writing failing tests that pin these. Make them pass.

## Out of scope

Named in `plan.md`; do not expand:

- Turning `Describe()` into a plang goal.
- Touching `Plan.llm`.
- Adding a structural validator for "no extra parameters in `formal` vs `actions`".
- Renaming or relocating any C# action handler files.
- Hot-reload of the markdown files mid-build.
