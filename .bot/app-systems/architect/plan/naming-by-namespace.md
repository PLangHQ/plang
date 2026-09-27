# Types named by namespace, short words as aliases (decision 100): the architect's own trace

Written before reading the coder's trace (Ingi: "do the same and not read coder result until you have finished yours, then compare"). The comparison is at the end.

## Why

Ingi, 2026-09-27: a type's name is its namespace (`app.channel.type.goal`), and a short word is an alias pointing at the one right type (`goal` → `app.goal`). Today the scan guesses every type's name from the last folder of its namespace, so two different things can land on one word. Making channel an item would register `app.channel.type.goal`, `…test` and `…file` as `goal`, `test` and `file`, which are three names already taken, and the app would stop at construction. Guessed words collide; namespaces can't.

## What exists today (read 2026-09-27)

- **The scan names a class** (`type/list/Registry.cs:258-303`): `NameOf` → `[PlangType("x")]` if declared, else an `@this` class's last namespace segment (`InferName`), else nothing. `FamilyName` (`:276-287`) turns three cases into kinds instead of types: a path scheme, a setting class, a typed program list. `Admit` (`:82-89`) refuses one name for two classes.
- **A type answers a name** (`type/this.cs:600-607`): `Names(key)` = its `Name` or one of its `Alias`, case-insensitive. `Match` is the async face of the same check. Aliases are a static `Alias` on the class (`:536`, read with `Declared`): text `["string"]`, list `["array"]`, dict `["dictionary","map"]`, binary `["bytes"]`, bool `["boolean"]`.
- **An item names its own type** (`type/item/this.cs:298`): `new(NamespaceTail(GetType()), GetType())`. About 25 overrides spell a literal word (`new("goal", typeof(@this))`, `new("hash", typeof(@this), Algorithm)`, `new("setting", GetType(), Path)`, …). `computed.cs:29` falls back to `NamespaceTail`.
- **The wire** (`type/this.cs:41-49`): the type slot writes `{name: Name, kind?, strict?, template?}`, so `.pr` files and every Data on the wire carry the word. The reader turns a name back into a type through `Names`.
- **The prompts:** `PlangName`/`Face` (`type/list/this.cs:201-233`) print a C# class as its plang name for the facts and the prompt's types section. They return `own.Name`, or the hard-coded family words `"item"`, `"type"`, `"choice"`, `"list"`, `"dict"`, `"clr"`.
- **The concept types:** `type<T,L>` is named `item.NameOf(typeof(T))` (`type/this.Generic.cs:23`), so `goal`, `module`, …; `Replace` finds the scanned entry by that name.
- **Closed sets** already declare their names explicitly (`[PlangType("visibility")]`, `"operator"`, `"level"`, …) and are kinds of choice. No item class uses `[PlangType]` today.
- **Sealed** (`Registry.cs:33-36`) is a list of words a loaded DLL may not claim.

## The rule, made concrete

1. **Identity is the namespace.** A type's `Name` is its class's full namespace: `app.type.item.text`, `app.goal`, `app.channel.type.goal`, `app` for the app. It's unique by construction, and it's the plang path, so the three paths agree.
2. **A short word is a declared alias, the one mechanism: the class's static `Alias`.** The first entry is the spelling plang prints (the wire's type slot, the prompts, `Face`). text becomes `["text", "string"]` and goal becomes `["goal"]`. No `[PlangType]` for items: one way to declare a word, not two. `[PlangType]` stays for closed sets, where it names a set, a kind of choice.
3. **Nothing is guessed.** `InferName`/`NamespaceTail` stop naming types. A type with no `Alias` is reachable and printed only by its namespace. That holds for the channel types, settings classes, internal classes and the event base.
4. **An alias names one type.** `Admit` refuses an alias another type already holds and an alias equal to another type's namespace, and the scan fails loudly on either. `Sealed` becomes the words a loaded DLL may not take as an alias.
5. **Printing:** the wire, `Face` and the prompt print the spelling, which is the first alias if there is one, else the namespace. So `.pr` files, prompts and `as text` stay byte-equal. The type's Out-view face (`%!app.type.text%` written out) shows `name: app.type.item.text` plus its aliases: **plang-visible, say so to Ingi.**

## Steps

1. **Declare the words.** Every type registered today keeps its current word as its first alias, written explicitly on its class (about 45 classes: the scalars, containers, concepts, goal/step/action, setting, event, table, registration, …). The migration is mechanical, and the proof is `.pr` byte-equality.
2. **`type.@this` born from its class:** a constructor that derives `Name` (namespace) and spelling (first alias) from the class. `item.Type` and its ~25 literal-word overrides become `new(typeof(@this))` or `new(typeof(@this), kind)`, which removes 25 hand-spelled words. `computed` keeps its declared name.
3. **The scan and `Registry.Add`:** they register by namespace, and `Admit` checks namespaces and aliases.
4. **`PlangName`/`Face`** print the spelling. The hard-coded family words stay, because they are those types' aliases.
5. **`type<T,L>`** is named by T's namespace, and `Replace` matches on it.
6. **Then** channel can become an item: `app.channel` with the sealed alias `channel`, and its types unaliased. App can become an item too: `app` has no clash. That depends on Ingi's app/channel→item answer.

## Checks

- **`.pr` byte-equality:** rebuild `Tests/Simple`, and load and write back the installed `.pr` files. Before anything else, list the distinct type names the `.pr` files hold. I couldn't do that myself: the hook blocks shell commands that mention `.pr`. Each of those names must be a declared alias.
- **Prompts byte-equal** (pick twins, the settings/keys twins). No Python change is expected.
- **The consumers crossing into plang:** `%!app.type.X%` in `.goal`/templates, `as X` in goals, `list<X>` spellings.
- **Tests** asserting `type.Name == "text"` change to the spelling or the namespace. Count them in the trace.
- **Two traps:** the scan builds the alias table while it fills, and `Admit` must see aliases from the first entry on. Case-insensitivity stays, and a namespace compares case-insensitively too.

## Not solved by this

- **The module's action list clash** (`app/module/action/list/` is the `list` module's handler folder). That's C#'s own namespace, a folder-layout question, and it stays with Ingi.
- **`FamilyName`'s kinds** (path schemes, settings, typed lists): they're about meaning, not naming, and stay. The "subclass with no name is a kind of its base" rule isn't needed for the channel clash any more.

## Comparison with the coder's trace (read after the above was pushed, a21e9e255)

**The coder's is better, on points I missed:**
- **The Python twins name types by the same guessed-tail rule, read off the C# source** (`params.py:plang_type`, `build_pr.py:plang_type`/`type_files`). I wrote "no Python change expected". Wrong: that's the language-boundary miss again (see memory `feedback_trace_language_boundary`). The fix is the coder's: a C# twin test writes `types.json` (namespace → word, aliases) and Python reads it, the `settings.json` pattern.
- **Non-`@this` item classes** (`[PlangType]` with no name: LlmMessage, Ask, Identity, Error, …) take the lowercase class name today. I only traced `@this`. The coder names them namespace + class (`app.module.action.llm.llmmessage`).
- **The `.pr` census** (58 files walked as JSON): bool, item, text, variable, number, list, choice, path, dict, goal, setting, action, type, plus the kinds `list<test>` and `list<llmmessage>`. I couldn't take it (the hook blocks `.pr` in shell) and left it as a check.
- **Where the word is declared:** the coder uses `[PlangType("text")]` for the word and keeps the static `Alias` for other spellings. Mine put everything in `Alias`, with the first entry as the spelling, which gives position a meaning. The coder's matches how closed sets already declare their names (`[PlangType("operator")]`), so a declared word is declared the same way everywhere. **Adopted.**
- **A whole-catalog render diff** before and after, as a byte-equal check.

**Mine adds, and it goes in:**
- `Sealed` becomes the words a loaded DLL may not claim as its word or alias.
- **The type's Out-view face** (`%!app.type.text%` written out) shows `name` = the namespace under the rule. That's plang-visible, so Ingi hears about it.
- `Admit` must see words and aliases from the first scanned entry on.
- **The ~25 hand-spelled `Type => new("goal", …)`:** they derive from their class now (`new(typeof(@this))`, read off the class's declared word), not as a follow-up. Otherwise the word is declared twice, once in the attribute and once in the literal.

**Same in both:** the namespace is the name and identity; the face is `word ?? Name`, so `.pr` files and prompts stay byte-equal; the alias guard; channel and app unblocked by the rule alone, with `FamilyName`'s kinds untouched.

**Lesson:** my trace stopped at the C# boundary once more. The twins are code.
