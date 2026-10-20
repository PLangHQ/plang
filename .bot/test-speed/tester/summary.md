# tester — test-speed — summary

**Version:** v1

**What this is.** A playground branch (decision 159), cut from item-doors, for making the C# tests and a new app's
birth faster. The measurements and design history are in `test-speed.md` (carried from item-doors).

**What was done.** Decision 157: a format (`PLang/app/type/kind/format/this.cs`) binds its type's own
encode/decode on first use; the registry (`PLang/app/type/list/Registry.cs`) binds nothing; `kind`'s encode/decode
constructor parameters and delegate types are gone (`kind/this.cs`, `kind/this.Generic.cs`); surface golden
re-pinned. Birth median 28–31 → 26–29 ms (~2 ms). Open: the static fields (architect: against "no statics";
kept here to try things); §6, the code's facts read once per process.

**Code example.**
```csharp
// format<T>: T writes itself when it implements IEncode<T>; else as any kind writes
public override Task<data> Encode(Stream stream, data data, context context, View? view = null, ...)
    => _encode != null ? _encode(stream, data, context, view, encoding, ct)
        : base.Encode(stream, data, context, view, encoding, ct);
```
