# codeanalyzer summary (windows)

**Version:** v1

**What this is:** the windows branch: plang's own goals/steps/actions start through their own events alone, the `[IsNotNull]` guard refuses a variable holding nothing, llm.query renders a template message row, paths use `/` math in the typed form, grants compare per OS, LF line ends, the sqlite store folder is 0700.

**What was done:** review 1 FAIL on one comment (StoreFolderModeProbeTests names the doc2 slice and its red state). Mutations on the action/step levels and the 0700 mode went red. Bench in band (ptest 4.5 s, sweep 41.8 s, red only Wire). Notes: a method hop in a required slot is evaluated twice by the guard; Windows-only behaviour is pinned only by the host's run.

**Next:** coder rewrites the comment; then re-check and tester.

**Code example:**
```csharp
protected internal override item? Level(int depth, context context) => depth switch
{
    0 => IsSystem ? this : context.App.step,
    1 when !IsSystem => this,
    _ => null,
};
```
