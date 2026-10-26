# System.IO goes through path verbs

**Chosen.** Production C# under `PLang/app/**` does not call `System.IO.File`, `Directory`, `FileInfo` and the like. It uses the verbs of `app.type.item.path.@this`, each of which passes `AuthGate(verb)` before it touches the disk. A path in interior C# is a `path`; a `string` appears only at the perimeter (`Documentation/v0.2/bans.md`, "System.IO Is Banned in Production C#"). The gate is the build diagnostic PLNG002, at error severity (`PLang.Generators/Diagnostics/Plng002.cs:48`).

**Why.** `bans.md:56-58`: a handler reaching `System.IO.File` and the others "is reaching **under** the auth gate. That means an out-of-root path the actor never consented to gets read / written silently." The diagnostic's own description says the same: "Direct System.IO reaches bypass FilePath.AuthGate, the only thing stopping out-of-root reads/writes" (`Plng002.cs:50`). The rule is "the filesystem analogue of the `Console.*` ban" (`bans.md:58`).

**Rejected, as recorded.**
- Pure name math on paths (`Path.Combine`, `GetFileName`, …) is allowed, through one forwarder, because "these are string transformations, not IO" (`bans.md`, the allowlist; `Plng002.cs:23-27`).
- The bootstrap-time discovery of shipped teaching markdown (`app.module.MarkdownTeaching`) is exempt: "converting its sync utility shape to async-everywhere buys no security and lots of churn" (`bans.md`, exempt files).
- Other alternatives: reason not recorded.

**Held by.** PLNG002 (a build error where analyzers run: `./dev.sh full`, not `./dev.sh build`; `dev.sh:31`, `:312-314`) and `Plng002SystemIoBanTests` (11 tests).
