# Stage 2.1: Route handler reads through the async `Value()` door — `Materialize()` becomes internal-only

> **Context for the coder.** You just finished Stage 2. During it, `Materialize()` survived as a public-ish sync core and got called directly at ~300 handler sites — because the door (`Value()`) is currently just `new(Materialize())` and many handlers are still sync, so `.Materialize()` was the path of least resistance. That's the gap this stage closes, *before* Stage 3 puts the async I/O read in front of the parse. This was found in conversation with Ingi (the verbose-read thread); it's expected work, not a surprise audit.

**Goal:** Every **handler** value-read goes through `await Value()`; a handler that's still sync becomes `async`. `Materialize()` reverts to the **internal sync core** of the door (Data→Data plumbing + the genuinely-sync surfaces ToString/Equals/serialize). So when Stage 3 slots the async **read** in front of the parse inside `Value()`, every handler gets it for free — nothing bypasses it.
**Scope:** Included — the ~60 handler files in `app/module/` that call `.Materialize()`, plus the navigation/compare chain in `app/data/` that is supposed to be async. Excluded — Stage 3's actual async read path (this stage only makes sure the *call sites* are on the door so Stage 3 lands cleanly); the comparison work (Stages 4–6); the two-phase `sort` (Stage 6 owns it — see Review list).
**Why now (the latent bug this prevents):** today `Value()` and `Materialize()` are identical, so nothing is broken *yet*. But the moment Stage 3 adds the async read inside `Value()`, every `.Materialize()` call site **skips it** — a `list/count` or `list/where` over a list of `file`/`url` references would never load their content. The direct calls hardcode the sync path. Fixing the call sites now means Stage 3 is a one-file change (the door), not a 300-site re-migration.

## The rule (apply per call site)

1. **Reading a value (content that could become a `file`/`url` reference → I/O)** → `await x.Value()`. If the enclosing method is sync (`Task<T>` + `Task.FromResult`, not `async`), **flip it to `async Task<T>`** and drop the `Task.FromResult`. This is the main move.
2. **Reading a name slot (`Data<Variable>` / `IRawNameResolvable` — resolving *which* variable, never content)** → still `await x.Value()`. The generic door returns the typed value, so `var v = await ListName.Value();` gives the `Variable` directly (no `as` cast). It's I/O-free and sync-completes, but routing it through the door keeps one rule and removes the per-site "is this a name or a value?" judgment that caused the sprawl. (If you find a name-only sync handler where the async flip is pure ceremony, flag it — there's a case for a dedicated sync name accessor, but default to the door for now.)
3. **Data-internal plumbing (`app/data` Data→Data) and genuinely-sync framework surfaces (`ToString`/`Equals`/`GetHashCode`, serializer `Write`)** → leave as `Materialize()` (or `Peek()` where it's a raw-rung read). These never `await` and never do I/O; `Materialize()` is their sync core.

**Enforcement:** after the migration, `.Materialize()` must not appear in `app/module/`. Make `Materialize()` as private as the call graph allows (`private` if all live callers are `@this` partial siblings; otherwise `internal`) and add a grep gate — `grep -rn "\.Materialize()" PLang/app/module/` returns zero — mirroring the `System.IO` PLNG-gate discipline. The door is the single handler-facing read.

## Worklist (from the audit)

**A — sync handlers with value reads → flip method to `async`, then `await .Value()`** (the real work):
`builder/validateResponse.cs`, `mock/intercept.cs`, `goal/getTypes.cs`, `list/{sort,set,get,indexof,flatten,reverse,last,remove,unique,add,count,first}.cs`, `crypto/code/Default.cs`, `settings/Sqlite.cs`, `math/{add,subtract,multiply,divide,intdiv,modulo,power,min,max}.cs`, `error/throw.cs`, `module/remove.cs`, `signing/Signature.cs`, `event/skipAction.cs`, `code/this.Snapshot.cs`. (`list/sort.cs` overlaps the two-phase sort — see Review.)

**B — sync handlers, name-slot reads only (I/O-free; flip is trivial / sync-completing)** — lowest risk, decide per note in rule 2:
`mock/{verify,reset}.cs`, `cache/wrap.cs`, `timer/{start,end}.cs`, `event/remove.cs`, `code/this.cs`, `crypto/{encrypt,decrypt}.cs`, `debug/tag.cs`, `module/remove.cs`, `variable/{exists,get,remove}.cs`.

**C — already `async`, straight swap `.Materialize()` → `await .Value()`** (no method change):
`builder/code/Default.cs` (38), `assert/code/Default.cs` (37), `llm/code/OpenAi.cs` (29), `debug/this.cs` (12), `error/handle.cs` (9), `ui/code/Fluid.cs` (8), `condition/Operator.cs` (6), `condition/code/Default.cs`, `test/{run,discover}.cs`, `http/code/Default.cs`, `variable/set.cs`, `list/where.cs`, `llm/query.cs`, `signing/code/Ed25519.cs`, `identity/code/Default.cs`. (Some of these — `ui/Fluid`, template render — may already materialise up-front per Stage 2; confirm the await lands on the door, not a re-introduced sync read.)

**D — LEAVE as the internal sync core** (`Materialize()` stays):
`app/data/{this.cs, this.Result.cs, this.Transport.cs, this.Reconstruct.cs, Wire.cs, ShouldExit.cs, code/Default.cs}`; `app/type/image/serializer/Default.cs`; `app/type/path/file/this.Operations.cs`. These are Data→Data plumbing, serialization, or type-internal raw handoffs.

**E — REVIEW separately (part of an async chain or another stage; do not blind-swap):**
- `app/data/this.Navigation.cs` (6) and `app/data/Compare.cs` (4) — navigation and compare are **async** by the Stage-2 / truthiness design (`ValueTask` navigation, async `Compare`). Their `Materialize()` calls likely should be `await Value()` on the async path, not the sync core. Verify against the navigation chain (`GetChild`/`GetChildValue` is `ValueTask`-shaped) — `count.cs:13` calls `data.GetChild("Count")` with no `await`, which is the same gap on the navigation side.
- `app/module/list/sort.cs` and `app/type/list/this.cs` — the **two-phase sort** (materialise keys in an async phase, sync-compare materialised keys) is **Stage 6's** mechanism. Coordinate; don't fold a naive `await` into the comparator.
- `app/goal/GoalCall.cs` (4), `app/goal/Methods.cs` (2), `app/goal/steps/**` — goal-call parameter resolution. Check whether these are on the async resolution path or genuinely-internal before swapping.

## Design notes

**One door, one rule.** The reason the sprawl happened is that two accessors (`Value()` and `Materialize()`) did the same thing, so the choice was free and the sync one won in sync methods. Collapsing the handler-facing surface to exactly one (`await Value()`) and making `Materialize()` un-callable from handlers removes the choice. A handler that "can't await" is the signal to make the method `async`, never to reach for the sync core.

**The async flip is mechanical.** `public Task<X> Run() { … return Task.FromResult(r); }` → `public async Task<X> Run() { … return r; }`, and each `x.Materialize()` → `await x.Value()`. Watch for: (1) `await once` per call site — don't `await x.Value()` twice, capture it; (2) the resolution-error guard reorder you already applied in Stage 2 (`var v = await x.Value(); if (!x.Success) return …; … v …`) applies to these sites too; (3) name slots return the typed value from the generic door, so the `as variable` cast usually disappears.

## You own this (coder)

The worklist is from a static audit — trust your read of each site over the bucket it landed in, especially the **E (review)** entries, where a blind `await` would be wrong (the two-phase sort, the navigation chain). Non-negotiable outcome: `app/module/` contains zero `.Materialize()` calls, `Materialize()` is internal, and every handler value-read is `await Value()`. If a handler genuinely must read synchronously and provably never touches I/O (a name-only path), flag it rather than quietly keeping `Materialize()` — that's the one judgment to bring back, not decide silently.
