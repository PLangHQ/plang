# tester — test-speed — summary

**Version:** v1

**What this is.** A playground branch (decision 159), cut from item-doors, for making the C# tests and a new app's
birth faster. The measurements and design history are in `test-speed.md` (§7 is the latest).

**What was done.**
- 157: a format (`PLang/app/type/kind/format/this.cs`) binds its type's own encode/decode on first use.
- §6, option 1: `app.assembly.@this` (`PLang/app/assembly/this.cs`) holds plang's code facts, read once. An app
  is born from them through `new app(path, …, assembly: code)`. The type list (`PLang/app/type/list/Registry.cs`)
  and the module (`PLang/app/module/this.cs`) make each app's objects from them, without reflection.
- `empty.Add` (`PLang/app/type/kind/empty/this.cs`) keys the new kind instead of re-keying all of them.
- The C# tests get apps through `PLang.Tests/Shared/Session.cs` (one code read per test process). `plang --test`
  children share the runner's code.
- Birth: 26–29 ms → 3.4–3.7 ms shared (15 ms fresh). Suites roughly halved in CPU.
- Open for Ingi: the holder (the app ctor's `assembly` parameter, the one surface change, re-pinned here), and the
  name.

**Code example.**
```csharp
// a test session reads plang's code once; each test still gets a fresh app
public static readonly global::app.assembly.@this Code = new(typeof(global::app.@this).Assembly);
public static global::app.@this App(string absolutePath, string? environment = null, bool autoWireConsoleChannels = true)
    => new(absolutePath, environment, autoWireConsoleChannels, Code);
```
