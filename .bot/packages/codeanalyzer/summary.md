# codeanalyzer summary (packages)

**Version:** v3

**What this is:** The packages branch (NuGet bumps, SkiaSharp for ImageSharp, BouncyCastle for Nethereum/Newtonsoft, Fluid.cs reshaped, native trims, ObpScan). v1 FAIL (F1–F3), v2 FAIL (F4), v3 PASS.

**What was done:** v1 read every touched file whole, ran the 15 commits (identical), the llm goals once, tests red under mutation, bench in band. v2 found F4 (Skia throws a plain Exception on a junk-data PNG; the decode thread then ends the host). v3: `Bitmap` answers every failed read as an AppException carrying BadFrame with the decoder's exception as its cause; Draw and the decode thread answer `ex.Error` whole; PngTests 3/3 with the probe PNG; `plang --test` 292 pass, 1 fail (the parent's). `Tools/Bench/run.sh` now rests 0 s, spends nothing on OpenAI, and runs the diff's parts (about 5 min).

**Next:** the tester and security. Window.cs's duplicate delta logic and QOI decoder are with Ingi (follow-up).

**Code example:**
```csharp
throw new global::app.error.AppException(error, cause);   // screen/code/Bitmap.cs: the producer says what its failure is, once
```
