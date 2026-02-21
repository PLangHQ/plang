# Security Learnings — data-envelope-architecture v1

## 2026-02-21 — Unbounded recursion is the #1 pattern in this codebase

**Vector:** UnwrapJsonElement, RehydrateNestedData, GetChild, ResolveVariablesInPath, Clr() — all recurse without depth limits
**Severity:** high
**Status:** open
**Details:** Every time PLang processes structured data (JSON, Data objects, navigation paths, type names), it uses recursive descent. None of these have depth counters. StackOverflowException is unrecoverable in .NET — the process crashes, no catch block can save it.
**Fix:** Add `int depth = 0` parameter to every recursive method. Check against a const max (100-128 for JSON, 100 for navigation, 20 for generics).
**Lesson:** When reviewing PLang code, check EVERY recursive method for a depth parameter. If it doesn't have one, it's a bug. The pattern is always the same: add depth param, increment on recurse, throw InvalidDataException at limit.

## 2026-02-21 — MemoryStack system variables have no write protection

**Vector:** MemoryStack.Set() accepts any name including !-prefixed system variables
**Severity:** high
**Status:** open
**Details:** PLangContext registers !engine, !context, !fileSystem, !memoryStack etc as system variables. The ! prefix hides them from enumeration (GetNames/GetAll), but Set() has no guard — any code can overwrite them. The variable/set action passes Name directly from .pr parameters, so a crafted .pr file can set %!engine% = null.
**Fix:** Guard Set() with if (name.StartsWith("!")) throw.
**Lesson:** "Hidden" is not "protected". The ! prefix is a visibility convention, not a security mechanism. Always check if hiding from enumeration also prevents direct write access.

## 2026-02-21 — ObjectNavigator reflection is an unintended capability amplifier

**Vector:** ObjectNavigator exposes ALL public properties of ANY object stored in Data.Value
**Severity:** medium
**Status:** open
**Details:** If Engine, PLangContext, or MemoryStack are navigable (they are — stored as Data values in system variables), reflection gives access to FileSystem, Libraries, Goals, System/Service actors. This is not just information disclosure — it's a capability leak.
**Fix:** Add a type blocklist or property blocklist to ObjectNavigator. Or use a [Navigable] attribute whitelist.
**Lesson:** Reflection-based navigators are dangerous in a system where objects carry capabilities. Every public property on Engine/Context is a potential escalation path. Principle of least privilege: only expose what's needed.

## 2026-02-21 — Gzip bomb protection is good but incomplete

**Vector:** MaxDecompressedSize=100MB correctly enforced in GZipDecompress
**Severity:** info
**Status:** mitigated (partially)
**Details:** The chunked read with cumulative size check is well-implemented. But the protection only covers raw decompressed bytes — no corresponding limit exists for the downstream JSON parsing. A 90MB JSON document (under the limit) can allocate 300MB+ of Dictionary/List objects during UnwrapJsonElement.
**Lesson:** Size limits must be applied at every transformation stage, not just the first one. Bytes → JSON → CLR objects is a 3x amplification chain.

## 2026-02-21 — Duplicate code is a security debt multiplier

**Vector:** UnwrapJsonElement exists in both Data.cs and fromJson.cs with identical logic
**Severity:** info
**Status:** open
**Details:** When a security fix is applied to Data.cs (e.g., adding depth limits), the duplicate in fromJson.cs won't get it unless someone remembers. This is exactly how security regressions happen.
**Lesson:** When finding duplicate code during security review, flag it explicitly. The fix must reference both locations, or better yet, consolidate to a single implementation.

## 2026-02-21 — Signature/Verified properties without implementation are dangerous

**Vector:** Verified is a public settable bool with no crypto backing
**Severity:** medium (time bomb)
**Status:** open
**Details:** Today, no code checks Verified. But the property exists and tests exercise it. A future developer will assume it works and build on it. The property name implies a security guarantee that doesn't exist.
**Lesson:** Never ship security-related properties without implementation. Either implement verification, or don't create the property. A "placeholder" security feature is worse than no feature — it creates false confidence.
