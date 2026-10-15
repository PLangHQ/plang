# docs — summary

## Version
v1

## What this is

Runtime2 diverged hard from the last docs pass, and the old docs
(`Documentation/architecture.md` especially) are stale. This starts a **fresh,
code-mirrored developer doc tree** aimed at the **C# developer** working on
Runtime2 — not the PLang user writing `.goal` files (that is a later pass).

The docs live **inside the code tree**: each folder's `start.md` is its overview,
sitting next to the folder's `this.cs`. This realizes one rule —
**path = namespace = doc location** — the same convention plang uses (`start.md`
is a folder's doc because `Start.goal` is the entry point). Every page answers:
*what is this, where is the source, how does the plang I write reach this C#?*

v1 covers the fundamentals Ingi named: the **app root** and the **module system**,
with the **file module** as the worked example.

## What was done

Three docs written, all verified against current source (not copied from the
stale `architecture.md`):

- **`PLang/app/start.md`** — the "how to read this codebase" primer + the app
  root. Sections: the one root (`app.@this`, and `app` *is* an `item`); path =
  namespace = plang name; the `/this.cs` + `@this` pattern and the `GlobalUsings`
  aliases; nodes-lowercase / verbs-PascalCase; the collection-node pattern
  (`app.X` / `["name"]` / `.list` / `.current`); what `app` holds (member table);
  how `item.list` works (the value lattice + native `list<T>`); `Data` as the
  universal carrier; and a plang→C#→source cheat-sheet. Serves as the tree index.
- **`PLang/app/module/start.md`** — the module system: what a module is, the
  registry (selection + lifecycle only), discovery (`Discover` + `[Action]`, the
  module name = namespace segment after `app.module`), the action handler pattern,
  the attribute reference, where the runtime teaching prose lives (points to
  `os/system/modules/<module>/`, does **not** duplicate it), dispatch from a built
  `.pr` step to `Start()` (real `decide.pr` shown), and "the type is the role".
- **`PLang/app/module/file/start.md`** — the `file` module end to end: the seven
  actions mapped (plang → file → `[Action]` → return type → cacheable), `read` as
  the canonical handler walkthrough, `exists` as "existence is the path's
  truthiness" (`IBooleanResolvable`, stat/HEAD), and the mutating actions' shared
  `From` + `Use` shape.

### Key design decisions (with Ingi, this session)

1. **Docs live in the code tree** (`PLang/app/...`), not a separate
   `Documentation/` mirror. Ingi: "why not put them in plang/app/". A concept's
   doc is in the concept's own folder, beside its source.
2. **`start.md` per folder** + named topic files when one page isn't enough.
3. **Depth approved** — the `PLang/app/start.md` level of per-section detail is
   the target for the rest.
4. **Dev docs point, don't duplicate** the runtime teaching markdown under
   `os/system/modules/` (different audience: the builder's LLM vs. the C# dev).

## Code example — the pattern every page follows

Each concept is explained as *what → where → plang mapping*. The spine:

```
PLang/app/module/file/read.cs     the file on disk
        app.module.file           the C# namespace (top of the file)
            file.read             the plang action ([Action("read")])
```

and a built step records it, so lookup is mechanical:

```json
{ "module": "file", "name": "read" }   // from the .pr  ->  PLang/app/module/file/read.cs
```

## What's next (later versions)

- `PLang/app/type/start.md` + `type/item/start.md` — the type system and the
  value lattice in full (v1 only covers `item.list` as a conventions section).
- `start.md` for `goal`, `data`, `channel`, `event`, `actor` as the tree grows.
- Retire / redirect the stale `Documentation/architecture.md` once the in-tree
  docs cover its ground.

## Blockers

None. Fundamentals (app + module) are complete and accurate for their scope.
