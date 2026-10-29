# Expected: stage 9, signed, in its own view

What I expect the coder to write (shown to Ingi, not the coder; compared when it reports). Decisions 58, 59, 64, 65. Members not quoted from code are marked NEW.

## The view: `PLang/app/View.cs`

```csharp
/// <summary>A snapshot: what plang keeps of a run, read at rest — the [Store] members, a [Sensitive] one left out.</summary>
public static readonly View Snapshot = new snapshot();                                   // NEW (64)

private sealed class snapshot : View                                                     // NEW
{
    internal override bool Shows(PropertyInfo property, bool tagged)
        => !IsIgnored(property) && !Is<SensitiveAttribute>(property) && (!tagged || Is<StoreAttribute>(property));
    internal override bool Masks(PropertyInfo property) => false;          // left out, never masked
    internal override string? Name(data.@this record) => record.Name;      // as Store
    internal override ValueTask<data.@this> Settle(data.@this data) => new(data);           // as written, as Store
    private protected override ValueTask Template(item.@this value, IWriter writer) => value.Output(writer);   // as Store
    internal override ValueTask Variables(item.@this value, IWriter writer) => …;           // as Store: the row's variable list
    internal override void Sensitive(string characters, IWriter writer) => writer.Null();   // (65): back unset, never ****
    public override string ToString() => "Snapshot";
}
```

The sensitive leaf's hook (65): `View.Sensitive(string)` returning a string becomes `internal virtual void Sensitive(string characters, IWriter writer)`, which writes `****` by default and the characters in Store. IWriter's `Sensitive(string)` keeps its signature, and its default body calls the view (`type/format/IWriter.cs:63`). The formal writer's own `****` is untouched.

The three Store-like members (Name, Settle, Template, Variables) are the same code in store and snapshot. I expect it shared, not copied: either snapshot derives from store, or both call one place. Copying it would be a finding.

## The signature records its view

```csharp
// type/item/signature/this.cs
[Store, Out] public string? View { get; init; }     // NEW (64): the view the hash was taken in — store | out | snapshot
// written into the canonical signing bytes, beside expires, so flipping it breaks the signature
```

- `Ed25519.SignAsync` sets it from the view it hashes in (`signing/code/Ed25519.cs:35,41`).
- `data/schema/signature.cs:95–104`: `Origin` is Stored when the recorded view is `snapshot`; otherwise it comes from the reader's view, as today (65(2): a store- or out-signed Data never gets freedom from the window by its own say-so).
- Verify (`Ed25519.cs:109–113`) re-hashes in the recorded view when it is `snapshot`, else as today.
- A signature with no `View` (made before this) reads as today.

## The save: written once, signed by the app

```csharp
// module/snapshot/save.cs — Start
var walked = Context.App.Snapshot(Context);                         // the walk, as now
if (!walked.Success) return walked;
var written = await walked.Write(View.Snapshot);                     // NEW door or existing: the plang writer, once, in the view
// the answer: a wire of type `snapshot` over those bytes (type/this.cs:475, Make(slice, transport)), so every carrier
// after this pastes the bytes (wire/this.cs:58–68) and none filters them again
return await Sign(written);                                           // the existing sign action, from C#, in View.Snapshot (59, 65(3))
```

- The catalog stays as it is: `signing.sign`'s step parameter stays `StoreView`, and the save names `View.Snapshot` from C#.
- No signing identity is named: every signature is the app's own (59).

## Resume: always verified, by this app

```csharp
// module/snapshot/resume.cs — before anything starts
// 1. the Data holds a signature, else SnapshotUnsigned (403)                                      NEW key
// 2. it verifies (the existing verify, in its recorded view), else its own error whole
// 3. its identity is one of the app's non-archived identities (through IIdentity), else SnapshotNotThisApp (403)   NEW key
// then the walk's Start, as now
```

There's one rule with no exemption: a snapshot that never left the app is verified too (59(2)).

## The file

A snapshot file is `.plang` (58). `test/plan/snapshot/…/FreshAppGoesOn`: `run.txt` becomes `run.plang`, written by a run of Record.goal, never by hand. The builder rebuilds Record and FreshAppGoesOn. `snapshots.md` "In a file" says `.plang`.

## Tests (red first)

- `ASensitiveMember_IsNotInTheSnapshotsText` goes green; a sensitive leaf in a dict comes back null.
- One `.plang` snapshot resumed twice (no NonceReplay), and one older than the signing window resumed.
- The same snapshot kept in a store row and in a file verifies both ways.
- A hand-edited `.plang` is refused on read. A snapshot signed by another identity is refused (SnapshotNotThisApp). An archived identity's is refused. An unsigned one is refused (SnapshotUnsigned).
- A store-signed Data arriving through Out still meets the window and the replay check (65(2)).
- A flipped `view` in a signature fails verify.
- The surface is re-pinned citing "snapshot 64, 65".

## Not in this stage

The re-sign action (stage 11, waiting on Ingi). Resuming a fixed goal from the same step (57, stage 11).
