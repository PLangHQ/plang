# Parked brief: `.data/`, what the app keeps, per owner and per identity (after app-systems)

Ingi, 2026-09-28 (a curious-architect conversation):
- "that .db folder should be named .data folder and in there is db, file, setting, etc.";
- "settings/data.sqlite … put setting down to user … .data/%user.id%/setting/ (|file|cache|trace) … default what is above";
- "developer needs to say /.data/file/admin/my/…; you mostly program now, so it needs to be a rule, now it often end in .db folder";
- "taught, yes and like runtime";
- "if input it coming from wire, it is per identity? … user has list of identities and then we can reach into each to get the files from each identity".

## Settled so far

- **Everything the running app keeps lives under `/.data/<kind>/…`:** `setting/data.sqlite`, `file/`, `cache/`, `trace/`, perhaps `db/`. `.db/` disappears (its sqlite becomes `.data/setting/data.sqlite`; the rest moves to its own folder). The app folder is what ships (goals, `.build/`, templates).
- **No magic mapping.** The developer writes the real path: `save %report% to "/.data/file/admin/my/report.pdf"`.
- **A rule that is taught *and* kept by the runtime.**
  - Taught in the plang docs, the skill and CLAUDE.md, where bots learn plang; the file module's examples use `/.data/file/…`.
  - Kept by the runtime: path authorisation lets a program running as the User actor write only under `/.data/`, so writing its own goals or `.build/` is refused or prompted like an out-of-root write. The System actor (builder, setup) writes where it must.
  - A program can't rewrite its own code. Deploying means replacing the app folder and keeping `.data/`; a backup is a copy of `.data/`.
- **Per identity, for input from the wire:** a request's caller is the identity in its signature (plang content between actors is signed). Each identity has its own folder with the same shape, and the app level above is its default:

```
.data/
  setting/data.sqlite  file/  cache/  trace/        the app level (defaults)
  user/<id>/
    setting/data.sqlite  file/  cache/  trace/      one identity's own; reads fall back to the app level
```

```
%!app.actor.user.identity.list%                     the identities that have reached this app
%!app.actor.user.identity["<id>"].file%            ↔ .data/user/<id>/file/
%!identity%                                         the identity of the request running now
- save %report% to %!identity.file%/report.pdf      → .data/user/<caller id>/file/report.pdf
```

- **The boundary follows where a value came from, not who is running** (Ingi: "if we change /change.txt, that is coming from source … it never came from the wire"; "we can check when we are writing file down to disk, did this come from the wire or source").
  - The check lives at the path's write gate (`AuthGate`), which every file verb already passes through.
  - Source (written in the program) goes where the developer said, under the `/.data/` rule.
  - From the wire (identity X), it's kept inside `.data/user/X/`, and `../..` can't climb out.
  - **Mixed** (`"/.data/file/%filename%"` with X's filename): any wire part makes it the sender's.
- **What it takes:** a value carries its **origin**, and derived values inherit it (a rendered template takes the origin of the variables it used). Today a verified wire value drops its signature (`data/this.Transport.cs:24-27`; `wire/kind/plang/this.cs:81-83`), so in memory it can't tell where it came from. This is a small first step of `Documentation/Runtime2/cool.md`'s "Causal lineage" (only the outside origin, not the full graph).

## Open

1. **Input without an identity** (an LLM's answer, an unsigned web request, an `ask` answer, a file's or url's content): where may it write? The app level `.data/file/`, a sandbox such as `.data/input/`, or nowhere without the System actor? (Asked. Lean: two kinds of value, source and input; input never writes outside `.data/` or into another identity's folder.)
2. **The id on disk:** a short fingerprint of the key as the folder name, with the full key in the identity (lean).
3. **Callers without an identity** (an unsigned browser request): no folder, the app level only, or refused?
4. **Naming:** `actor.Identity` today means the actor's own signing keys, while "the user's identities" means the callers. That's one word for "who I am" and "who came to me"; name them apart.
5. Is `cache/` per identity or shared at the app level (the LLM cache shared saves cost)? Does `trace/` move out of `.build/traces/`?
6. Setup's executed steps belong to the app level. Once each part owns its storage, what's left of `store`?

## Today (2026-09-28)

- `app/this.cs:605-618`: the one store, `/.db/system.sqlite` (in memory while testing).
- `BuildGoal/Start.goal:21`: traces under `/.build/traces/`.
- Plang content between actors is signed, and unsigned content is refused (formats (e), decision 130).
- The parked "variable storage by identity" work is the same axis.
