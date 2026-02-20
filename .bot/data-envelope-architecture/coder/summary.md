# Data Envelope Architecture — Coder Summary

**v1** — Phase 1: Created `Engine.Types` class consolidating PLang name ↔ CLR type, extension → Kind/MIME, and compressibility data into a single live instance on Engine. Additive change — old static TypeMapping untouched. 62 tests pass. See [v1/summary.md](v1/summary.md) for details.

**v2** — Phase 2: Type gets context + lazy derivation. Type navigates to Engine.Types through context for Kind, Compressible, ClrType (falls back to static TypeMapping when contextless). Data gets late-bound context with lazy Type derivation. MemoryStack/PLangContext propagate context to all Data automatically. 23 new tests, 1233 total Runtime2 tests pass. See [v2/summary.md](v2/summary.md) for details.

**v3** — Phase 3: Split Data.cs into 4 partial class files by concern (core, result, navigation, envelope). Added `Out` view to View enum with `[Out]` attribute for transport serialization. Tagged Properties and Signature with `[Out]`. Added Signature (byte[]?) and Verified (bool?) stub properties for Phase 4. 8 new tests, 1313 total pass. See [v3/summary.md](v3/summary.md) for details.
