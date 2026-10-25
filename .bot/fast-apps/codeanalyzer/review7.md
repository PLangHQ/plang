# fast-apps: seventh review — the two files changed after review6 (rule 66, N12) and the full bench row (N15)

Tip 2401279ed. Scope: `module/identity/code/Default.cs` (D4-1's export refusal) and `type/item/image/this.cs` (P1's size gate and the ICC comment), each read whole (378 and 329 lines); the diff since review6 is 23 lines in these two files and nothing else in `PLang`, `PLang.Generators` or `PlangConsole`. The identity slot at six doors (N10) is already on the follow-up. Read only; I did not run `Tools/ObpScan`. The bench ran the tool as it is, then again after a restore (below).

**Verdict on the two files: PASS, with notes.** Nothing blocks by the branch's rules. The new code does what its comments say; the findings are about where the question is asked and a comment that narrates an advisory.

## `module/identity/code/Default.cs` — D4-1's export refusal (`ExportAsync`, `:197-217`)

The check: `ResolveIdentityAsync` answers the identity, then `LoadAll(action)` and `items.Exists(i => PublicKey == exported.PublicKey)`; an identity that is not in the app's own stored rows is refused `IdentityNotOwn` (403) before anyone is asked. It closes the case that matters: with no name, `GetOrCreateDefaultAsync` falls to `action.Context.App.origin.Identity(app)` (`:314`) when nothing is stored, and that answer is the maker's (a sub app's System takes its caller's identity), which an app must not give out. For a named export the identity came from `LoadAll` already, so the check passes trivially; for a stored default it passes; for an origin's it refuses. I traced those three; the behaviour is right.

Not blocking:
1. **A membership test where the resolver should answer "own".** The store is read twice per export (`ResolveIdentityAsync` → `LoadAll`, then `LoadAll` again at `:205`), and "is this mine" is a public-key scan beside the natural call. Fix shape: `ResolveIdentityAsync` (or an export variant of `GetOrCreateDefaultAsync`) never returns the origin's fallback to a caller that hands the key out; the origin step belongs to the signing/read paths, not to export. Then no scan and no second read.
2. **The message is wrong for a root app.** "it is the app's that made this one" names a maker; the code's origin has none (`origin.Identity` for the code is the identity being stored right now, `:311-313`). Say what is true for both: "is not held by this app".
3. **Older, in the whole file (rule 66; none blocking):**
   - `app.actor.list.System.Identity = …` is assigned at six doors (`:31,66,137,180,321,325`): N10, on the follow-up.
   - `making` (`:314`) is a participle for an identity.
   - Verb+noun / noun-as-verb private names: `ResolveIdentityAsync`, `GenerateIdentity`, `LoadAll`, `SaveAsync` (two overloads), `Store`, `Setting`, `Consent`, `Identities`.
   - `Load`/`LoadAll`/`Setting` return `(value, error)` tuples (leaf returns Data).
   - `.Clr<string>()` throughout (`:25,41,46,52,73,95,116,…`): high `.Clr` density, and `:366` lowers a `NowUtc` to `DateTimeOffset` with `Clr`.
   - `:191` an unnamed Data (`new data.@this("", i.Public(), context: …)`).
   - `Consent` takes the verb and the identity name and builds the permission request itself; fine.
   Fix shapes are renames and the typed `Data<…>` carrying the key instead of `Clr<string>`.

## `type/item/image/this.cs` — P1's size gate and the ICC comment (`:308-328`)

The change: `ProbeDimensions` detects the format from the magic bytes (`DetectFormat`) before any decoder runs and refuses a format outside `{JPEG, PNG, GIF, BMP, WEBP}` with `ImageSizeUnread` (415) as an `AppException`; `Identify` runs only for those. TIFF is excluded on purpose (a BigTIFF IFD count can hold the decoder), and nothing reads the ICC profile, metadata or EXIF. I read it whole; the gate does what it says and the throw comes from a member with no Data return (`Width`/`Height` are `int` properties), which the error rule allows.

Not blocking:
1. **A second list of formats, by literal name, beside the type's own.** `Sized` (`:313`) is five format names; the type declares eight `[Format]` attributes (`:19-26`: jpg, png, gif, bmp, tif, svg, webp, heic). So `image` declares tif, svg and heic and refuses to size them (and ImageSharp cannot identify svg or heic at all). The two lists will drift. Fix shape: the format says whether its size is read (a fact on the `[Format]` attribute or the kind), and `ProbeDimensions` asks the detected format's kind; no literal list in the value.
2. **The comment narrates an advisory and a version that doesn't match.** `:308-312` cites `GHSA-gwg2-r3hj-4w44` and says "nothing reads them while ImageSharp is at or below 4.1.1", while the project is on 2.1.13: either the version is wrong or the comment means "fixed in 4.1.2". The code states what is; the advisory and the version are provenance (git blame, the security notes). Fix shape: keep the invariant ("an image's ICC profile, metadata and EXIF are never parsed; its size is read from the header of the formats it names") and drop the advisory id and the version bound.
3. **A static in production.** `private static readonly HashSet<string> Sized` — "a constant: readonly, never written", the same case ruled for the system variables' table (70), so allowed; a `HashSet` is still mutable in type. `FrozenSet<string>` (or a `switch` on the name) would make it a constant in fact. Merged with 1 if the formats answer for themselves.
4. **`Width` and `Height` each run `ProbeDimensions()`** (`:121,124`): two `DetectFormat` + `Identify` calls per image; one probe cached as a pair. `_width`/`_height` are `int?` assigned with `??=`: a struct, so a torn read under a parallel first access can answer a wrong value once (the `bool? _isName` pattern from review3).
5. **To verify, not claimed:** a plang read of `%image.width%` on a TIFF now throws `AppException` from a property getter. I did not trace whether property navigation answers `ex.Error` or lets it escape; if it escapes, the gate turns a refused image into an unhandled exception.
6. **Older, in the whole file (rule 66; none blocking):**
   - `Open` (`:95-100`) makes a throwaway `new data.@this("", this, context: context)` named `loading` only to call `Value(loading)` and read `.Error` (the throwaway-Data pattern, unnamed `""`).
   - Two stacked `<summary>` blocks at `:102-106` sit above no member (an orphaned doc comment).
   - `ValidateKind` is the `IKindValidatable` member (B list, known); it also carries a `_ => Bytes` default arm and a `catch … when (ex is UnknownImageFormatException or InvalidImageContentException)` type switch (`:302`).
   - `CheckStrictKind` returns `(bool ok, string? actualKind)?`, a nullable tuple.
   - The leaf unwrap `((value as item)?.Clr<object>() ?? value)` is written at `:137` and `:153` (raw hand-off).
   - `Mime` answers the string `"application/octet-stream"` for an image with no kind.
   Fix shapes: the throwaway goes by a Data-returning door; the orphan summary is deleted; the rest are the known follow-ups.

## Locked core
Nothing public added. `Sized`, `ProbeDimensions` are private; `ExportAsync`'s signature is unchanged. `image` is not one of the core value types.

## The bench row (N15) — `Tools/Bench/run.sh` on 2401279ed

Two runs, because the first found a stale restore (below). Rows appended to `Tools/Bench/record.md`: `review7-tip` (full) and `review7-after-restore` (build, `plang --test`, sweep). Against stage 5 (cc78ec913) and review4's full row (02aff152e), bands from the file's top:

| Number | Stage 5 | review4 row | Now | Band | Reading |
|---|---|---|---|---|---|
| Sub birth | 0.15 ms, 128 KB | 0.14 ms, 110 KB | **0.14 ms, 110 KB** | ±0.02 / ±5 | in band, stable |
| Root birth | 18.4–20.2 ms, 2,357–2,377 KB | 18.48 ms, 2,488 KB | **17.90 ms, 2,358 KB** | ±1.5 / ±25 | in band (N1's fix holds) |
| Sub request | 2.0–2.3 ms, 603–614 KB | 1.85 ms, 541 KB | **1.82 ms, 540 KB** | ±0.15 / ±10 | lower, stable |
| Root request | 17.2–18.3 ms, 3,636–3,858 KB | 16.56 ms, 3,720 KB | **16.68 ms, 3,821 KB** | ±1 / ±120 | in band |
| Held, sub (MB), 0/1k/2k/3k/rest | 2.4 → 3.0 → 3.0 → 3.0 → 2.6 | 1.6 → 2.1 → 2.1 → 2.1 → 1.9 | **1.6 → 2.1 → 2.1 → 2.1 → 1.9** | ±0.1; flat = last three within 0.2 | **flat**, identical to review4 |
| Held, root (MB) | 0.9 → 2.8 → 2.7 → 2.7 → 1.7 | 0.1 → 2.0 → 1.9 → 1.9 → 1.1 | **0.1 → 1.9 → 1.9 → 1.9 → 1.1** | same | **flat** |
| `plang --test` | 4.7–5.6 s | 5.6 s (298 pass, 1 fail) | **5.3 s (298 pass, 1 fail)** | ±0.5 s | in band, same counts |
| C# sweep | 40.6–54.7 s (red: Wire) | 37.3 s (red: Wire) | **40.5 s (red: Wire, 6)** | ±7 s | in band, the same red names |
| Build, incremental | 11.5 s | 9.1 s | **8.6 s** | ±1.2 s | below the band (good) |
| Build, clean | quiet 33.8–53.8 s (the tester, decision 89) | 90.5 s, rerun 74.9 s (load 13–16) | **75.7 s** (starts at load 2.81) | ±3 s quiet | **above the quiet range; not settled** |

- **Held memory is flat in both modes and unchanged from review4: nothing grows with the request count.** Birth, requests and held are the valid, quiet-machine numbers (the full row started at load 2.49).
- **The clean build is the open number.** 75.7 s, with the load climbing to 6.9 by the end as a neighbour started work (the 1-minute average lags); the tester's quiet range is 33.8–53.8 s. I cannot say from this run that it is slower, and I cannot say it isn't. The tester's rows are the reference; it needs another run on a machine nobody else is building on.
- **The first full row's `plang --test`, sweep and build columns are void** (0 tests ran; every suite red; a 162.5 s "clean" build). Cause, verified: `Microsoft.Data.Sqlite, Version=9.0.21.0` could not be loaded at run time (`FileNotFoundException`, `store/sqlite/this.cs:329`), because this checkout's test and console projects still had `Microsoft.Data.Sqlite/9.0.2` in their restored `project.assets.json` and `PlangConsole`/the test bins held the 9.0.2 DLL. The build reported 0 errors. `dev.sh` restores a project only when its own project file changed; decision 89 changed `PLang.csproj`'s package, which the dependents read through a project reference, so a tree that built before 89 fails at run time. `dotnet restore` of the seven test projects and `PLangConsole` fixed it; the second run is the valid one. **A tooling gap, not an OBP finding:** `dev.sh` (and the bench) should restore a project when a project it references changed its packages, or build with `-restore`; else any tree older than 89 shows every suite red with a green build.
- `plang --test` leaves `test/app/permission/grantastorespersisted/probe-question.txt` modified in the working tree (the test rewrites its own tracked probe file: "read" became "write"); I restored it with `git checkout`. A test writing a tracked file is the tester's to fix.

## For the coder / architect
1. Optional, small: finding 1 of each file (the resolver answers "own"; the format answers whether its size is read), the two comments (the wrong "maker" message; the advisory and version), finding 4 (one probe) and 5 (verify the getter's throw).
2. `dev.sh`/the bench: restore dependents when a referenced project's packages change.
3. Another quiet-machine clean-build row (the tester's).
