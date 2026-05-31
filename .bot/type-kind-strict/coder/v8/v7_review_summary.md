# v7 review summary — codeanalyzer v1 (FAIL → coder)

Codeanalyzer reviewed v7 (stages 1–9) at HEAD `502d43d0e`. Build clean, C#
3818/3818, PLang 263/263 — **but green hid a real gap.** Verdict FAIL on F1.

## F1 (MAJOR, blocking) — `as image strict` enforced nothing for the realistic shapes
Strict kind validation only fired when the bound value was a raw `byte[]`. For a
**path literal** (`"real.gif"`) and a **`read`-lifted `image.@this`**,
`TryInstantiateValidator` built no probe (neither matches an image ctor's first
param) and `image.ValidateKind` only read `byte[]` — so a PNG was silently
accepted as a strict GIF, at build *and* runtime. The C# Cut2 tests passed only
because they fed raw bytes; the two `.test.goal` files that "covered" the
mismatch asserted nothing.

**Ingi's ruling (2026-05-31):** validate at byte-materialization, wherever that
is — if strict, throw. `set` stays lazy.

## F2 (MEDIUM) — `kind` written twice on the wire (`type` entity + flat `Data.Kind` sibling) — OBP smell #6, branch-introduced.
## F3 (MINOR) — `type.@this.Scheme` dereferenced `Context.App` without the `?.` every sibling uses.
## F4 (MINOR) — "text never derives kind from spelling" enforced by a string name-check in `set.cs` while `text.Build` still registered a spelling-kind hook (smell #5).
## F5 (NIT) — dead `CanonicaliseKind` fast-path; redundant `BuildBuilderNames` wrapper.

## What it confirmed clean (won't re-litigate)
Signing/hash relocation invariants; LLM prompt scoping to Fundamentals; text/
number/int-literal kind behavior; the `{Name,Kind,Strict}` single-owner fold.
