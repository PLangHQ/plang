# windows

Cut from runtime2 b4a549d1c (decider-key's merge). Ingi, bugs 52: "2. yes". A path plang writes uses `/` on every OS, with the OS form only at the System.IO call, and the whole C# suite passes on Windows. Ingi, 2026-10-26, "2. yes": a user's before/after each goal fires only for the app's own goals.

## Why

On Windows, at runtime2 plus bugs, 100 C# tests fail (`/shared/wintest/failures-2.md`, b954c06ca). They come from a few causes, and some of them are real bugs a Windows user meets:

```
- call goal Process            (a snapshot taken on Windows)
```

**Before:** the snapshot holds `"goalPrPath": "\\.build\\process.pr"`. **After:** `"/.build/process.pr"` on every OS.

## Stages

1. **A user's event sees only the app's own goals** (doc2's finding C). `before each goal call Log` fires before plang's own system goals too, three times before the user's first goal. A user's binding to "each goal" sees the app's goals only; a system goal is bound by naming it. This doesn't need Windows, so it goes first and is red on Linux.

2. **A path's plang form is plang's: `/` on every OS.** A path keeps two forms, the OS form (`Absolute`) and the plang form (`Raw`, what a goal writes and a `.pr` holds). Today the plang form is derived with the OS's functions (`PathHelper.Combine(Raw, child)`, `GetDirectoryName(Raw)`, `ChangeExtension(Raw, …)`, `file/this.Derivation.cs:35–54`), so on Windows it gains `\`. The plang form gets its own `/` math, in one place; the OS form keeps the OS's. Every derivation goes through it: Combine, Parent, WithExtension, Relative, Pr. Also: a plang form never made by gluing `/` onto an OS form (`/C:\…\TestProvider.dll`). Failures this covers: PrPath ×5, Combine, Parent, sub-goal `.build` ×2, FileUrl ×3, listing entries, a narrowed read's path, a dll's path ×3, a goal beside an OS goal. They're red on Windows only (Linux's `PathHelper` already writes `/`), so their red is the Windows run, plus a Linux test that a `\` in a typed child never reaches the plang form.

3. **Reading a file in any line ending, and a number in any culture.**
   - A `.goal` written on Windows has CR LF: its steps read as LF lines (ToString_ReturnsGoalText; the formal reader's indent tests).
   - A number in text is read in the invariant culture. Ingi's machine is Icelandic, where `3.14` failed to read as a number (Deserialize_Decimal, Deserialize_Double, an llm tool's `1.5` argument). That's a real bug wherever the culture isn't English, not only on Windows.
   - The repo checks out LF everywhere (`.gitattributes`), so goldens and pins compare like with like. That covers 13 type pages, the four prompt pins, the surface golden and two module page tests.

4. **Permissions on Windows.** Nine store tests find no grant they just saved, and three gate tests deny a path a grant exists for (`C:\p`). The cause isn't traced. It's either a test's root `/` (which is `C:\` on Windows) or a grant stored in one form and looked up in another. The coder traces it first; a real mismatch is a security finding (it fails closed, but it is broken).

5. **Tests that assume Linux say what they mean on each OS.**
   - 24 start `/bin/sh`, `/bin/echo` or `/bin/true`.
   - An app root of `/`.
   - `//tmp/x`, Unix's OS-rooted form, which is a UNC path on Windows.
   - `/etc/passwd` asked interactively.
   - A file made unreadable with chmod.
   - The `.data ` lookalike: Windows drops the trailing space, so its refusal is right there.
   - The gate's link count, which has one part fewer under a drive root.
   - Byte bounds measured on Linux: 152 KB at a sub app's first ask, 2.4 MB reading settings facts.
   - `Test_Complete`'s ≥ 1 ms.
   Each test either runs its intent on both OSes, or says why it is one OS's, by an attribute, never by runtime code for tests.

6. **The rest of the Windows list.**
   - A line to a child's stdin uses the OS newline (`terminal/type/process/this.cs:232`), a stray CR for a `wsl.exe` child.
   - The restore race in `PLang.Tests/All.proj` (parallel restore fails on Windows).
   - Security's probe of the stores reached by a case variant on a case-blind disk (`.DATA\setting`).

7. **The Windows run** (the host): the whole sweep with only Wire's 6 failing, `plang --test` at the baseline, a real `Draw` with the cursor line, and the real window (bugs' step 4, never reported).

## Out

The six snapshot reds (Wire's, on every OS) stay where they are, with the snapshot branch.

## Locked surface

Stage 2 is a behaviour fix inside `path` (a core value type): no member changes. Stage 1 touches events, which are locked: the behaviour is Ingi's yes, and any new member comes to him first.
