# codeanalyzer summary (packages)

**Version:** v2

**What this is:** The packages branch (NuGet bumps, ImageSharp replaced by SkiaSharp, Nethereum and Newtonsoft replaced by BouncyCastle, Fluid.cs reshaped, native trims, ObpScan). v1 (review.md) failed on three small findings; v2 (review2.md) re-reads the coder's fixes at tip 1510726b5.

**What was done:** v1: every touched file read whole, 15 commits run (identical), llm goals once, tests red under mutation, bench in band. v2: F1 (a `code/Bitmap.cs` owns the screen's pixels), F2 (TIFF tests) and F3 (usings) are fixed. New finding F4: the window's decode thread now catches only `FormatException`, and Skia throws `System.Exception` on a PNG with a valid header and junk data (probed), which would end the host; wrap Skia's failure as `FormatException` in `Bitmap`'s reader and pin it in `PngTests`. Also: `Tools/Bench/run.sh` now rests 0 s by default, spends nothing on OpenAI, and says which parts to run for which diff (about 5 minutes whole).

**Next:** the coder fixes F4; a short re-read of `Bitmap.cs` and `PngTests`, then the tester and security. Window.cs's `Move`/`Copy` are stored twice beside `Shown` (older, Windows-only): the architect rules fold or defer.

**Code example:**
```csharp
// the producer says what its failure is, once; both callers keep catch (FormatException)   screen/code/Bitmap.cs
internal Bitmap(byte[] encoded)
```
