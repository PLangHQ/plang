# Settings store-lazy ruling — scope: Build.Files only; walk branch (i) + list.Clr identity (ii); traced

Answer to `coder/to-architect.md` (reshape scoping), settled with Ingi 2026-07-16. Scope confirmed: **property promotions = Build.Files only**; the walk's mechanism covers everything; scalars stay CLR (their promotion is Ingi's parked settings pass). Trail: this implements Ingi's store-lazy ruling that superseded layer-3(b); the (a)-rejection survives (no sync-materialize fork on source — the reshape holds materialization to one door).

> **You own this.** All `file:line` verified this session against `16a43ccbd`.

## Usage trace (the whole blast radius)

| Site | Today | Disposition |
|---|---|---|
| `module/action/build/this.cs:23` | `public List<path> Files { get; set; } = new();` | → the plang list (declaration ruling below) |
| `build/code/Default.cs:178-197` | reads `app.Build.Files` as `List<path>`, stamps `bf.Context`, `path.Matches` filter | the ONE consumer — reads rows through their doors (method already async) |
| `callstack/this.cs:29` | comment mention only | update the comment if touched, nothing else |
| `setting/this.cs:82-117` (`Set(node, dict)`) | else-arm: `Create(kvp.Value, ctx).Clr(prop.PropertyType)` for every leaf | gains the plang-vs-CLR branch (i) |
| `type/item/list/this.cs:536-556` (`list.Clr`) | decompose arm fires on ANY IEnumerable generic target — including plang list types | identity fix (ii) |
| `SettingsTests.Set_StringArray_BindsToListOfPath` (skipped) | the acceptance target | unskips green |
| `Test.Include/Exclude` etc. | already plang, bind via the working-but-wrong Clr path | UNTOUCHED in behavior; they ride branch (i)'s honest path (see the wrinkle) |

## The wrinkle the trace found — typed generic vs native list (decides the declaration)

`Test.Include` is the TYPED generic `list.@this<text.@this>` (test/list/this.cs:52). Today it binds through `list.Clr`'s decompose arm (Activator + per-element lower). But branch (i)'s "store the entity-door output directly" births the NATIVE `list.@this` (kind-tagged via layer-2 `Retag`) — `SetValue` onto a `list.@this<path>` property would be an ArgumentException. Making the door birth closed generics is real machinery we don't need. So:

**Ruling: `Build.Files` declares the NATIVE list** — `public global::app.type.item.list.@this Files { get; set; }` — born `{list, kind:"path"}` by the walk (layers 1-2: create-as-declared + `Retag`). This is the kind-on-list model applied ("C# generics are the mechanism" is for values BUILT from C# generics; a settings slot declared plang carries its element as the kind). `Test.Include` stays exactly as-is — its typed-generic path works today; harmonizing it is the parked settings pass, not this branch. Branch (i) therefore keys on the property type: a native-plang property stores directly; a typed-generic plang property keeps the existing Clr path (which (ii) makes safe); a CLR scalar keeps `.Clr`.

## The code

### (i) — the walk's branch (`setting/this.cs`, the else-arm)

```csharp
else
{
    object? val;
    try
    {
        // A plang-typed property stores the plang value DIRECTLY — born lazy as the
        // declared type (create-as-declared + Retag), materialized at the consumer's
        // door. No Clr: the property already IS plang; there is no boundary to cross.
        if (typeof(global::app.type.item.@this).IsAssignableFrom(prop.PropertyType)
            && prop.PropertyType.IsInstanceOfType(
                   val = _context.App.Type[prop.PropertyType].Create(kvp.Value, _context)))
        { /* stored as-is */ }
        else
            val = global::app.type.item.@this.Create(kvp.Value, _context).Clr(prop.PropertyType);
    }
    catch (…) { /* the existing TypeConversionFailed envelope — unchanged */ }
    prop.SetValue(node, val);
}
```

(The `IsInstanceOfType` guard is what routes the typed-generic properties — `Test.Include` — down the existing Clr path untouched; factor it your way, the intent is: store directly when the born value fits the slot, else lower.)

### (ii) — `list.Clr` identity before decompose (`type/item/list/this.cs`, ahead of the elem-detection at :544)

```csharp
// Identity: the target IS this plang list's own type — nothing to lower, nothing to
// decompose. (The decompose arm below is for CLR collection targets; without this
// guard it fired on plang list targets too and re-lifted every element.)
if (target.IsInstanceOfType(this)) return this;
```

Pin with a unit test (a `list.@this` lowered to `typeof(list.@this)` returns the same instance). This is a correctness fix independent of the walk.

### The consumer (`build/code/Default.cs:178-197`)

```csharp
var buildFiles = app.Build.Files;                       // the native plang list
if (buildFiles.Items.Count > 0)                         // your count surface — CountRaw/Items per the class
{
    var patterns = new List<path>();
    foreach (var row in buildFiles.Items)               // each row materializes through ITS door:
        if (await row.Value() is path bf)               // a path-declared source resolves via the path reader
        { bf.Context ??= context; patterns.Add(bf); }
    // …the existing Matches/ordered/seen filter over `patterns`, unchanged (path owns its containment math)
}
```

The `%var%`-raw pin from the layer-3 answer lands here naturally: a row whose raw is a builder-marked `%var%` resolves the variable at `row.Value()` — pin it.

## Pins

- `--build={"files":["a.goal"]}` → `Set_StringArray_BindsToListOfPath` unskips green; `plang build` proceeds end-to-end (the 4d validation).
- `--build={"files":"one.goal"}` (the scalar form the doc comment advertises, build/this.cs:21) — verify the walk lifts a single string into the list (the entity door's container handling) — pin it.
- Scalar settings regression guard: `--test={"timeoutSeconds":5}` etc. still bind (`Test.TimeoutSeconds` is a plang `number` — it rides branch (i) or the guard's fallback; either way green; this is the overreach your `Resolved` attempt hit — pin it).
- `list.Clr` identity unit test.
- Types baseline held (28 = 28).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| store-direct for native plang props | no Clr for a no-op crossing; born lazy, door on read | ok |
| Build.Files as native list + kind | the kind-on-list model; element typing via the kind, not a closed generic the door can't birth | ok |
| Test.Include untouched | no harmonization creep; parked pass owns it | ok |
| list.Clr identity first | the exit door answers self for self; decompose only crosses a real boundary | ok |
| scalars stay CLR | the parked "everything is Data" reshape keeps its scope, with Ingi's name on it | ok |
