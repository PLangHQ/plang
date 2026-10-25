# codeanalyzer summary (fast-apps)

**Version:** v7

**What this is:** The two files the auditor's N12 named, changed after review6 and not read whole (`module/identity/code/Default.cs`, D4-1's export refusal; `type/item/image/this.cs`, P1's size gate), read whole under rule 66, and the full bench row at the tip (N15). Tip 2401279ed. Read only. Report: `.bot/fast-apps/codeanalyzer/review7.md`.

**What was done:** Both files pass; nothing blocks. Notes: the export refusal asks "is this identity mine" by a public-key scan beside the resolver that returned the origin's fallback (store read twice), and says "the app that made this one" for a root app; `image`'s `Sized` repeats the type's formats by literal name (it declares tif, svg and heic and refuses to size them), and its comment names an advisory and a version (4.1.1) that doesn't match the project (2.1.13). Bench at the tip (quiet, load 2.5): sub birth 0.14 ms / 110 KB, root birth 17.9 ms / 2,358 KB, requests 1.82 ms / 540 KB and 16.68 ms / 3,821 KB, held memory flat (identical to review4), `plang --test` 5.3 s (298 pass, 1 fail), sweep 40.5 s (red: Wire), incremental build 8.6 s — all in band. The clean build, 75.7 s, is not settled (a neighbour started building; the tester's quiet range is 33.8–53.8 s). The first full row's test, sweep and build columns are void: this checkout's test and console projects still had `Microsoft.Data.Sqlite` 9.0.2 restored, so 9.0.21 failed to load at run time with a green build; `dev.sh` doesn't restore dependents when a referenced project's packages change.

**Next:** the optional small fixes in the two files; `dev.sh`/the bench restore dependents; one more quiet-machine clean-build row (the tester's).

**Code example:**
```csharp
// a second list of formats, by literal name, beside the type's own eight [Format] attributes     type/item/image/this.cs:313
private static readonly HashSet<string> Sized = new(["JPEG", "PNG", "GIF", "BMP", "WEBP"], StringComparer.OrdinalIgnoreCase);
// root: the format says whether its size is read; ProbeDimensions asks the detected format's kind
```
