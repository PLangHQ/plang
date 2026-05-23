# codeanalyzer v1 — path-polymorphism

**Branch:** path-polymorphism · **Reviewed:** 2026-05-22
**Build:** clean (0 errors, 447 warnings — pre-existing nullable noise).
**plang --test:** 202 pass / 0 fail / 1 stale (`ContextVars2.test.goal` — documented, pre-existing).

The branch makes `path` scheme-polymorphic: abstract `path` base, `FilePath` /
`HttpPath` subclasses, per-App scheme registry, file handlers collapsed, the
`System.IO.Abstractions` wrapper layer deleted. The structural OBP work is
mostly clean — the scheme registry is a proper `@this` type, the Permission
gate is genuinely centralised (closes the v2 #1 copy-paste finding), and the
wrapper-layer deletion is a real simplification. But the polymorphism is leaky
at two seams: the **handlers branch on concrete type**, and the **base class
keeps file-only semantics** that misbehave for `HttpPath`. Detail below.

---

## Summary of findings

| # | Sev | What | Files |
|---|-----|------|-------|
| F1 | High | File handlers downcast `Path.Value is filepath fp` — 6 of 8 handlers branch on concrete type | `modules/file/{save,copy,move,delete,exists,list}.cs` |
| F2 | Med | Base `path` exposes file-only live properties (`Exists`, `Size`, …) that are wrong/throwing for `HttpPath` | `types/path/this.cs` |
| F3 | Med | `file.exists` returns a different value shape per scheme (`path` vs `bool`) | `modules/file/exists.cs`, `types/path/{file,http}` |
| F4 | Med | Unregistered-scheme path → `NullReferenceException` inside `Run()`; the clean `SchemeNotRegistered` error is lost | `modules/file/*.cs`, generated handlers |
| F5 | Low | `Relative` uses hard-coded `OrdinalIgnoreCase` instead of `RootComparison` — the exact drift the `RootComparison` doc warns against | `types/path/this.cs:106,108` |
| F6 | Low | Base `Authorize` references concrete `file.@this.OsAbsolutePath` — base→subclass coupling | `types/path/this.Authorize.cs:98` |
| F7 | Low | `[PathScheme]` doc promises a single-string ctor for reflection minting; that path skips `FilePath.Resolve`'s normalization | `types/path/PathSchemeAttribute.cs` |
| F8 | Low | `HttpPath.List()` / `Mkdir()` return `Fail` without going through `AuthGate` | `types/path/http/this.cs:160,213` |

---

## F1 — File handlers downcast to the concrete `filepath` type  *(High)*

Stage 3's stated goal: "handler one-liners over `Path.X()`". Six of the eight
file handlers instead carry a runtime type-test:

`PLang/app/modules/file/copy.cs:20-25`
```csharp
public async Task<data.@this> Run()
{
    if (Source.Value is filepath fp)
        return await fp.CopyTo(Destination.Value!, Overwrite.Value, IncludeSubfolders.Value);
    return await Source.Value!.CopyTo(Destination.Value!);
}
```

The same `is filepath fp` shape repeats in `save.cs`, `move.cs`, `delete.cs`,
`exists.cs`, `list.cs`. This is a polymorphism violation: the whole point of
the abstract `path` base is that a handler should *not* know the concrete
subclass. Here every handler does.

**Root cause.** `FilePath` carries an option-bearing surface the base does
not: `CopyTo(dest, overwrite, includeSubfolders)`, `Delete(recursive,
ignoreIfNotFound)`, `List(pattern, recursive)`, `Save(value)`,
`ExistsPathAsync()`. The abstract base only declares the parameterless verbs
(`CopyTo(dest)`, `Delete()`, `List()`, …). To reach the option overloads the
handler must downcast.

**Consequences.**
- When `Source` is an `HttpPath`, the else-branch silently drops
  `Overwrite` / `IncludeSubfolders` (`copy.cs:24`), `Overwrite` (`move.cs:20`),
  `Recursive` / `IgnoreIfNotFound` (`delete.cs:22`), `Pattern` / `Recursive`
  (`list.cs:24`). The drop is invisible — no diagnostic, no documented no-op.
- Add an `S3Path` later and all six handlers need editing again. That is the
  "one change touches three files" smell — the missing structure is the
  option-bearing verb surface on the base.

**OBP-correct form.** Lift the option-bearing overloads onto the abstract
base as `virtual` (or `abstract`) methods. `HttpPath` implements them ignoring
the filesystem-only options — exactly the architect's documented
"`Recursive` stays a no-op on non-FS schemes" intent, except the no-op now
lives *inside the scheme* instead of being a branch the handler picks:

```csharp
// types/path/this.Operations.cs — base
public abstract Task<data.@this> Delete(bool recursive, bool ignoreIfNotFound);
public abstract Task<data.@this> CopyTo(@this dest, bool overwrite, bool includeSubfolders);
public abstract Task<data.@this> List(string pattern, bool recursive);
public abstract Task<data.@this> Save(data.@this? value);
```
```csharp
// modules/file/copy.cs — handler collapses to the one-liner stage 3 promised
public async Task<data.@this> Run() =>
    await Source.Value!.CopyTo(Destination.Value!, Overwrite.Value, IncludeSubfolders.Value);
```

That removes the `is filepath` test from all six handlers and makes the option
no-op an explicit, testable property of `HttpPath`.

---

## F2 — Base `path` keeps file-only semantics that break for `HttpPath`  *(Medium)*

`types/path/this.cs` makes `Scheme`, `Absolute` and the verb surface abstract,
but leaves these as concrete file-semantics on the base — inherited unchanged
by `HttpPath`:

- `Exists` (`:145`) — `System.IO.File.Exists(_absolutePath) || Directory.Exists(_absolutePath)`.
  For an `HttpPath` `_absolutePath` is the raw URL; both return `false`.
  **`httpPath.Exists` is always `false`, even when the resource exists.**
  The correct HTTP answer is the async `ExistsAsync()` verb.
- `Size` (`:147-154`) — `new System.IO.FileInfo(_absolutePath)`. For a URL this
  returns `0` on Linux; on Windows `new FileInfo("https://…")` throws
  (`:` mid-path is not a valid Windows path). **`httpPath.Size` is wrong
  everywhere and may throw.**
- `Extension`, `FileName`, `Directory`, `IsFile`, `IsDirectory`, `GoalCall` —
  computed from `System.IO.Path` string ops on the URL. Mostly harmless but
  semantically meaningless for `http://`.

All of these are `[LlmBuilder]`, so they are surfaced to PLang programs — a
program holding an `HttpPath` variable can navigate `%path.Size%` /
`%path.Exists%` and get a wrong answer (or a Windows crash).

**Recommendation.** Either make `Exists` / `Size` `virtual` and override them
in `HttpPath` (sync HTTP probes are awkward — more likely override to throw
`NotSupported` or return a sentinel), or — cleaner — move the file-only live
properties off the base into `FilePath` and expose only what is genuinely
cross-scheme (`Raw`, `Absolute`, `Scheme`, `Extension`, `MimeType`) on the
base. The current shape lets file semantics leak into every future scheme.

---

## F3 — `file.exists` returns a different shape per scheme  *(Medium)*

`modules/file/exists.cs:16-18`
```csharp
if (Path.Value is filepath fp)
    return await fp.ExistsPathAsync();   // data.@this<path> — the Path object
return await Path.Value!.ExistsAsync(); // data.@this        — a bool
```

`file.exists` on a file path returns the `path` object (so `%result.Exists%` /
`%result.Size%` are readable — "today's semantics"); on an `http(s)` path it
returns a bare `bool`. A PLang program cannot write scheme-agnostic code
against `file.exists` — `%result.Size%` works for a file and silently fails
for a URL. This breaks the uniform-shape contract the branch is built on
(architect: "drives uniform failure semantics across schemes").

Pick one shape for the action and have every scheme honour it. Returning the
`path` object uniformly is the natural choice — `HttpPath.ExistsAsync` already
knows the answer; wrap `this` the way `FilePath.ExistsPathAsync` does and the
`is filepath` branch disappears here too (folds into F1).

---

## F4 — Unregistered scheme → `NullReferenceException`, clean error lost  *(Medium)*

`Conversion.cs:208-224` carefully shapes an unknown scheme into a typed error
with a `FixSuggestion` ("Register a factory … or use a bare/file:// path").
That error never reaches the user through a file handler.

Trace:
1. `s3://bucket/key` → `Scheme.From` throws `SchemeNotRegistered` →
   `Conversion.TryConvertTo` returns `(null, error)`.
2. Generated `Read` getter (`app.modules.file.Read.Action.g.cs:21`):
   `__Path_backing = __ResolveData("path").As<path>(Context);` — a failed
   `data.@this<path>` with `Success=false`, `Value=null`. `__resolutionError`
   is captured.
3. `ExecuteAsync` checks `__resolutionError` *before* `Run()` — but the getter
   is lazy, so it is still `null` at that point.
4. `Run()` executes. `read.cs:28` — `await Path.Value!.ReadText()`.
   `Path.Value` is `null` (`data.@this<T>.Value` → `GetValue<path>()` → `null`).
   `null!.ReadText()` → **`NullReferenceException`**.

The `!` suppresses the compiler warning but not the runtime null. The
exception escapes `Run()` before `ExecuteAsync:63` can return the nice
`__resolutionError`. The user gets an NRE-wrapped generic error instead of
"register a factory for scheme 's3'".

This is a *new* failure mode: before this branch a `path` parameter from a
string never failed conversion. `PathTypeMapperTests.PathParameter_UnknownScheme_…`
covers the `Conversion` layer but **no test runs a file handler with an
unregistered-scheme path** — the gap is uncovered.

**Recommendation.** Non-nullable `data.@this<path>` slots should get the same
pre-`Run` guard the generator already emits for non-nullable
`data.@this<Variable>` (`[IsNotNull]`-style) so a failed conversion short-
circuits cleanly. If that generator change is out of branch scope, at minimum
add a handler-level test for the unknown-scheme path and decide the contract.
Trivially fixable in isolation (a `if (!Path.Success) return Path;` guard) but
that re-introduces per-handler boilerplate — the generator guard is the right
level.

---

## F5 — `Relative` ignores `RootComparison`  *(Low)*

`types/path/this.cs:30-33` introduces `RootComparison` with a pointed comment:
"Single home so `IsUnder` and `ValidatePath` can't drift apart again." But
`Relative` (`:106`, `:108`) hard-codes `StringComparison.OrdinalIgnoreCase`:

```csharp
if (_absolutePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
```

On Linux this treats `/SRV/app` as living under `/srv` when stripping the root
prefix. `Relative` is a derived/display property, not a gate, so the blast
radius is small — but it is precisely the drift `RootComparison` was created
to prevent. Use `RootComparison` here too.

---

## F6 — Base `Authorize` reaches into the concrete `file` subclass  *(Low)*

`types/path/this.Authorize.cs:98` — the scheme-agnostic base calls
`global::app.types.path.file.@this.OsAbsolutePath`. The base depending on a
concrete subclass is a small inversion. `OsAbsolutePath` ("the `os/` folder
next to the executable") is not file-scheme-specific — it is an App-level
constant. Consider hanging it off `app.@this` (next to `AbsolutePath` /
`OsDirectory`) so the base does not import a child namespace.

---

## F7 — `[PathScheme]` ctor contract skips `Resolve` normalization  *(Low)*

`PathSchemeAttribute.cs` doc: decorated classes "MUST expose a public
single-string constructor … or the future-reflection registration path won't
be able to mint them." But the registered factories use `Resolve(raw, ctx)`,
and `FilePath.Resolve` does goal-relative resolution + `ValidatePath`
normalization that the bare `new FilePath(raw)` ctor does **not**. A future
`code.load` that mints via the bare ctor would get an un-normalized FilePath.
Either the documented reflection contract should target a `Resolve`-shaped
static factory, or the normalization must move into the ctor. Worth resolving
before stage-4's attribute is actually consumed.

---

## F8 — `HttpPath.List()` / `Mkdir()` skip `AuthGate`  *(Low)*

`http/this.cs:160` and `:213` return a `NotSupported` `Fail` synchronously
without calling `AuthGate`. Every other `HttpPath` verb authorizes first. No
I/O happens so this is not a security gap, but it is an inconsistency — an
unsupported verb on an unauthorized path reports "NotSupported" rather than
the permission outcome. Minor; flagging for consistency only.

---

## What is clean

- **Scheme registry** (`scheme/this.cs`) — textbook `@this`: private
  `ConcurrentDictionary`, `Register` / `From` / `IsRegistered` surface, no
  leaked mutable collection. `ParseScheme` validates per RFC 3986. Clean.
- **Permission gate centralisation** — `Authorize` / `AuthGate` on the base,
  called once per verb impl. This genuinely closes the v2 #1 handler-copy-paste
  finding. The `BuildRequest` / `SignAndStore` awkwardness is honestly
  documented and tracked in todos.md.
- **`GlobMatches` rewrite** (`permission/this.cs:54`) — the glob→regex
  compile is correct and readable; `*`=intra-segment, `**`=cross-segment,
  `?`=single. The generated regex has no nested quantifiers, so no ReDoS
  exposure. (`RegexMatches` runs a grant-author-supplied regex with no
  timeout — acceptable since grants are signed, but a `matchTimeout` would
  be cheap insurance.)
- **Wrapper-layer deletion** (Stage 8) — removing `IPLangFileSystem` +
  `PLangFileSystem` + 9 `Default/*` types is a real simplification; the
  `Handler → Path.X → System.IO` two-layer path is much easier to follow than
  the former three-layer indirection. `ValidatePath` relocated as an honest
  normalization static with a clear "this is not the gate" doc.
- **Cross-scheme `CopyTo` / `MoveTo` defaults** on the base
  (`this.Operations.cs:59,72`) — naive read/write with subclass fast-path
  override is the right shape.

---

## Deletion test

Nothing flagged. The fix-introduced code (the `is filepath` branches) is the
opposite problem — it *should* be deleted (F1), and deleting it requires the
base-class surface change, not removing the branch alone.

---

## Verdict: NEEDS WORK

Build and tests are green and the structural skeleton is sound, but the
branch's headline feature — polymorphism — leaks at two seams: handlers
downcast to the concrete type (F1) and the base class carries file-only
semantics that misbehave for `HttpPath` (F2). F3 breaks the uniform-shape
contract across schemes and F4 is a real crash on a newly-reachable input.
F1–F4 should be addressed before this is called done; F5–F8 are low and can
ride along or be logged.
