# The type list's tables made at build time — a scoped proposal (for the architect, then Ingi)

Pattern: a birth fact, not a search (5); the first asker loads it (11). The type list is the locked core (the type system) and the generator is Ingi's; nothing here is built before his yes. I read and measure; the coder builds.

## What it replaces (measured, cold root app, quiet machine, `Bench cold root`)
`origin.code` makes `new type.list.@this(this)`, whose `Scan()` builds `Code.Library` by reflection over the whole PLang assembly (`type/list/Code.cs:141–181`):
| part | ms (JIT in it) |
|---|---|
| `assembly.GetTypes()` | ~10 (8) |
| per class `FamilyOf` / `NameOf` → `Item`, `Subclasses` | ~25 (6) |
| `Kind` classes (`IsAssignableFrom`, `GetConstructor`) | ~3 (1) |
| `new Mark(t)` for every class → `Marks` | ~18 (8) |
| `Readers` (serializer folders: ITypeReader ctors, static `Read`) | ~10 (5) |
| the rest of `Scan()`: entries made from those tables | ~38 (24) |
So ~105 ms of a ~210 ms cold root app (the CLI's one app) is this scan; a second root app in the same process still rescans (~30–60 ms).

## The shape
The generator (a new `Emission/Library/this.cs`, beside `Action` and `Property`) writes, for the assembly it compiles, one static class holding the Library's six lists as data: `Item` (name, class), `Subclasses` (class, family, kind, reader), `Kind` (class, owner), `Marks` (the classes that have a codec, are a list, or are a choice), `Tokens` (serializer folder, reader class), `Payloads` (folder, kind, static `Read`). Each is derived from the same facts the reflection reads today (`FamilyOf`, `NameOf`, `[Format]`, `[PlangType]`, namespaces ending `.serializer`), at compile time, from Roslyn symbols.
`Library` keeps one shape. The PLang assembly's `Library` is born from the generated tables; an assembly a package adds (`code.dll`) is still scanned by reflection into the same type. That is the one fork to name: two births for one type (a type-switch-free one: `Library.Of(assembly)` asks the assembly for its generated tables, else scans). The per-class `Class` facts (aliases, owned CLR types, defaults) stay lazy, as they are.

## Why it is not free
- A generated table of thousands of `typeof(X)` entries is one large straight-line method; its JIT may cost tens of ms. Mitigation to measure first: emit the six lists as separate small static arrays, or by namespace, and read each at its first ask; or emit names only and bind `Type` lazily. A prototype of the emitted file (copied by hand into a scratch project) would give the number before the generator is written.
- The remaining ~38 ms (making the entries from the tables) is not removed by this; it is the next thing to make lazy (pattern 11).
- Expected saving: ~60–90 ms of ~210 ms cold root birth, and the JIT of the scan methods; the CLI start ~600 → ~520 ms. Not measured; it is arithmetic on the table above.

## How it stays honest
A C# pin builds the reflection `Library` and the generated one for the PLang assembly and asserts the six lists equal (same entries, same order). A class added later cannot make them drift; the pin names it. A surface golden re-pins `type.list`'s new internal member with the decision that approved it.

## Asked of Ingi
Whether the generator may emit the Library's tables (a locked-core addition), and whether it goes into call-cost or its own branch.
