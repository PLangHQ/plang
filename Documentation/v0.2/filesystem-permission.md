# Filesystem Permission System

The gate between PLang code and the host filesystem. Every file/directory
operation runs `path.Authorize(verb)` before touching disk, and only an
explicit user consent — or a previously-stored grant — lets the I/O
proceed.

This doc covers the **shape** of the system. For the OBP rationale behind
the `Verb` variant design, see `good_to_know.md` § "OBP Variant Design".

## What the prompts guard

A permission prompt guards what a goal does **as the User actor**: the reads, writes and deletes outside the root that the User would otherwise make unasked. It does not guard against the app's own code. An app can choose to run a goal as the System actor (`call goal X` naming it), and it can wire its own channels, an input that answers the asks included (consent is asked of the actor's input channel, so replacing that channel redirects every ask). So running an app means trusting its code; the prompts protect the user from what the User actor would do unasked, not from the app itself.

Consent comes from the actor's input channel only: never from a variable (`%!ask.answer%` is the stateless-resume sentinel and answers nothing here), and not from the `on.ask` event, which fires but does not answer.

## Concepts

```
app.filesystem.permission/
├── this.cs                     -- PermissionRecord + Match enum
├── verb/
│   ├── this.cs                 -- Verb @this (composes Read/Write/Delete)
│   ├── Read.cs                 -- record Read(bool Recursive, bool Metadata)
│   ├── Write.cs                -- record Write(bool Create, bool Overwrite, bool Append, bool Mkdir)
│   └── Delete.cs               -- record Delete(bool Recursive, bool Permanent)
└── (Match: exact | glob)

app.actor.permission/
└── this.cs                     -- per-actor Find/Add/Revoke

app.type.item.path/this.Authorize.cs -- the gate (Authorize, Standing, Request, Keep)
```

**PermissionRecord** — `(Actor, Path, Verb, Match)`. Identity is the
three-tuple `(Actor + Path + Verb)`. `Match` decides how `Path` is
interpreted: `exact` (the path as written) or `glob` (see "Grant patterns").

**Verb** — always-present sub-options for Read/Write/Delete. A grant's
`Verb.Covers(request.Verb)` answers "does this grant cover this request?"
by per-sub-option boolean ≥. Defaulted to full capability so a hand-written
`new Verb { Read = new Read() }` means "all of Read."

## Authorize flow

`path.Authorize(verb)` is the single entry point. Every FS handler
(`file.read`, `file.save`, `file.copy`, `file.move`, `file.delete`,
`file.exists`, `file.list`) calls it before any I/O.

```
1. The actor is the caller's (context.Actor).
2. Place: where the verb acts (see "Where a verb is judged"). A place the gate
   can't tell (its links loop, or run past 40) → LinkLoop (403), for every door.
3. Standing(verb) on the place:
     in the root                      → Ok()   (app.filesystem.root, minus the os folder and the stores' folder)
     actor.Permission.Find(place, verb) → Ok()  (a grant it holds covers)
     in the stores' folder, no grant  → PermissionDenied, nobody is asked
4. named && asked by os → Ok(), nothing stored  (a step of a goal that ships with plang, naming the path itself)
5. Ask the actor's input:
     "Allow {actor} to {verb} {abs}? (y/n/a)"
     y → keep the grant on the place, in memory for the run
     a → keep it persisted: signed as it crosses into the settings' store
     n → Fail(PermissionDenied)
     other → re-prompt with "Invalid answer '{x}'." prefix
```

The y/a split is what the user sees at the prompt; the actor's permission view
(`actor.Permission`) keeps grants in **three homes**, behind one `Find`:

- **Standing** — what the actor's role stands on the app's folders (the os
  folder, the stores' folder). In memory for the run, never asked, never
  persisted; only a `Stand` replaces one, and no revoke reaches it.
- **Once ("y")** — unsigned, in memory, until the app exits. Kept beside the
  standing grants, never in their place: a "y" on a place a standing grant
  covers adds a grant, it does not replace it.
- **Persisted ("a")** — the actor's own permission setting
  (`%!app.actor.permission.setting%`), one row holding all its saved grants,
  signed whole when saved and verified whole when read (a tampered row reads as
  no grants). Survives `new app()` on the same root.

`Find` walks standing, once, then persisted. `Add(grant, persist)` routes by
the user's answer and answers the save's result: an "always" whose save failed
is not a yes. The path verbs return that failure, one-verb and move/copy doors
alike, and do the I/O only after every grant is kept. The same `Path` twice
overwrites in its own home.

Only "nobody could answer" is a no, answered as `PermissionDenied` holding the
cause: a cancelled ask, an input whose stream closed or transport broke, or one
that says it is unanswered. Any other failure inside the ask surfaces as its own
error (through `Error.FromException`), never as `PermissionDenied`.

## In-root short-circuit

`IsInRoot()` auto-grants any place under `app.filesystem.root` (`%!app.filesystem.root%`, the app's folder, what `/` is) **except** two folders. The os folder (`app.filesystem.os`, `%!app.filesystem.os%`, what ships with plang: the system goals, the type and module teaching) is outside it: what an actor may do there is its standing grants, so every actor reads and runs, and only the system writes. The stores' folder is outside it too (below). A goal under the os folder that names a path itself is trusted by its origin (`named`); a path it was handed is asked as any other.

### The stores' folder — `.data/`

`<root>/.data/` holds the app's stores and each identity's key. It is the system's: the system reaches it through a standing grant, and any other actor holding no grant is refused, never asked. A move never carries a folder that holds its place, whether the stores are there yet or not.

plang makes `.data/` on an app's first run. An app folder that arrives with one is used as it is, its identity and its settings included.

The stores' folder is recognised case-blind, as a case-blind disk opens it (`.DATA` is `.data` there).

The folder is its owner's alone: opening a store sets it to 0700 (`store/sqlite/this.cs:109-114`), so a key saved in it is no other OS user's to read. The mode is on the folder, so it holds from the first byte and covers the `-wal` and `-shm` files beside the database; a folder made earlier is set on its next open. On Windows nothing is set (a folder takes ACLs there). A mode does not survive git or a zip, so a copied app folder is set again when it is opened.

## Grant patterns

A grant is `exact` or `glob` (`permission/this.cs:97-160`). A grant with no wildcard is compared as a whole; a glob reads three wildcards:

| Wildcard | Matches |
|---|---|
| `*` | any run within one part: it stops at a separator |
| `**` | any run across parts |
| `?` | one character of a part |

A part ends at each of the OS's separators: `/` on Linux, `\` and `/` on Windows. So `/apps/*/file.txt` covers `/apps/a/file.txt` and not `/apps/a/b/file.txt`, and `https://api.example.com/*` covers `https://api.example.com/users` and not `https://api.example.com/users/1`.

The literal text between wildcards is compared the way the exact grant is: a file grant ignores case where the disk does (Windows) and is exact elsewhere; a URL grant never ignores case, on any OS. A glob never folds case wider than the exact grant. The match is one forward pass over the pattern's pieces, so a pattern full of `*` does not slow down on a long path.

A path a goal writes is the same on every OS: `/` between its parts. A Windows `\` in it reads as `/`, and a path the runtime hands back (`Parent`, `Combine`, a changed extension) never gains a `\`. The OS form, with the OS's own separator, is what the gate judges and what reaches the disk.

## Where a verb is judged

The gate judges a verb where the OS acts for it. The door that does the act names the place; the gate's inner `Authorize(verb, place, …)` takes it, and the question, the standing check and a yes's grant are all on that place.

| Act | Place | Reads as |
|---|---|---|
| open: read, write, execute, list, stat | `Location` | where the OS lands for the path: each link followed, the last one too |
| delete; a move's two ends | `Spot` | the entry itself: its folder's landing plus its own name, never followed — a link is removed or replaced, never what it leads to |
| a copy's two ends | `Location` | the links are followed (a copy opens its source and writes through a link at its destination) |

The rest follows from that:

- **A move's source is asked read and delete.** A rename takes the name away (delete), and what the folder holds is reachable where it lands (read). A yes keeps one grant on that place, holding both verbs.
- **A grant is on the place asked about, for the verbs asked there.** A delete grant on a link never answers a read or write of its target. One grant per place: a place's grant replaces the one before on it.
- **A trailing separator names no other place.** `dl/` is the entry `dl`; the string judged is the string handed to the OS (a root keeps its separator).
- **Deleting a folder link removes the link alone.** It never recurses into the target.
- **A file is a tree of one.** A file copied or moved into an existing folder is judged as that one entry under the folder's landing, by the same `Refusal` a folder's tree gets, before anything is touched. A move's destination that is anything else (a file, a link, nothing) is the entry itself, replaced.
- **A link in a tree is an entry of its own.** A copy re-creates it as written, a delete removes it, a move renames it; nothing reads through it. A recursive delete asks the gate for every entry first, then removes each alone, children first.
- **Ask once, check all, then do.** A copy, a move or a recursive delete asks the user once at the top (the answer covers all), and asks the gate about every entry it touches, both ends, before anything is touched. An entry the gate refuses refuses the whole verb, so there is no half-copy. `list` leaves out the entries the gate refuses.
- **The anchors are landed once.** `app.filesystem.root`, `.os` and the stores' folder are paths, each landed once and kept for the app; the one containment question is the path's own `IsUnder`, which asks `root.Holds(location)` (`path/this.cs`): the folder itself, or a location past a separator (`/root2` is not under `/root`). A path still being resolved asks `Holds` too (`path/file/this.Validate.cs`), with the root's comparison, so the rule is one wherever it is asked.
- **An unpacked link points inside its folder.** An archive unpack refuses a link whose target, read as the OS reads it, leaves the folder it was made in.
- **A place that can't be told is refused.** Links that loop or run past 40 answer `LinkLoop` (403) at every door.

## Find — two-pass lookup

`actor.Permission.Find(path, verb)` builds a synthetic `PermissionRecord`
request and walks both homes:

1. In-memory list (snapshot under `_lock`, verify outside the lock so
   `VerifySignature` doesn't hold the actor lock).
2. Sqlite via `SettingsStore.GetAll<Data<PermissionRecord>>("permission")`,
   filtered client-side to `grant.Actor == _actor.Name`.

For each candidate, `TryCover` runs `grant.Covers(request)` and then —
if the grant has a signature — verifies it. Verification is cached in
the `Data` instance's `Properties` bag under `permission.verified`, so
repeat `Find` calls on the **same** `Data<PermissionRecord>` skip re-verify.
Sqlite grants always re-verify because `SettingsStore.GetAll` yields a
fresh `Data` per call.

## Grant verification — a stored read

A grant is read from plang's own store (`View.Store`), and the signature reader gives a signature its live window only
when it is read off the wire. So a grant's signature is born with origin `stored` (`%x!signature.origin%`) and no window:

| Step | What | For a grant |
|---|---|---|
| 1 | Expired (`%x!signature.expired%`) | only what its signer signed (`Expires == null` → permanent) |
| 2 | Nonce replay | **not checked** — not a live read |
| 3 | Contract check | yes |
| 4 | Data-hash check | yes, in the Store view |
| 5 | Ed25519 cryptographic signature | yes |

A live read's window (`%!signing.setting.expiry%`, 5 minutes) would expire "always allow" after 5 minutes, and a
persisted grant re-presents the same nonce on every read — that is not a replay. Neither applies, because what decides
both is how the signature was read, born with it, not a flag a caller passes. Step 5 (the Ed25519 signature) is the core
integrity gate; forgery still requires the actor's private key. A wire message read live gets the window and the nonce
check: wire anti-replay is intact.

Regression coverage:
- `Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow` advances `NowUtc` past the live window and re-reads.
- `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt` reads twice with a stateless channel; each `Find`
  re-deserializes the grant, so each read is a real verify.

## Revoke

`Revoke(record)` removes from in-memory by `(Actor + Path)` and from
sqlite by `Path` key. Returns true if either home dropped a row.
`Scenario5_RevokeReprompts` covers the round-trip: revoke + next read
fires a fresh prompt.

## Known follow-ups (non-blocking)

These were called out by the reviewer chain and deferred deliberately.
They are tracked in `coder/v6/result.md`.

- **`RootComparison` thread-through.** `Path.cs:125,127` and
  `PLangFileSystem.cs:254` still use raw `OrdinalIgnoreCase`. (auditor F-C)
- **`Add` dedup-by-Path overwrites different-verb grants.** The class
  doc claims `(Actor + Path + Verb)` identity, but `Add` dedups by
  `Path` alone. Granting `Read` then `Write` on the same path drops the
  `Read` grant. Either the doc or the dedup key needs to move; the
  audit pass left it because no test pins it.
- **Bundled-consent prompts in `copy`/`move`.** `MoveCopyBundledConsentTests`
  exercises bundled consent on the v2 `Path` surface; the production
  `copy.cs`/`move.cs` handlers still issue two prompts. (auditor F-5,
  deferred with F-C/D/E.)
- **`output.ask` is text-only**, which forces the awkward
  `BuildRequest`/`SignAndStore` shape in `path.Authorize.cs` (the
  permission is built twice — once to format the question, once to seal
  on the answer). When `output.ask` grows structured options, the
  permission becomes a first-class option, defined once. Tracked in
  `Documentation/v0.2/todos.md`.

## File map

| What | Where |
|---|---|
| Authorize gate (the entry point) | `PLang/app/type/item/path/this.Authorize.cs` |
| Where a file verb acts (`Location`, `Spot`, the transfer walk) | `PLang/app/type/item/path/file/this.Operations.cs` |
| PermissionRecord + Match | `PLang/app/filesystem/permission/this.cs` |
| Verb (Read/Write/Delete) | `PLang/app/filesystem/permission/verb/` |
| Per-actor Find/Add/Revoke | `PLang/app/actor/permission/this.cs` |
| Permission denied error | `PLang/app/errors/PermissionDenied.cs` |
| Ed25519 verify (5+3 step pipeline) | `PLang/app/modules/signing/` |
| Tests | `PLang.Tests/App/FileSystem/Stage5MessagesEndToEndTests.cs` and siblings |

For the wider type catalog see `app-tree.md` (Actor.Permission line).
For OBP rationale on the Verb variant shape see `good_to_know.md`
§ "OBP Variant Design".
