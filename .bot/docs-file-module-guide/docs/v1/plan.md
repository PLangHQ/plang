# v1 — Fundamentals: the C# developer's guide to Runtime2

## The task (Ingi, 2026-10-14)

Runtime2 is a huge departure from the last docs pass; the docs are all out of
date. Restructure them into a tree that **mirrors the code**, with `start.md` as
each folder's entry doc (because `Start.goal` is plang's entry point, so
`start.md` is a folder's doc). This pass is aimed at the **C# developer**: always
show plang code and how it maps to C#, and how to find the source. Get the
fundamentals stable first; the PLang-developer audience comes later.

> "look how path of .cs file match namespaces, and the /this.cs pattern, how
> item.list works. analyze the .cs code base, understand it."

## Decisions (confirmed with Ingi)

- **Doc location:** the docs live **inside the code tree** — `PLang/app/start.md`
  beside `PLang/app/this.cs`, `PLang/app/module/start.md` beside `module/this.cs`,
  and so on. (Ingi: "why not put them in plang/app/".) This realizes
  path = namespace = doc location: a concept's doc is in the concept's own folder.
  Matches the plang convention that a folder's docs are `start.md`. The initial
  `Documentation/app/start.md` draft was moved to `PLang/app/start.md`.
- **Naming:** `start.md` is each folder's entry/overview doc; a folder may also
  hold **named topic files** when one page isn't enough.
- The legacy `Documentation/v0.2/*.md` stay as reference until superseded.
- **Audience:** C# developer working *on* Runtime2 — not the PLang user writing
  `.goal` files. Every section answers "what is this, where is the source, and
  how does the plang I write reach this C#?"

## Scope of v1 (fundamentals Ingi named: app + module)

The branch is `docs/file-module-guide`; the `file` module is the worked example
for the module system — so the branch name and the fundamentals pass line up.

1. **`PLang/app/start.md`** — the reading-the-codebase primer + the app
   root. The one doc a new C# dev reads first. Sections:
   - The one root: `app.@this` in `PLang/app/this.cs`. `app` *is* an `item`.
   - **Path = namespace.** `PLang/app/module/file/read.cs` ↔ `namespace
     app.module.file` ↔ plang `file.read`. The rule, with the recipe for going
     from a plang step to its source file.
   - **The `/this.cs` + `@this` pattern.** Every folder's primary class is
     `@this` in `this.cs`; consumers use the `GlobalUsings.cs` aliases.
   - **Nodes lowercase, verbs PascalCase** — what plang navigates vs. where C#
     executes.
   - **The collection-node pattern:** `app.X` (the collection), `app.X["name"]`
     (select), `app.X.list` (enumerate), `app.X.current` (the one in play; only
     some concepts have it). Registry = selection + lifecycle; behaviour on the
     element.
   - **How `item.list` works:** the native `type.item.list.@this<T>`, and how a
     domain `X.list` is one. Why there are no plurals.
   - **Everything is `Data`** (brief) — the universal carrier; `Data<T>`; link
     out, don't duplicate the data-spec.
   - **Find the source** — a cheat-sheet: plang path → namespace → file.
   - Serves as the index for the whole `Documentation/app/` tree.

2. **`PLang/app/module/start.md`** — the module system.
   - What a module is (`module.@this`): one vocabulary of actions; the app's
     module (`%!app.module%`) is the empty registry holding `list`.
   - **Discovery:** `Discover(assembly)` scans `[Action]` classes; the namespace
     segment after `app.module` is the module name; an external DLL via
     `module.add`.
   - **The handler pattern:** `[Action("read")] partial class Read : IContext`,
     lazy `data.@this<T>` params (source-generator-filled), `Start()` the entry,
     `Build()` the build-time check, `.Use(...)` to open carriers.
   - **Dispatch:** a `.pr` step carries `"module"`+`"name"` → the registry mints
     the catalog element → `action.Start(context)`. Show the real `decide.pr`.
   - The attributes a handler uses (`[Action]`, `[Default]`, `[IsNotNull]`,
     `[Code]`, `[Whole]`) — the C# shape; prose lives in markdown.
   - **Find an action's source** recipe.

3. **`PLang/app/module/file/start.md`** — the `file` module worked
   example: its seven actions (copy, delete, exists, list, move, read, save),
   each mapped plang step → `.cs` file → what it does. `file.read` read in full
   as the canonical handler walk-through.

Deeper subsystems (`type`, `data`, `goal`, `channel`, `event`, …) get their own
`start.md` in later versions. "How `item.list` works" is covered as a
conventions section in `app/start.md`; the full type-system doc is a later pass.

## Proposals to evaluate

- `.bot/<branch>/claude-md-proposals.md` — none on this branch (new branch).
- `.bot/<branch>/character-proposals.md` — none on this branch.

(If either appears, each gets an apply/reject/defer line here.)

## Not in scope

- No PLang `.goal` examples authored as tests (tester's job) — but inline plang
  snippets that illustrate the mapping are expected and required.
- No edits to production `.cs` XML docs this version unless a gap blocks the
  fundamentals story; the three source files read (app, module, file.read) are
  already well-commented.
- Legacy `Documentation/v0.2/*.md` left in place; `app/start.md` links to the
  ones still accurate (data-spec, obp-smells, object_pattern_formal) rather than
  copying them.

## Verification

Every structural claim is checked against the source I read this session:
`PLang/app/this.cs`, `PLang/app/module/this.cs`, `PLang/app/module/file/read.cs`,
`PLang/app/module/Attributes.cs`, `PLang/app/GlobalUsings.cs`,
`PLang/app/type/item/this.cs`, and the built `decide.pr`. No claim copied from the
stale `architecture.md`.
