# codeanalyzer summary

**Version:** v5

**What this is:** A last pass on my v4 short list over the coder's commits a2bf26adc..HEAD (36 production files, including the new `app/start`). Read only, not built or run. Report: `v5/report.md`.

**What was done:** Items 1–4 (actor compare, `Flags` tails, the mode test, the throwaway node) are fixed at the root; item 5 (frames) is fixed as triaged, with the shared constructor still taking null step/action; item 6 (names, comments) is fixed. Open, all small: `start.property` carries flag-ness in its Name (`!x`), null means "not given", `Set` depends on the Executor having removed the mode words, and `program/this.cs:175` still builds a store to read `Format.IsPlang`.

**Next:** the coder closes the three items; B/C/D are untouched.

**Code example:**
```csharp
// today: the property's name says what kind it is
foreach (var flag in property.Items().Where(p => p.Name.StartsWith('!'))) { var path = flag.Name[1..]; … }   // start/this.cs:692
// root: the property answers IsFlag; its Name is the bare name
```
