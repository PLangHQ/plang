# Coder v3 learnings — codeanalyzer v1 review of path-polymorphism

## 1. A polymorphic base must carry the *option-bearing* verb surface, not the bare one

codeanalyzer F1: 6 of 8 file handlers did `if (Path.Value is filepath fp)`. The
root cause was not the handlers — it was the abstract base declaring only
`Delete()` / `List()` / `CopyTo(dest)` while `FilePath` carried
`Delete(recursive, ignore)` / `List(pattern, recursive)` /
`CopyTo(dest, overwrite, subfolders)`. To reach the options a handler *had* to
downcast. **When you make a base abstract, the abstract surface must be the
*full* surface callers need — including every option overload.** Put the
parameterless forms back as non-abstract convenience (`Delete() =>
Delete(false,false)`). Non-FS schemes implement the option-bearing signature
and document the FS-only options as no-ops — the no-op lives *inside the
scheme*, never as a branch the caller picks.

## 2. A base class must not carry semantics that are wrong for a subclass

codeanalyzer F2: the base `path` had `Exists` (`System.IO.File.Exists`) and
`Size` (`new FileInfo`). For an `HttpPath` these are silently wrong (false
everywhere) or throw (Windows can't make a `FileInfo` from a URL). If a property
on the base only makes sense for one subclass, it belongs *on that subclass*.
The cross-scheme query is the abstract verb (`Stat()`), not a concrete property.

## 3. `Data.ToBoolean()` returned `true` for any non-null object — a latent bug

codeanalyzer F3 exposed this: `if X exists` lowers to `path == true`, routed
through `Data.ToBoolean()`, whose final line is `return true` for any object.
So **`if X exists` was *always true*** — the negative-case tests had been
quietly disabled (`.test.goal2`) because they couldn't pass. Lesson: a blanket
"non-null ⇒ truthy" is a real bug magnet. The fix is `IBooleanResolvable` — a
value that knows its own truthiness answers via `AsBooleanAsync()`, and `Data`
*dispatches* to it rather than guessing. Watch for "always passes" tests: a
green test that would also be green if the behaviour were broken is not
coverage.

## 4. A review's failure trace can name the wrong mechanism — verify it

codeanalyzer F4 said an unregistered scheme NREs on `Path.Value!` inside
`Run()`. Tracing it for real: `Data.As<path>` reflection-invokes `path.Resolve`,
which *throws* `SchemeNotRegistered`; the `TargetInvocationException` escapes
`As<T>` itself — so a handler `if (!Path.Success)` guard would never run
(touching `Path` throws first). The review's conclusion (surface a typed error)
was right; its mechanism (NRE / lazy getter) was off. **Always re-trace a
review finding against the live code before coding the fix** — the fix lands at
a different layer than the report implies.

## 5. `As<T>`'s reflection-invoked `Resolve` can throw — catch it

`AsT_Impl` / `AsT_Convert` invoke a domain type's static `Resolve(string,ctx)`
via reflection. `Resolve` can legitimately throw (`path` raises
`SchemeNotRegistered`). An uncaught throw there escapes `As<T>` and every
handler. `As<T>` already returns `FromError` for cycles and depth limits — a
thrown `Resolve` must be treated the same way: catch `TargetInvocationException`,
shape the inner exception into a failed `Data<T>`.

## 6. Making one method async ripples through its whole pipeline — map it first

`path.AsBooleanAsync()` is async (http existence is an HTTP HEAD). It is reached
from the *synchronous* condition pipeline, so the change rippled:
`Data.ToBooleanAsync` → `Operator.Evaluate` (`Func<…,Task<bool>>`) →
`IEvaluator.Evaluate` (`Task<data.@this>`) → `Default` evaluator → `if`/`elseif`/
`compare` handlers → `list.any` → and the parallel `assert.IsTrue/IsFalse`
surface. Before writing an async change, grep every caller of every method on
the path and budget the whole tree — not just the entry point.

## 7. Convenience overloads vs. the abstract contract — reflection tests break

`PathAbstractTests` did `typeof(path).GetMethod("Delete")` — once `Delete` had
two overloads (`Delete()` convenience + `Delete(bool,bool)` abstract) that
threw `AmbiguousMatchException`. Overload-aware reflection tests must pass
explicit `paramTypes` to `GetMethod`. When you add an overload, check the
reflection-based shape tests.
