# codeanalyzer summary — path-polymorphism

## Version
v1 — first codeanalyzer review of the branch (coder delivered all 8 stages).

## What this is
The branch makes PLang's `path` scheme-polymorphic: an abstract `path` base
with `FilePath` / `HttpPath` subclasses, a per-App scheme registry, file
action handlers collapsed onto `path.X()`, and the old
`System.IO.Abstractions` wrapper layer (`IPLangFileSystem` + 9 `Default/*`
types) deleted. It closes the v2 #1 finding from `filesystem-permission`
(handler-layer authorize copy-paste).

## What was done
Five-pass review of the new `app/types/path/` tree, the seven file action
handlers, the scheme registry, `Conversion.cs` path dispatch, and the Stage 8
wrapper-layer removal. Clean rebuild + `plang --test` both run green
(202 pass / 0 fail / 1 documented stale).

**Verdict: NEEDS WORK (fail).** Eight findings — full detail in
`v1/report.md`:

- **F1 (High)** — 6 of 8 file handlers downcast `Path.Value is filepath fp`.
  This is a polymorphism violation at the centre of a branch named
  "path-polymorphism". Root cause: `FilePath` has an option-bearing verb
  surface (`CopyTo(dest,overwrite,subfolders)`, `Delete(recursive,ignore)`,
  `List(pattern,recursive)`, `Save`) that the abstract base lacks. Fix: lift
  those overloads onto the base as virtual; `HttpPath` ignores the FS-only
  options. Handlers then become the one-liners Stage 3 promised.
- **F2 (Med)** — base `path` keeps file-only live properties (`Exists`,
  `Size`, …). `HttpPath.Exists` is always false; `HttpPath.Size` is wrong and
  throws on Windows. They are `[LlmBuilder]` so reachable from PLang programs.
- **F3 (Med)** — `file.exists` returns a `path` for file scheme and a `bool`
  for http scheme — no scheme-agnostic program can use it.
- **F4 (Med)** — an unregistered scheme (`s3://…`) makes the handler NRE on
  `Path.Value!` inside `Run()`; the nice `SchemeNotRegistered` error built in
  `Conversion.cs` is lost. No handler-level test covers this.
- **F5–F8 (Low)** — `Relative` ignores `RootComparison`; base `Authorize`
  references concrete `file.OsAbsolutePath`; `[PathScheme]` ctor contract
  skips `Resolve` normalization; `HttpPath.List/Mkdir` skip `AuthGate`.

The scheme registry, Permission-gate centralisation, `GlobMatches` rewrite,
and the wrapper-layer deletion are all clean — noted in the report.

Next step: coder addresses F1–F4 (F5–F8 may ride along).

## Code example — the central finding (F1)
```csharp
// modules/file/copy.cs — handler branches on concrete type
if (Source.Value is filepath fp)
    return await fp.CopyTo(Destination.Value!, Overwrite.Value, IncludeSubfolders.Value);
return await Source.Value!.CopyTo(Destination.Value!);   // http: options silently dropped
```
Fix — option overloads become virtual on the abstract base, handler collapses:
```csharp
public async Task<data.@this> Run() =>
    await Source.Value!.CopyTo(Destination.Value!, Overwrite.Value, IncludeSubfolders.Value);
```
