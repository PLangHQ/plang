# Permission Design — Deep Dive

## Folder layout

```
PLang/App/FileSystem/Permission/
  this.cs              -- @this manager + the Permission record (both live here)
  Verb/
    this.cs            -- Verb @this: composes Read/Write/Delete coverage
    Read.cs            -- record Read(bool Recursive = true, bool Metadata = true)
    Write.cs           -- record Write(bool Create = true, bool Overwrite = true, bool Append = true, bool Mkdir = true)
    Delete.cs          -- record Delete(bool Recursive = true, bool Permanent = true)
```

Namespace: `App.FileSystem.Permission`. Singular. The doubled type name `App.FileSystem.Permission.Permission` is the accepted cost of singular OBP in C#.

## The record

```csharp
namespace App.FileSystem.Permission;

public enum Match { Exact, Glob, Regex }

public record Permission(string AppId, string Path, Verb.@this Verb, Match Match)
{
    public bool HasAccess(Path path, Verb.@this requested)
    {
        if (!PathMatches(path)) return false;
        return Verb.Covers(requested);
    }

    private bool PathMatches(Path path) => Match switch
    {
        Match.Exact => string.Equals(Path, path.Absolute, StringComparison.OrdinalIgnoreCase),
        Match.Glob  => Glob.IsMatch(Path, path.Absolute),
        Match.Regex => System.Text.RegularExpressions.Regex.IsMatch(path.Absolute, Path),
        _           => false
    };
}
```

`HasAccess(Path path, ...)` takes the whole Path object. The receiver decides which field to read; callers don't pre-decompose.

`PathMatches` is private — the path-matching algorithm is an implementation detail of how Permission interprets its own Match field. The enum-and-switch is a known smell; if Match ever grows configurable variants (`Glob(CaseSensitive)`, `Regex(Flags)`), promote Match to its own `Match/` folder with the variant-design pattern from `good_to_know.md`.

## The manager

```csharp
public class @this
{
    // State lives in app.System variables, not a private list here.
    // @this is a typed view over that variable.

    public IEnumerable<Permission> List() => /* read system variable */;

    public Data Check(Path path, Verb.@this requested) =>
        List().Any(p => p.HasAccess(path, requested))
            ? Data.Ok()
            : Data.Fail(new PermissionRequired(path, requested));

    public void Add(Data<Permission> signed) => /* write back to system variable */;
}
```

Check is four lines because every comparison is delegated. The manager iterates and composes; it never reaches into a record to apply matching from outside.

## The Verb @this

```csharp
namespace App.FileSystem.Permission.Verb;

public class @this
{
    public Read   Read   { get; init; } = new Read();
    public Write  Write  { get; init; } = new Write();
    public Delete Delete { get; init; } = new Delete();

    public bool Covers(@this requested) =>
        Read.Covers(requested.Read) &&
        Write.Covers(requested.Write) &&
        Delete.Covers(requested.Delete);
}
```

All three variants always present, defaulted to "fully granted." Narrowing is an explicit record copy:

```csharp
var loggerVerb = new Verb.@this
{
    Write  = new Write(Overwrite: false),
    Delete = new Delete(Recursive: false, Permanent: false),
};
```

## The variant records

Each variant owns its own `Covers` rule. The rule reads naturally: *"if the request needs feature X, the grant must have X."*

```csharp
// Read.cs
public record Read(bool Recursive = true, bool Metadata = true)
{
    public bool Covers(Read r) => (!r.Recursive || Recursive) && (!r.Metadata || Metadata);
}

// Write.cs
public record Write(bool Create = true, bool Overwrite = true, bool Append = true, bool Mkdir = true)
{
    public bool Covers(Write w) =>
        (!w.Create    || Create)    &&
        (!w.Overwrite || Overwrite) &&
        (!w.Append    || Append)    &&
        (!w.Mkdir     || Mkdir);
}

// Delete.cs
public record Delete(bool Recursive = true, bool Permanent = true)
{
    public bool Covers(Delete d) => (!d.Recursive || Recursive) && (!d.Permanent || Permanent);
}
```

## What the verbs mean in operations

- **`mkdir`** is `Write` (with `Mkdir=true`). Creating a directory is making something exist.
- **`rmdir`** is `Delete`.
- **Rename / move** is *two checks*: `Delete` on the source path + `Write` on the destination parent. No new verb.
- **Copy** is `Read` on source + `Write` on destination parent. No new verb.
- **Stat / exists** is `Read` with `Metadata=true`. Granting a backup-style app `Read{Recursive: true, Metadata: true, but...}` — actually, content visibility isn't a sub-option of Read in this model. Worth a question: do we want a `Content` boolean on Read so "may stat but not read content" is expressible? See open-questions.md.

## Serialization (LLM-legible)

A signed `Data<Permission>` for the Messages app reading every app's `system.sqlite`:

```json
{
  "appId": "<messages-app-id>",
  "path": "/apps/*/system.sqlite",
  "match": "glob",
  "verb": {
    "read":   { "recursive": false, "metadata": true },
    "write":  { "create": false, "overwrite": false, "append": false, "mkdir": false },
    "delete": { "recursive": false, "permanent": false }
  }
}
```

Wrapped in Data, which carries signature, signer, signed-at, expires-at. The record itself is the payload; the envelope is the proof. Permission never sees crypto fields directly — it trusts that what comes out of the variable has already been verified.

## What's NOT in scope here

- The signing mechanism (PLang plumbing).
- The prompt UI (PLang plumbing).
- Where the grant physically lives on disk (system variable; see `storage.md`).
- The provider/Code routing for goal-backed virtual filesystem (parked).
- The app-side cascade for *requested* verb config (see `open-questions.md`).
