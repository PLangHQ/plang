# Stage 9: Runtime — reference fundamentals are lazy path-handles

> **Coder: you own the final shape.** Settled (with Ingi): `- set %x% = "file.jpg" as image` mints an `image` whose `.Path` is set and **reads nothing** — content materializes from the path on first access. The async wrinkle below is the one thing to get right; method shapes are yours. **Mutation/save (e.g. resizing) is explicitly parked — do not design it here.**

**Goal:** A reference fundamental declared from a path is a lazy handle: `variable.set` mints the typed value with `.Path` set and performs no I/O; the content (bytes, decode, dimensions) loads from `.Path` only when first needed.
**Scope:** Included — `image` gains path-backed lazy construction (the proving instance; `audio`/`video` follow the same pattern); `variable.set` runtime mints the typed handle for a path-string + reference-fundamental declaration instead of the current string carve-out; the lazy content-load seam. Excluded — **mutation, divergence-from-file, and save** (parked); the build-time stamping (stage 8 owns `as image` → `{image, jpg}` at build); `path` itself (already lazy by nature).
**Dependencies:** Stage 8 (the reference-fundamental category and the `as image` build stamp). The `image` type already exists (lazy `Width`/`Height` from in-memory bytes); this stage makes the *bytes themselves* lazy when path-backed.

## Why

`- set %x% = "file.jpg" as image` should give you an image you can carry around for free and only pay for when you use it. Reading the file at the `set` is wasted work if the next step never touches the pixels — and it's the same lazy stance as "file read returns raw, materialize on access." A reference fundamental is a handle: `.Path` now, content later.

## Where the code diverges today

1. **`image` must be built with bytes.** Its constructor is `@this(byte[] bytes, string mime, path? path = null)` — `Bytes` is eagerly stored, `Path` is optional provenance. There is no path-only, bytes-lazy image. (`Width`/`Height` are already lazy, but they probe *already-loaded* `Bytes`.)
2. **`variable.set` doesn't mint an image for `as image`.** When `"file.jpg"` won't convert to `image.@this` (no bytes), the runtime falls into a carve-out that mints a plain `Data<string>` annotated as image — so `%x%` is a *string typed image*, not an image with `.Path` set. That carve-out is what this stage replaces.

## Design

### `image` — path-backed and lazy

Add a path-backed construction: an `image` built from a `path` alone, with `.Path` set and **no bytes loaded**. `Bytes` becomes lazy — when the image is path-backed and bytes haven't been loaded, the first content access reads them from `.Path`; cache once (mirrors how `Width`/`Height` already memoize). A bytes-backed image (network fetch, base64 decode, `Path = null`) is unchanged — bytes are already in hand.

So an `image` has two origins, one shape:
- **bytes-backed** (today): bytes in memory, `Path` optional provenance.
- **path-backed** (new): `Path` set, bytes lazy from it.

`audio`/`video` are the same pattern; `image` is the proving instance — build it so the lazy-from-path mechanism isn't image-specific where it doesn't need to be.

### `variable.set` runtime — mint the handle, don't read

When the declared type is a reference fundamental and the value is a path-string, mint the **typed handle** with `.Path` set from the resolved path — replacing the current "mint as `Data<string>`" carve-out. No read. The value of `%x%` is now an `image` (path-backed), not a string.

### The async wrinkle — call it out, don't trip on it

Loading bytes from a path is **I/O**, and in this codebase all path I/O is **async** (it goes through `FilePath.AuthGate` — the actor permission gate). But `Bytes`/`Width`/`Height` are **sync** getters today (they work because the bytes are already in memory). A path-backed image can't honor a sync `Bytes` getter that needs to read the file — that would force sync-over-async and bypass the async auth gate.

So the lazy content surface for a path-backed reference fundamental must be **async** (an `await`-able content accessor), the same way `IBooleanResolvable` made the condition pipeline async because path existence is I/O. Coder decides the exact shape (an async `LoadAsync()`/`BytesAsync()`, or making the content accessors async), but the constraint is firm: **the lazy load is async and must pass through the path's auth gate — never a blocking read in a sync getter.** Sync `Width`/`Height` stay valid only for the already-in-memory (bytes-backed) case.

### Errors surface late, but validate the cheap thing early

Because nothing is read at the `set`, a missing file or an undecodable image surfaces at **first content access**, not at the declaration. That's consistent with the lazy model and acceptable — but validate what's free: a malformed *path string* can fail at the `set` (cheap, no I/O); existence and decode stay deferred to first use.

## Deliverables

- `image` path-backed construction: built from a `path`, `.Path` set, `Bytes` lazy-loaded from it on first content access (cached); bytes-backed construction unchanged.
- An async content-load seam through the path's auth gate (no sync-over-async).
- `variable.set` runtime: `as <reference-fundamental>` + path-string → mint the typed path-handle, no read (replaces the `Data<string>` carve-out).
- Tests: `set %x% = "file.jpg" as image` → `%x%` is an `image`, `.Path` is `file.jpg`, **no file read occurred** (assert via a read-counting path or a non-existent file that does NOT error at set); first access to content triggers exactly one async load through the auth gate; a malformed path string still errors at set.

## Parked (do not implement here)

Mutating loaded content (`set the width of %x% to 200`) makes the value diverge from its backing file and raises the lifecycle question — origin vs destination `.Path`, copy-on-write, when/where it saves. Out of scope for this stage by Ingi's call. Capture it as a follow-up when we get there.
