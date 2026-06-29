# Stage 1 — reader coverage (additive, the precondition)

**Goal:** every `(type, kind)` reachable by `as T` construction can materialize a `source` through an `ITypeReader`. Nothing in later stages may route construction onto `source` until this holds — a missing reader throws `NotSupportedException` (`type/reader/this.cs:119`), which `source.Value`'s catch does **not** cover (`source.cs:98`: only `JsonException`/`FormatException`/`InvalidOperationException`), so it escapes to the courier instead of failing the Data as `MaterializeFailed`.

**Kind:** purely additive. Nothing dies. No ctor/caller behavior changes yet.

---

## Step 1.0 — the reachable-set trace (THIS IS THE GATE; do it first)

Do not write a single reader before this trace exists and is logged. Enumerate every `(type, kind)` that `as T` construction can actually reach, from the entry points:

- `set %x% = … as T` (`variable/set.cs`) — the literal/dynamic `as <type>` converter.
- `Declare(type)` (`builder/code/Default.cs:927,943`) — schema-stamped parameter types.
- `validateResponse.cs:222` — declared parameter types validated at build.
- the Data ctor's `type:` argument anywhere it carries a declared, non-polymorphic type.

For each reachable type, record: does it ship an `ITypeReader` (`serializer/Reader.cs`)? If not, what is its construction entry (a `Convert` hook? a ctor/factory? not reachable at all?).

**Account for two non-obvious branches:**
- **`as binary` with a kind narrows** to the kind's inner type when that type owns a reader, else rides as `binary` (`type/reader/this.cs:111-118`). The flat per-type table will not surface this — trace it.
- **`byte[]` inputs** — format comes from the declared type's kind→mime (see Stage 3 case 3), not `text/plain`. A `byte[]` declared as a structural container has no clean arm: confirm unreachable, or define it.

**Output:** a logged map `(type, kind) → reader | entry | excluded-because`. Commit it as `stages/stage-1-reachable-set.md`. This is the Stage-1 exit evidence.

---

## Step 1.1 — add `ITypeReader` readers for the from-raw scalar gaps

Types with a `Convert` hook but **no `serializer/` folder**: `date`, `datetime`, `time`. Add `app/type/<name>/serializer/Reader.cs` mirroring `number/serializer/Reader.cs` (the **`ITypeReader`** pull — NOT `Default.cs`, a different `Of()`/whole-payload contract). Scalar one-liner shape:

```csharp
namespace app.type.date.serializer;   // datetime, time analogous
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;
    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx) where TReader : global::app.channel.serializer.IReader, allows ref struct
        => reader.Null()
            ? new global::app.type.@null.@this("date", kind)
            : global::app.type.date.@this.Convert(reader.String(), kind, ctx.Context);
}
```

Verify the family `Convert` hook signature matches (`date/this.Convert.cs` etc. — `static item.@this Convert(object/string raw, string? kind, context)`); adapt the delegate call to the real signature.

---

## Step 1.2 — `file`, `directory`, `url`, `permission` (serializer folder, only `Default.cs`)

These have a `serializer/` folder with **only `Default.cs`**, no `Reader.cs`. The trace (1.0) decides per type:

- **Reachable by `as T`** → add an `ITypeReader` `Reader.cs`. **Caveat:** `url` has **no `Convert` hook** (it is not in the `this.Convert.cs` set). Its reader cannot delegate to a hook — it must construct the `url` from the scalar string via `url`'s own ctor/factory. Find that entry point (`url/this.cs`) and confirm it before writing the reader. `file`/`directory`/`permission` likewise — check whether they have a hook or a from-string factory.
- **Not a construction target** → record the trace proving it (no `as file` path reaches the ctor), and exclude it. No reader needed.

---

## Step 1.3 — `choice` (its own sub-task — the scalar one-liner will NOT fit)

`choice<T>` is keyed under the **enum's name**, not `"choice"` (`type/this.cs:288-296`). It has no `serializer/` folder; its `Convert` lives in `choice/this.cs`.

1. First confirm `as <Enum>` construction even reaches the ctor (trace from `set`/`Declare`/validate). If it does not, `choice` needs no reader — record the trace and stop.
2. If it does: register an `ITypeReader` under the **enum name** in the **runtime** table (`_runtimeTyped`, not the generated convention scan — the convention scan keys by folder name `choice`, which is wrong here). Mirror the manual `goal.call` registration at `reader/this.cs:166`.
3. The reader takes the scalar string token and validates against **`ValidValues` membership** — never a string-ctor (closed-enum rule; `feedback_validvalues_vs_construction`). A non-member fails (→ `MaterializeFailed` at first use).

---

## Step 1.4 — decide the defense-in-depth question

Decide whether `source.Value`'s catch should also catch the missing-reader throw (`NotSupportedException`) as a belt-and-suspenders failure-as-`MaterializeFailed`. Totality (1.0–1.3) is the **primary** fix; this is secondary. If read-path-unification is moving `MaterializeFailed` authoring into `app.type.Create(source)`, fold the decision there rather than `source.Value`. Record the decision either way.

---

## Exit criteria

- [ ] `stage-1-reachable-set.md` committed: every reachable `(type, kind)` mapped to a reader, an entry, or an exclusion-with-trace.
- [ ] `date`/`datetime`/`time` ship an `ITypeReader` `Reader.cs`; a unit test materializes each from a raw string scalar (`"2026-01-01" as date` → a `date`).
- [ ] `file`/`directory`/`url`/`permission` each either ship a reader (with a test) or are excluded-with-trace.
- [ ] `choice` resolved per 1.3 (reader-with-test, or excluded-with-trace).
- [ ] No construction routes onto `source` yet — the ctor is unchanged. Confirm by diffing `data/this.cs` is untouched.
- [ ] Global exit gates (build + both suites green).

## What must NOT happen

- Do not touch the ctor, `Build`, `Judge`, `Convert`, or `set`/`validateResponse` in this stage. Additive only.
- Do not write a reader for a type the trace did not prove reachable — that is speculative surface.
- Do not mirror `Default.cs` — wrong contract. Mirror `number/serializer/Reader.cs`.
