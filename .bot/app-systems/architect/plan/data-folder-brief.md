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
  identity/<id>/                                    one identity's own; reads fall back to the app level
```

The folder is named for the concept, `identity` (Ingi: "so it is just /.data/identity/%identity%/file.txt"). The developer writes the path:

```
- save %report% to "/.data/identity/%Identity%/report.pdf"   → .data/identity/<id>/report.pdf
%!app.actor.user.identity.list%                              the identities that have reached this app
```

(Decision 207, Ingi: "it is the public key = %Identity% in plang code. yes, I think user.identity would go into .data/identites/%identity%/data.sqlite". So `%Identity%` is the user identity's **public key** (text). In a local run, with no caller, it falls back to the system's identity. An identity's own store is `.data/identity/%Identity%/setting/data.sqlite` (singular folder, with the kind level he settled earlier). The key was standard base64 (`signing/code/Ed25519.cs:177`), with `/` in about half of all keys. **Settled (decision 208, Ingi: "(a)"): public keys are written URL-safe base64** (`-`/`_`, no padding), so `%Identity%` is a safe path segment and URL part as it is.)

(Settled, decision 205, Ingi: "there should be a dynamicdata for %Identity%, %Identity% is the user.identity, %MyIdentity% is the system.identity". The pattern is `/.data/identity/%Identity%/…`. `%MyIdentity%` is already a DynamicData (`actor/this.cs:109`); `%Identity%` is new. The kind level inside an identity's folder is settled above.)

- **The boundary follows where a value came from, not who is running** (Ingi: "if we change /change.txt, that is coming from source … it never came from the wire"; "we can check when we are writing file down to disk, did this come from the wire or source").
  - The check lives at the path's write gate (`AuthGate`), which every file verb already passes through.
  - Source (written in the program) goes where the developer said, under the `/.data/` rule.
  - From the wire (identity X), it's kept inside `.data/identity/X/`, and `../..` can't climb out.
  - **Mixed** (`"/.data/file/%filename%"` with X's filename): any wire part makes it the sender's.
- **What it takes:** a value carries its **origin**, and derived values inherit it (a rendered template takes the origin of the variables it used). Today a verified wire value drops its signature (`data/this.Transport.cs:24-27`; `wire/kind/plang/this.cs:81-83`), so in memory it can't tell where it came from. This is a small first step of `Documentation/Runtime2/cool.md`'s "Causal lineage" (only the outside origin, not the full graph).

- **Simplified (Ingi: "it's more of a convention, a pattern to follow"):** the path decides where a file goes, always, with no magic. `.data/` is the taught pattern (`/.data/file/…`, `/.data/identity/%!identity%/file/…`), not forced on every write. Only two guards, both about origin:
  - **(a)** a path, or part of one, that came from the wire is kept inside its identity's `file/`, so `../..` can't climb out;
  - **(b)** content from the wire can't be written where code lives (`.build/`, `.goal`, `os/`).
- **Saving into a folder (Ingi's next idea):** `save %!user.data% to folder, write to %path%`. `file.save` returns the path it saved to (decision 179), so `%path.name%` is the final name, and `read file %path%` reads it back. The file takes the value's own name (the upload's filename, as a sanitised leaf). On a name clash it doesn't overwrite silently: the identity is **asked** (overwrite, or add a counter), and **the answer is stored as that identity's setting**, so it isn't asked again. That combines plang's ask, settings and identity.

## Open

1. **Settled (Ingi): "they all have identity, cant write if they dont have identity."** Every writer has an identity. A value whose origin has no identity can't be written to disk. Still to settle: which identity each kind of input carries. A signed wire request carries its signer's. An `ask` answer carries the local user's. An LLM's answer and a url's content: the provider's, or the asking actor's? The latter would put them in the asker's folder.
2. **The id on disk:** a short fingerprint of the key as the folder name, with the full key in the identity (lean).
3. ~~Callers without an identity~~: settled in 1, no identity means no write.
4. **Naming:** `actor.Identity` today means the actor's own signing keys, while "the user's identities" means the callers. That's one word for "who I am" and "who came to me"; name them apart.
5. Is `cache/` per identity or shared at the app level (the LLM cache shared saves cost)? Does `trace/` move out of `.build/traces/`?
6. Setup's executed steps belong to the app level. Once each part owns its storage, what's left of `store`?

## Today (2026-09-28)

- `app/this.cs:605-618`: the one store, `/.db/system.sqlite` (in memory while testing).
- `BuildGoal/Start.goal:21`: traces under `/.build/traces/`.
- Plang content between actors is signed, and unsigned content is refused (formats (e), decision 130).
- The parked "variable storage by identity" work is the same axis.
