# Data Envelope Architecture — Coder Summary

**v1** — Phase 1: Created `Engine.Types` class consolidating PLang name ↔ CLR type, extension → Kind/MIME, and compressibility data into a single live instance on Engine. Additive change — old static TypeMapping untouched. 62 tests pass. See [v1/summary.md](v1/summary.md) for details.

**v2** — Phase 2: Type gets context + lazy derivation. Type navigates to Engine.Types through context for Kind, Compressible, ClrType (falls back to static TypeMapping when contextless). Data gets late-bound context with lazy Type derivation. MemoryStack/PLangContext propagate context to all Data automatically. 23 new tests, 1233 total Runtime2 tests pass. See [v2/summary.md](v2/summary.md) for details.

**v3** — Phase 3: Split Data.cs into 4 partial class files by concern (core, result, navigation, envelope). Added `Out` view to View enum with `[Out]` attribute for transport serialization. Tagged Properties and Signature with `[Out]`. Added Signature (byte[]?) and Verified (bool?) stub properties for Phase 4. 8 new tests, 1313 total pass. See [v3/summary.md](v3/summary.md) for details.

**v3.5** — Tester fixes: Fixed Add/KindOf pipeline bug (Add() now updates _allKinds/_mimeToKind), null guards on Kind()/Mime(), Name() backtick stripping for generics, BuilderNames/ComplexSchemas tests. 17 new tests, 1330 total pass.

**v4** — Phase 4: Envelope pipeline methods on Data: Wrap (kind envelope from Type.Kind), Compress (GZip if compressible), Encrypt (pass-through — no crypto yet), Decrypt/Decompress/Unwrap (inbound reverse). Includes JSON rehydration for nested Data after deserialization. Full pipeline round-trip: `data.Wrap().Compress().Encrypt()` ↔ `received.Decrypt().Decompress().Unwrap()`. 17 new tests, 1347 total pass. See [v4/summary.md](v4/summary.md) for details.

**v5** — Security hardening: depth limits on all 5 unbounded recursive methods (UnwrapJsonElement, RehydrateNestedData, GetChild, Clr, ResolveVariablesInPath), deduplicated fromJson.cs, Verified → private set with internal SetVerified(), zip bomb test, Merge tests, StatusCode assertions. 12 new C# tests, 1384 total pass. Added 3 PLang integration test suites (DeepNavigation, VariableIndexing, FromJson) — 17 PLang tests pass. See [v5/summary.md](v5/summary.md) for details.
