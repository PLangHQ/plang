# v18 — three shapes (report, no code yet)

## 1. A hash holds its kind
Today `hash.@this.Algorithm` is a string; four places turn names back into kinds or write a literal:
`crypto/code/Default.cs` (verify: `bound.Algorithm`, a string param, `FromBase64(…, algorithm)`),
`signing/code/Ed25519.cs:32` (`Digest(view, "keccak256", …)`), `:104` (`storedHash.Algorithm`),
`data/schema/signature.cs:31,65,84` (`hashAlgo = "keccak256"`, read as a string),
`hash/serializer/Default.cs:13` (`kind ?? "keccak256"`).

```csharp
// hash/this.cs
[Out, Store] public kind.@this? Algorithm { get; }        // null: a digest read from bare text (a comparison's)
public @this(byte[] bytes, kind.@this? algorithm)
protected internal override type.@this Type => new("hash", typeof(@this), Algorithm?.Name);
internal static @this Of(byte[] bytes, kind.@this algorithm)   // as now
public static @this FromBase64(string base64, kind.@this? algorithm)
// data/this.Output.cs
internal ValueTask<hash.@this> Digest(View view, hash.kind.@this algorithm, context)   // no parse, no null answer
```
- **Wire form unchanged**: the signature writes `Hash.Algorithm!.Name` (`signature/this.cs:185,218`,
  `this.Wire.cs:43`); readers turn the name into the kind through the choice door
  (`choice<hash.kind>.Parse`) — a name that is no kind refuses the read (DeclinedException, readable), never a
  silent fallback. The signature reader's `"keccak256"` initial value goes: a signature with no hash type is
  refused (today it silently assumes keccak). The serializer's `kind ?? "keccak256"` becomes "no kind" (null).
- **Sign's kind — one door**: `signing.setting` gains `Hash: choice<hash.kind>`, `[Default("keccak256")]`
  (`%!signing.setting.hash%`, storable like Expiry). Ed25519 signs with `context.Setting.Of<signing.setting>().Hash`.
  Verify rehashes with the stored hash's own kind (`storedHash.Algorithm`).
- **crypto.verify**: `Algorithm: data<choice<hash.kind>>` with `[Default("keccak256")]` as crypto.hash; Verify picks the
  kind: the bound hash's own, else the declared kind on Hash's type, else Algorithm — all kinds, no strings.
- Pins: a signature round-trips its hash kind (sha256 set on the setting → verifies); a signature whose hash type is
  no kind is refused naming it; crypto.verify of a sha256 hash value with no Algorithm verifies.

## 2. One door for the /system/ overlay
The overlay ("a /system/ path is the app's own when it has it, else the os's") is stated three times:
`path/file/this.Validate.cs:50–59` (a plang-rooted /system/ path), `:72–81` (an absolute one under the app's
/system/), and `goal/list/this.cs:107–109` (`App.OsAbsolutePath + at.Parent.Raw`).

```csharp
// path/file/this.cs — the places a path names, the app's first: a /system/ path its own under the app root, then
// the os's same path; any other path the one place it is
public IReadOnlyList<path.@this> Place { get; }
```
- `ValidatePath` keeps one rule: its two fallback blocks become one private step "of the places this absolute
  names, the first that is present, else the first" (the plang-rooted case made absolute first, then the same step).
- `Spelled` lists `at.Parent.Place` (each that exists) — no string math on `App.OsAbsolutePath`.
- `goal/list/this.cs:95`: `Resolve(at.Parent.Combine(file).Raw)` — Combine for the join, Resolve to apply the
  overlay to the spelled name (as `:80` does).
- Pins: the existing GoalCallResolutionTests (app's /system/ first, an os caller sees the app's copy, os Show.goal in
  another case) and a path pin: `/system/x` with the app's copy present → the app's; absent → the os's.

## 3. The template mark becomes the kind (shape only)
`string? Template` on `type.@this`, `text.@this`, `ReadContext` (+ ~30 reads: `!= null`, passing it on) becomes
`template.kind.@this?`. **The `.pr` bytes do not change**: the type writes `"template": <the kind's Name>`
(`type/this.cs:48`), which is `"plang"` today; the reader reads the name into the kind through the choice door, a
name that is no template kind refused at read. The 33 sites are mechanical (a `!= null` stays a `!= null`; the three
places that make the mark — `path.Read`, the formal reader's `Born(…, "plang")`, `TextLeaf` — pass the kind).
The `"plang"` literal in the formal reader (`reader.cs:367`) becomes `new template.kind.plang()` (or the set's member).
