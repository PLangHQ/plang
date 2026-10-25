# security v1 — packages (tip c3d9c2f50, parent runtime2 55302f8a5)

Verdict: PASS (no critical/high). One Medium (availability, not a regression), nothing else. Probes: `PackagesPassProbeTests` (11; 10 green, 1 red by design).

## Finding
- **S1 (Medium, availability) SkiaSharp decodes a frame at the size its header claims, with no bound.** `PLang/app/module/screen/type/screen/code/Bitmap.cs:17-35`: `DecodeBounds` then `Decode(..., info.Width, info.Height)`; reached from `window/this.cs:121` + `:154` (a browser page's frame line) and `:133` (bytes on the window's channel). Probe `ABlankPng_ThatClaims8000x8000_IsRefusedBeforeItsPixelsAreAllocated` (red): a 60 KB PNG took the process peak working set up 504 MB (~8000x amplification); 30000x30000 is ~3.6 GB native plus the managed `decoded.Bytes` copy. `decoded.Bytes` (Bitmap.cs:34) sits outside the try, so an OutOfMemory/Overflow there is not an AppException and ends the decode thread (the catch in `Present` takes AppException only). Not a regression (ImageSharp decoded at claimed size too). Fix: refuse a frame whose `DecodeBounds` exceeds the screen's size (or a fixed pixel count) as BadFrame before `Decode`; keep `Bytes` inside the guard. Also, unchanged and Windows-only: `window/code/Window.cs:236,305,320` multiply message-supplied `w*h` (int) for QOI parts: with Ingi with the rest of Window.cs.

## Checked clean
1. NSec 26.4.0: Ed25519 sign/verify code unchanged (`signing/code/Ed25519.cs:159-207`); probes on the library: RFC 8032 vector verifies, tampered message fails, non-canonical S (S+L) refused, small-order key with zero signature refused, 63-byte signature false without throwing.
2. BouncyCastle `KeccakDigest(256)` (`crypto/type/hash/kind/keccak256/this.cs`): empty and "abc" known answers; chunked feeding equals one shot (original Keccak padding, not FIPS).
3. Skia: see S1. Failed reads answer AppException BadFrame; the decode thread reports it.
4. The image type holds no image library; `ValidateKind`/`Sniff` are magic bytes only (`image/this.cs:274-276`, `this.Parse.cs`): a classification, not a decode; a polyglot passes as its first magic (accepted, as before).
5. Fluid 2.40 includes (`module/ui/code/Fluid.cs` provider): `../` forms (to a sibling folder, deep `..` to /tmp) render no content; a name by a variable is IncludeNotLiteral (also a variable spelling `./p/ok` for a literally included `p/ok`); a literal in-root include renders with no ask. Every probe/read still goes through `path.Exists`/`path.Bytes` (the gate).
6. Vulnerable table, all 19 csproj, read whole: "has no vulnerable packages" for each.

Not probed: Windows/macOS Skia native assets; a case-insensitive disk.
