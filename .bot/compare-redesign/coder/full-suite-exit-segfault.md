# Full-suite process-exit SIGSEGV — investigation (needs a Windows/non-WSL stack)

**Status:** pre-existing, characterized, NOT root-caused. Cosmetic (never corrupts
results). Blocked on getting a native stack — this Linux/WSL2 sandbox denies
`ptrace`, so `createdump`/`dotnet-dump` cannot attach. **Asking a Windows runner to
capture the crash stack.**

## Symptom

Running a full C# test suite binary crashes with **SIGSEGV (exit 139)** at
**process exit** — *after every test result has already printed*. It eats the
final `Test run summary:` line but does not affect any test outcome.

Affected suites (each crashes at exit): **Data, Wire, Runtime**.
Clean at exit: **Generator** (203 tests, pure Roslyn — never instantiates `app`),
**Types**, **Modules**.

```
./PLang.Tests/Data/bin/Debug/net10.0/PLang.Tests.Data --timeout 150s
# ...all tests print...
# Segmentation fault (core dumped)   exit=139
```

Deterministic for the full Data suite (3/3 runs crash).

## What is RULED OUT (tested this session)

1. **Not a single test.** Bisected the Data suite by namespace → class → method.
   `SnapshotAt_IsPure_SameInputsSameResult` *did* crash standalone, but that was a
   **separate, real bug** (a stack overflow, now fixed — commit `5113a3ac0`). After
   that fix, **every namespace passes when run alone** (exit 2 = failures, not 139).
   The full suite still crashes. So the exit-crash is not any one test.

2. **Not the test platform/runtime host.** A single trivial test class exits clean
   (exit 0). Only the large, varied union crashes.

3. **Not leaked SQLite handles from undisposed `app` instances.** This was the
   leading hypothesis (each `app` lazily opens a native `SQLitePCLRaw.e_sqlite3`
   connection for its settings store; ~950 undisposed apps → finalizer race at exit).
   **Refuted by a direct probe:** a single process creating **800 undisposed apps**
   that each touch `app.SettingsStore` (forcing the SqliteConnection) exits **cleanly**
   (exit 0) — disposed or not. 800 > the whole 952-test suite, so app count and the
   settings connection are not the cause.

4. **Not a managed finalizer the GC can reach.** An `[After(Assembly)]` hook doing
   `GC.Collect(); GC.WaitForPendingFinalizers()` did NOT prevent the crash —
   inconclusive on its own (TUnit roots test instances through the hook), but
   combined with (3) it argues against a simple finalizable-handle leak.

## What is ESTABLISHED

- Crash correlates with suites that instantiate `app.@this` **and do varied native
  work**. Generator (no `app`) is clean; the `app`-heavy varied suites crash.
- It is **accumulation across the whole run**: no namespace alone crashes; the union
  does. But it is NOT app-count accumulation (point 3). So it is accumulation of
  something the *variety* of tests produces.
- Native components loaded in these suites: **`SQLitePCLRaw.e_sqlite3`** (ruled out
  as the trigger above) and **`Microsoft.Playwright`**. Everything else (ImageSharp,
  etc.) is managed. `HttpTestServer` (Kestrel sockets) is also exercised.

## Leading remaining suspects (untested — need the stack)

- **A native-resource teardown race** other than SQLite — Playwright's node/driver
  process handle, or Kestrel sockets from `HttpTestServer`, finalized against a
  partially-unloaded native lib at process exit.
- **A .NET runtime GC/shutdown bug** with a large/specific heap — these manifest as
  exactly this shape (correct results, SIGSEGV only at teardown). If so it's a
  runtime issue, not an app bug; the fix is an env/runtime-version workaround.

## How to capture the stack on Windows (the ask)

The frame is the whole answer; this sandbox can't get it. On a Windows box (or any
non-`ptrace`-restricted Linux):

```powershell
# from repo root, after a Debug build
$env:DOTNET_DbgEnableMiniDump = 1
$env:DOTNET_DbgMiniDumpType   = 4        # full dump
$env:DOTNET_DbgMiniDumpName   = "C:\temp\plang-exit-%p.dmp"
.\PLang.Tests\Data\bin\Debug\net10.0\PLang.Tests.Data.exe --timeout 150s
# then:
dotnet tool install -g dotnet-dump
dotnet-dump analyze C:\temp\plang-exit-<pid>.dmp
#   > clrstack -all          (managed frames on the crashing thread)
#   > threads / !threads
#   > the crashing thread's NATIVE frames name the lib (e_sqlite3 / playwright / coreclr GC)
```

What we need back: **the crashing thread's stack** (managed + native). That single
frame tells us whether it's SQLite, Playwright, Kestrel, or the runtime GC, and the
fix follows directly (deterministic disposal of that resource, or a runtime/env flag).

Reproduce: just run the full `PLang.Tests.Data` (or `.Wire` / `.Runtime`) binary;
it crashes at exit deterministically. No special filter needed.

## Workaround until then

It's cosmetic — results all print before the crash. Read pass/fail from the per-test
`failed `/`passed ` lines, not the summary line. The baseline failing-test sets are
saved in `test-baseline.txt` (same dir).
