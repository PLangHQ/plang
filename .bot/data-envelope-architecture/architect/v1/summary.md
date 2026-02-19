# v1 Summary — Data Envelope Architecture

## What this is

Design spec for making Data self-describing at IO boundaries. When Data leaves the PLang runtime (io.write over TCP to another plang instance), it needs to carry its own type routing, support automatic signing, optional compression, and optional encryption — all as transparent envelope layers that peel off on the receiving side.

## What was done

Architectural design session producing a four-phase plan:

1. **Engine.Types** — Replace three scattered type-mapping sources (static `TypeMapping`, `FileModule.TypeMapping`, `MimeTypeHelper`) with a single live instance on Engine. Owns PLang names, CLR types, MIME types, extensions, Kinds, compressibility. Extensible at runtime via Add/Remove.

2. **Type gets context + lazy derivation** — Type stops being a dumb string wrapper. Gets a context reference, navigates lazily to `Engine.Types` for `.Kind`, `.Compressible`, `.ClrType`. Data gets late-bound context (stamped by pipeline, inherited by children). Type derivation in Data becomes fully lazy — no eager creation on Value set.

3. **Data restructure + Out view** — Split Data.cs into four partial classes (core, result, navigation, envelope). New `Out` view attribute for transport-only serialization — Properties and Signature only serialize when Data is leaving the system.

4. **Envelope pipeline** — Data gains Wrap/Compress/Encrypt/Decrypt/Decompress/Unwrap. Two-Data envelope pattern: outer Data routes by Kind, inner Data carries handler-specific details. Pipeline methods navigate through context to action handlers. Each method is OBP — inspects itself, decides whether to act.

## Key design decisions

- **Late-bound context on Data** — static factories (`Data.Ok()`, `Data.FromError()`) survive unchanged, context stamped by pipeline after creation
- **Kind not Category** — `.Kind` on Type for the content category ("image", "spreadsheet", "encrypted")
- **Out view** — new serialization view for the IO boundary, keeps envelope metadata invisible inside the runtime
- **OBP rules documented** — no verbs in APIs, navigate through context, lazy properties, instance not static, one-word noun names

## Files produced

- `v1/plan.md` — full four-phase design spec with source file references for the coder
- `v1/DATA_DEEP_DIVE.md` — comprehensive reference doc on current Data class (from handover)
- `v1/DATA_ENVELOPE_ARCHITECTURE.md` — original envelope design with examples (from handover)
- `v1/DataEnvelopePipelineExample.cs` — code examples for the pipeline (from handover)
- `learnings/data-envelope-architecture/architect/v1/learnings.md` — OBP learnings from the session
