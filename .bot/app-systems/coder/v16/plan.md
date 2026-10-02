# v16 — Find + the shortcut loader: shape

## The rule, once — on the goal type
```csharp
/// The .pr a .goal is built to: X.goal → .build/x.pr beside it. A static on the goal type by exception: the goal's
/// own PrPath, goal.Load and nothing else read it.
public static path.@this Pr(path.@this source) => source.Parent.Combine(".build").Combine(source.FileNameWithoutExtension.ToLowerInvariant() + ".pr");
public path.@this? PrPath => Path is { Absolute.Length: > 0 } source ? Pr(source) : null;
```

## goal.Load takes the .goal
```csharp
public static async Task<data> Load(path.@this source, app app)   // source: the .goal
{
    var pr = Pr(source);
    var loaded = app.goal.list[pr] is { } held ? context.Ok(held) : await Read(pr, app);
    ...setup refusal unchanged
}
```
Load doesn't require the .goal on disk (Find and Walk do — that's where "an app doesn't run from a .pr alone" is
enforced); so a .pr-only test fixture still loads by its .goal name. Callers move:
- `Executor.cs:124-127` — its own copy of the rule (wrong for a subfolder: `sub/X.goal` → `/.build/sub/x.pr`) goes;
  goalFile is `"/" + goalFile` (the .goal). Test mode: `/system/test.goal`.
- `module/build/this.cs:76` → `"/system/builder/build.goal"`. `test/this.cs:140` → `Goal.Path`.
- Tests passing `.pr` paths (PrPipelineTests, GoalsTests, PrLoadTests, AppGoalsThroughPathVerbsTests) → the .goal.

## Find — one method; TryLoadPr, Readable and the name math go
```csharp
public async Task<goal?> Find(string name, goal? caller = null, CancellationToken ct = default)
{
    // the caller's chain, then the goals held — unchanged
    // not read yet: the .goal the name writes — beside the caller (a slash-qualified name walks up its folders),
    // then from the app root; path.Resolve settles /system/ (the app's own first, then the os's)
    var source = name.EndsWith(".goal") ? name : name + ".goal";
    if (!rooted && caller?.Folder is { } folder)
        for (var at = folder; ...; at = at.Parent)          // bare name: the caller's folder only
            if (await Loaded(Resolve(at.Combine(source).Raw)) is { } near) return near;
    return await Loaded(Resolve("/" + source));
    // Loaded = the .goal exists → goal.Load(it); a local function, not a member
}
```
Re-resolving `.Raw` is what keeps app-first for an os caller: `os/system/builder/Build.goal` calling `Foo` must find
the app's `/system/builder/Foo.goal` first (Ingi: an app's /system/ files override the system's). `Combine` alone
stays in the os folder. Rooted = `source` starts with `/` (one check on the name as written).

## Walk lists .goal files
`Listed` lists `*.goal` (recursive) under the app root and os `/system`, not `.build/*.pr`; each goes through
`goal.Load(source)`; a .goal with no .pr is left out with the debug line it has today. The seen-set (by plang form,
app first) already makes an app's `/system/x.goal` win.

## The shortcut list loads through Find
```csharp
// names: the .goal files of the app's /system/shortcut/ and of the os's (union), then the app's /shortcut/
foreach (var name in names) Add(new shortcut(await app.goal.list.Find("/system/shortcut/" + name)));  // app first
foreach (var name in appNames) { var goal = await Find("/shortcut/" + name); ...collision check... }
```
`IsSystem => Goal.IsSystem` (the ctor's flag goes). The app's `/system/shortcut/goal.goal` is a system goal (its path is
under `system/`), so it overrides and stays sealed. ShortcutCollision message:
`"{path} names %!{name}%, a system shortcut — to change it, write /system/shortcut/{name}.goal"`. `this.code.md` updated.

## Questions
1. A .goal found with no .pr (not built): Find answers null today (the call says "not found"). Keep, or answer
   "X.goal is not built" (Find returning the failure)? Proposed: keep null now; a better message is a separate change.
2. Walk now also meets `.goal` files with no .pr (`.bot/` copies, unbuilt drafts): left out, one debug line each. OK?
