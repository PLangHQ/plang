# architect → coder — the snapshot's entry bag is untyped; type it at the write door and the serializer disappears

Answers `to-architect-snapshot-serializer-seal.md`. Settled with Ingi 2026-09-22.

> **You own this.** Ruling settled; mechanics yours.

## Ruling: yes to both — and the root is one level ABOVE the serializer

Your smell names are right (broken seal, fork, stray static). But the type-switch exists because the snapshot's entry bag is untyped:

```csharp
// snapshot/this.cs:49 — the door that makes the switch necessary
public void Write<T>(string key, T value) => _entries[key] = value;    // Dictionary<string, object?>
```

An `object?` bag FORCES a central switch downstream. `this.Wire.cs:6-8` states the intended design and then can't follow it ("each ISnapshot subsystem serializes the slice it captured — it alone knows the concrete CLR type behind each `object?` entry"). Fix the door; the switch has nothing left to do.

```csharp
// snapshot/this.cs — an entry is a plang VALUE, born at the write door with the snapshot's context.
// Scalars lift through the entity door (string→text, int→number, List<snapshot>→list, List<data>→list),
// exactly like every other value birth. Nothing is stored raw.
public void Write<T>(string key, T value)
    => _entries.Set(new data.@this(key, item.@this.Create(value, Context), context: Context));   // _entries: dict.@this
// Read<T>(key) becomes the typed ask on the entry (Value<T>) — same door as everywhere.

// the snapshot writes ITSELF: an object of entries, then sections — each entry writes itself. No switch.
public override async ValueTask Output(IWriter writer, View mode, actor.context.@this? context)
{
    writer.BeginObject();
    foreach (var entry in _entries)   { writer.Name(entry.Name); await entry.Output(writer, mode, context); }
    foreach (var (name, section) in _sections) { writer.Name(name); await section.Output(writer, mode, context); }
    writer.EndObject();
}
// the `Write(IWriter)` override at this.cs:77-78 and serializer/Default.cs — DELETED entirely
// (Write, both Render overloads, the reflection fallback, the scalar/IDictionary/IEnumerable arms).
```

The snapshot's own node shape (entries + sections) is the ONE structural thing it owns and writes. Below that: composition only.

## Migration — what lands in `default:` today, and where it goes

- `Registration(TypeName, ProviderName, Source)` / `DefaultOverride(TypeName, ProviderName)` (`module/action/code/this.Snapshot.cs:14,20`): plain records. Tag their properties `[Out]`; they cross as property bags through the ESTABLISHED reflection-kind `Output` (`kind/reflection/this.cs:239`), which enforces the declared-face rule and throws loud for an untagged type in our assembly. That is CLAUDE.md's sanctioned path for domain types ("ship by adding `[Out]`") — today's fallback is a second, unguarded copy of it. Same wire bytes (camelCase wire names), one path.
- Everything else already lifts at the door: call-frame scalars (`call/this.Snapshot.cs:29-36`), `frames` (`List<snapshot>`, `callstack/this.Snapshot.cs:157`), `variables` (`List<data>`, `variable/list/this.Snapshot.cs:29`).
- Grep every `s.Write(` site; anything that isn't a scalar, a snapshot, a Data, a list of those, or an `[Out]`-tagged record fails loud at the write door — which is the point.

## The overflow: no cap, no visited-set

You're right. A value writing itself never exposes a foreign back-reference, so the walk cannot cycle. Prediction: if the `{"name":"a","value":{"name":"a",…}}` symptom SURVIVES the change, it is no longer the serializer — it is the `data.@this<T>` implicit double-wrap footgun CLAUDE.md names, at the CAPTURE site (`Clone()` at `variable/list/this.Snapshot.cs:27` is where to look first). After this change it surfaces at that site instead of as a crash three layers away — fix it there.

## Restore: untouched

Already deferred (`this.Wire.cs:39-44`; `Create` throws). This ruling is the WRITE half of that redesign; the read half follows the same symmetry later (each section reads itself back off an `IReader`). Do not expand into it now.

## Order

Fits between the current step and modifier subframes — it unblocks the Wire suite, which every later number depends on. Then subframes → Stage D.
