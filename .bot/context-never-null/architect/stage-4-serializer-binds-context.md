# Stage 4: Serializer & store bind context — one read path

**Goal:** Thread context into every serializer so `Wire._context` is non-null, collapse the two read narrows into the single Typed-reader path, and retire `ContextLessFallback`. This is the core of the branch — where the cache double-wrap stops being possible.
**Scope:** Mechanism C. Included: `Wire._context`, the two json converters, the settings/snapshot/transport/crypto sites, the `_context` gates and the trust branch, `View`'s role, the hash body-only canonicalization. Excluded: the authenticity check and `verify.Root` (Stage 5) — this stage keeps verify behavior as "verify signature, skip freshness on Store" without the identity match.
**Deliverables:**
- `data/Wire.cs` — `_context` non-null (`:73`). Drop the `_context != null` guards on the three typed-read branches (`:386,406,419`); the context-less `Parse → RawSlot` fallback for typed values becomes dead and goes. Delete the `if (_context == null) { … }` trust/fail-closed block in `ReadSignatureLayer` (`:211-227`). Pass `typeRef` whole to `Readers.Typed(typeRef)` (`:407`), not `.Name` + `.Kind`.
- `channel/serializer/json/converter.cs` — `_context` non-null (`:24,47`).
- `settings/Sqlite.cs:20` — `_serializer = new(context)` (the System context — see plan: settings is system-owned). Ctor change touches `DataSourceTests`, `AbsoluteDisciplineTests`, `SqliteAuthorizeDenialTests`.
- `channel/serializer/plang/this.cs` — snapshot `_snapshot` Wire built with the ctor's context; delete `ContextLessFallback` (`:141`).
- `snapshot/this.Wire.cs` — retire the 2-arg `FromWire(raw, kind)` seam (`:83`) for a context-ful read registration; drop `?? ContextLessFallback.SnapshotOptions` (`:89`).
- `data/this.Transport.cs:58,142` — require the (now non-null) actor; drop `?? ContextLessFallback`.
- `module/crypto/code/Default.cs:22` — hashing routes through the crypto module's context; no `ContextLessFallback`. The hash canonicalizer must use a **body-only write** (not sign-if-missing) to avoid sign recursion.
- `View`: keep only the freshness role (`SkipFreshnessCheck = (View == Store)`); it no longer decides whether verify runs.
**Dependencies:** Stage 1 (non-null actor/context), Stage 3 (values carry context, so a context-ful read produces correctly-born values).

## Design

The store binds the serializer, and the serializer *is* the MIME. The settings/identity store is `application/plang` by construction, so its reads verify and its writes sign — the null-context-means-trust coupling was a context-less serializer leaking in. Once `_context` is non-null, `Wire.ReadBody` routes every typed value through the Typed reader, so a nested `{type:{name},value}` entry is always born to its type whether it came from a `.pr` load or a settings read. That single read path is what removes the cache double-wrap.

Turn on the tripwire in Stage 6, not here — but write this stage so it *would* pass: no Store read reaches `ReadBody` with a null context.

Full detail: `plan/mime-and-verify.md`.

## You own this

The context-ful read registration seam, the body-only write entry, and the exact ctor signatures are yours. The contract: `ContextLessFallback` is gone, `Wire._context` is non-null, there is one typed read path, and `View` only modulates freshness.
