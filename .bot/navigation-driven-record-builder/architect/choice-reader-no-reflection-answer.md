# Choice reader — no reflection at read time; the registry does the dispatch

Follow-up to `stabilization-cluster1-choice-answer.md`, from Ingi's review of your change: `GetMethod("Parse")` + `Invoke` per read is the old shape renamed. The reflection isn't a style nit — the AnyKind reader is doing the **registry's job inside itself**: per-(type, kind) selection is exactly what `app.type.reader.@this` exists for, and a cached-`MethodInfo` dictionary keyed by wrapper type is a second, private registry with boxing on every read.

> **You own this.** Sketch traced against HEAD; verify the flagged mechanics.

## The shape — one closed reader per closed set, registered where the closed sets are already enumerated

**1. The reader goes generic and fully typed — zero reflection, zero dispatch:**

```csharp
namespace app.type.item.choice.serializer;

/// <summary>Typed pull reader for ONE closed option set — registered per (choice, kind)
/// by the boot walk that discovers the closed sets. The symbol parses through the
/// type's own Parse; an unknown symbol's FormatException rides to MaterializeFailed.</summary>
public sealed class Reader<T> : global::app.type.reader.ITypeReader where T : notnull
{
    private readonly string _kind;
    public Reader(string kind) { _kind = kind; }
    public string Kind => _kind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("choice", kind);
        return global::app.type.item.choice.@this<T>.Parse(reader.String());
    }
}
```

**2. Registration rides the existing boot walk.** `RegisterModuleChoiceTypes` (`type/list/this.cs:530+`) already enumerates every closed `choice<T>` off the handler props and registers the reverse name map — the same loop registers the closed reader:

```csharp
// inside the existing per-prop loop, beside Register(GetTypeName(inner), c):
var kindName = GetTypeName(inner);                     // "operator", "httpmethod", …
Reader.Register("choice", kindName,
    (global::app.type.reader.ITypeReader)System.Activator.CreateInstance(
        typeof(global::app.type.item.choice.serializer.Reader<>).MakeGenericType(inner), kindName)!);
```

One `Activator.CreateInstance` per closed set, **once at boot** — the same sanctioned "single reflective touch, closed lazily, cached" class as the entity thunk (plan model #6). Reads are then a plain interface call.

**3. The reader registry gains the typed runtime seam** — it has `Register(string, string, Read)` for untyped delegates (`reader/this.cs:149-153`) but no ITypeReader overload; add the mirror (`_runtimeTyped[(typeName, kind)] = reader`), same precedence rules. It was a gap anyway — `code.load` DLLs shipping typed readers have no door today.

**4. The old AnyKind reflection reader dies whole** — the `_fromName`/`_parse` MethodInfo cache, the wrapper resolution through `App.Type[kind].ClrType`, the "needs its kind" throw. Lookup semantics improve for free: `Reader("choice", kind)` exact-match hits the closed reader; an unknown kind now fails through the registry's own loud miss ("no reader for type 'choice' (kind 'weird')") instead of a hand-rolled message.

## Verify (the two mechanics that could bite)

- **Discovery must not trip on the open generic**: `IndexAssembly` instantiates namespace-matched `ITypeReader` classes with a parameterless ctor (`reader/this.cs:194-199`). `Reader<T>` has neither (ctor takes kind; open generic) — expected to be skipped, but confirm; if the guard is loose, add `!type.IsGenericTypeDefinition` to it.
- **Coverage is identical by construction** — the old reader resolved wrappers via the reverse map that the SAME boot walk populates, so any kind the old path could resolve gets a registered reader. Pin one non-operator set (`httpmethod`) to prove the loop registers all of them, not just operator.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `Reader<T>` | the closed set's own reader; adding a set = registering an instance, nothing central | ok |
| registration in the closed-set walk | selection lives on the registry; boot-time single reflective touch, read-time typed call | ok |
| typed `Register` seam | mirrors the existing untyped seam; closes a `code.load` gap | ok |
| AnyKind reflection reader deleted | the private MethodInfo registry (a registry inside a reader) dies | ok |
