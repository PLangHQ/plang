# codeanalyzer — lazy-deserialize — summary

**Version:** v1 (first codeanalyzer pass)

## What this is
`lazy-deserialize` makes `Data` lazy: `Data { bytes(raw), type, kind, value }` where `value`
is computed once, on first touch, from the raw source form via a per-(type,kind) **reader
registry** (the read-side mirror of the existing renderer). One read boundary (`channel.read`)
stamps `{type,kind}`; `file.read`/`http.get` stop deserializing. Scalar reads (`%x%`) return
the raw form without parsing; navigation (`%x.field%`) and `as <type>` materialize. The v3
session also fixed a class of internal `Data→JSON` round-trips (deep-clone / wire-shape /
goal-call param) that were dropping Signature and mislabelling types.

## What was done (this review)
Rebuilt clean and verified the coder's GREEN claim:
- **C# suite: 4021 / 0.** **Goal suite: 271 pass / 1 fail** — the 1 fail is a live
  `httpbin.org` **503** (network-flaky; passed earlier same session). No regression.
- Mechanical bans (System.IO, Console, OBP #9 courier-into-`.Value`): **0 hits** in the
  changed surface.

Deep-read the lazy mechanism + the highest-risk recent commits. Findings in
`v1/report.md`. **Verdict: NEEDS WORK (fail).**

### Findings
- **F1 (Medium, blocker):** `list.add` stores the whole `Data` (correct — preserves
  Signature), but list-*creation* still seeds **raw** values, so a list can hold a mix and
  two consumers (`List.Element`, `WrapItem`) defensively unwrap. OBP smell #5, introduced by
  this branch's own shallow-clone fix. Store `Data` uniformly + add the signed-Data-in-
  collection regression the coder already flagged.
- **F2 (Low/Med):** `Materialize()` (read-through) vs `Materialise()` (in-place seam) — one
  vowel apart, different behavior. Rename the seam.
- **F3 (Low):** duplicated static-`Resolve` reflection block in `AsT_Impl`/`AsT_Convert`.
- **F4 (Low):** `variable.set` re-runs `CanonicaliseKind` to repair a context lost across the
  `.pr` round-trip (Pass 4.5 re-derive tell). One site; flag not block.
- **F5 (Low):** `variable.set.Run()` ~190 lines — extract the forced-`Type` block.
- **R1:** disable the flaky in-goal httpbin test like its 8 siblings on the parent branch.

## Code example (the F1 shape)
```csharp
// list/add.cs — creation path seeds RAW...
else if (existing != null) list = new List<object?> { existing };
// ...but every add stores a Data wrapper:
data.@this snapshot = Value.ShallowClone(Value.Name);
list.Add(snapshot);

// so every consumer must defensively unwrap both shapes:
// navigator/List.cs:  if (raw is @this inner) return inner;
// data/this.cs:510:   item is @this data ? data : new @this("", item)
```

## What to do next
Coder addresses F1 (+ regression test) and F2; F3–F5 are quick cleanups; R1 is test hygiene.
Next bot: **coder**.
