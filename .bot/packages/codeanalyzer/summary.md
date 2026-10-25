# codeanalyzer summary (packages)

**Version:** v1

**What this is:** The review of the packages branch (NuGet bumps, ImageSharp replaced by SkiaSharp, Nethereum and Newtonsoft replaced by BouncyCastle, Fluid.cs reshaped, action.Return's race fixed, native trims, NSubstitute and two old projects out, and the three ObpScan commits). Tip 63a299efb, parent 55302f8a5. Report: `.bot/packages/codeanalyzer/review.md`.

**What was done:** Read whole every touched production file; ran the 15 commits one by one (build, `plang --test` with the llm tag excluded and no OpenAI key: identical on every commit); ran the llm goals once at the tip (6 pass); mutated Fluid.cs, keccak and `Pixels` to show the three new tests are red; ran the bench at the tip (in band; clean build 30.9 s; bins 50 → 29 MB; publish 27/29 MB). Verdict FAIL on three small findings: F1 the window's `Pixels` is a stray helper over a raw `(bgra, w, h)` tuple that needs a picture type in the screen module (and was made `internal` for a test); F2 the TIFF sniff changes `FromBytes` with no test; F3 three unused usings in `crypto/code/Default.cs`.

**Next:** the coder fixes F1–F3; then a short re-read, then the tester and security.

**Code example:**
```csharp
// the triple handed by hand through five signatures (F1)         window/this.cs:153, code/Window.cs:94,105,110
internal static (byte[] bgra, int w, int h)? Pixels(byte[] encoded)
```
