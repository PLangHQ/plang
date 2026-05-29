# CLAUDE.md proposals — data-serialize-cleanup

## coder — v1 — 2026-05-27
**Target:** /PLang/App/CLAUDE.md
**Why:** Stage 5 of data-serialize-cleanup scrubbed "envelope" from the codebase
because the word was an active conceptual leak — it implied Data lived inside a
container, which made readers reach for parallel container types (the deleted
`Envelope` class in `plang/Data.cs` is the load-bearing example). Future
contributors should not reintroduce the concept.

**Proposed change:**
- **Data is not enveloped.** Data IS the wire shape — `{name, type, value, properties, signature}`. Do not introduce parallel wrapper types ("Envelope", "Wire", "Wrapper") for Data's serialization shape; the wire shape is Data's own shape with `[Out]` filtering. If you find yourself building a parallel type to bypass `[JsonIgnore]`, the right answer is to add an `[Out]`-aware filter and let the existing `app.data.WireJsonConverter` handle the wire layout.

(Coder filing this under the reviewer-bot exception clause: explicit user
request to land all five stages of data-serialize-cleanup. The Envelope class
was actually deleted on this branch.)
