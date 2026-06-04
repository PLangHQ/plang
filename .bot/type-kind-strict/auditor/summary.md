# auditor — type-kind-strict

## v1 — **NEEDS WORK** → next: **coder**. HEAD `e847bf8ff`.

Audited the merged `type-kind-strict` + `lazy-deserialize` state. Upstream coverage clean (no production code after codeanalyzer v3 / tester v13 / security v1). I walked the strict×lazy seam codeanalyzer v3 traced as CLEAN and verified the in-file claims line-by-line — they hold. The gap is one step further out:

**F1 (Major) — Path-backed strict-kind enforcement is unreachable from any production caller.** `image.BytesAsync()` is the sole entry point that throws `StrictKindMismatchException`; `grep BytesAsync\\(\\)` returns zero production callers outside the method's own definition. All consumers (`image/serializer/{Default,text,protobuf}.cs`, `Width`/`Height`/`ProbeDimensions`, `AsBooleanAsync`) read sync `value.Bytes`, which returns `Array.Empty<byte>()` for a path-backed image (`_bytes == null`). So `set %img% = "wrong.png" as image/gif strict` followed by `write out %img%` silently emits empty bytes and never enforces strict. The stage-9 contract ("content loads from the path on first access; strict throws at load") is structurally present but never triggered by any real flow. Upstream missed it because each bot stopped at the in-file seam: codeanalyzer trusted the deferral; tester's goal tests stop at `assert Type.Kind == gif` and punt enforcement to a C# unit test that calls `BytesAsync` directly; security audited under the assumption enforcement fires.

Three fix options laid out in the report — option 1 (await `BytesAsync` in image leaf serializers) matches the architect's "async load seam" framing.
