# No `Console.*` writes

**Chosen.** Production C# does not call `Console.WriteLine`, `Console.Write` or `Console.Error.WriteLine`. Diagnostics go to `context.App.Debug.Write(...)`, gated on `--debug`; user-facing text goes to the actor's `output` channel. Queries (`Console.IsInputRedirected`, `Console.IsOutputRedirected`) and the process boundary in `PlangConsole/Program.cs` stay (`Documentation/v0.2/bans.md`, "Console.* Is Banned in Production C#").

**Why.** `bans.md:135`: "Channels exist so that I/O is **redirectable** — a user can re-register the `output`/`error`/`debug` channel to a file, an in-memory buffer, an HTTP response, or a goal. Any `Console.WriteLine` / `Console.Write` / `Console.Error.WriteLine` in production C# silently bypasses that surface and breaks the contract."

**Rejected, as recorded.**
- Routing user-facing output through `Debug.Write`: "Do **not** route these through `Debug.Write` — the `IsEnabled` gate would silence them" without `--debug` (`bans.md`, the three flavours of write).
- Test fixtures that capture stderr by `Console.SetError(...)`: they "are broken under the channel model", because the channel was registered with the standard error stream at boot (`bans.md`, end of the section).
- Other alternatives: reason not recorded.

**Held by.** Review only. No analyzer and no test was found. A search of `PLang/app/` finds no `Console.Write` or `Console.Error` call.
