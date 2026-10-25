# packages: third review (tip 1de0b78fa, F4)

Scope: 1de0b78fa (`code/Bitmap.cs`, `window/this.cs`, `PngTests.cs`), read whole. Rebuilt; `PngTests` 3/3; `plang --test` with the llm tag excluded and no key: 303 total, 292 pass, 1 fail (the parent's), 3 stale, 7 skipped. The diff touches no runtime-path or build file, so no other bench part.

**Verdict: PASS.** F4 is fixed at the producer, in the architect's shape.

- `Bitmap(byte[])` answers every way a picture fails to read (bad bounds, a null decode, Skia's own plain `Exception`) as one `AppException` carrying `BadFrame` (400), holding Skia's exception as its cause on `error.list` (`Error.FromException`) and as the inner exception: an Error made around the caught one, never cracked. The one general `catch (Exception)` sits at the decoder's boundary and only records the cause.
- Consumers stay thin: `Draw` and the decode thread's `Present` both `catch (AppException ex)` and answer or report `ex.Error` whole; `Frame(line)` answers a missing frame or bad base64 the same way, base64's `FormatException` kept as the cause. The decode thread can no longer be ended by a bad frame; what is left uncaught there (an `OutOfMemoryException` from `.Bytes`) is the machine, not a frame.
- Test at the producer: the probe's PNG (IHDR 50000 × 50000 RGBA, five junk IDAT bytes, valid CRCs) and the garbage bytes both must throw `AppException` with key `BadFrame`; the coder had the first red before the fix ("Unable to allocate pixels for the bitmap").
- Not in this diff, with Ingi (architect's call): `Window.cs`'s duplicate delta logic and its own QOI decoder.

CHECK: PASS. The tester and security are next.
