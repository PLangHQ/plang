# coder v2 — the maker hands every value its type (item-doors 32, 33)

Decisions are counted per branch now: item-doors 1–33 (old 867–898 → n − 866).

## The rule
- **When:** anything makes a value (list, text, number, …).
- **What:** the maker hands it the app's type object (`context.App.type.list[...]`). The constructor takes `type.@this type`, required. It never builds its own type (`new type.@this(...)`) and never falls back to one (`type ?? new ...`).
- **Why:** one type object per type, made when the app starts. A value making its own copy is how image lost its mime, and why `type.Get` looks the type up again on every navigation.

## Steps (each one green and pushed)
1. **App start order.** Make the type list before the actors (`app/this.cs:288` before `:284`). The type list takes its own entry as its type, the same way the root type is its own type (item-doors 18).
2. **list, list<T> and the 12 subclasses.** Every constructor takes the type. The `list<T>` Type override goes (`this.Generic.cs:21–24`). Makers pass the app's type:
   - goal.Parse (`goal/this.cs:534`) and the .pr reader make the node lists. The `= new()` field defaults go (goal :39 :149, step :56, action :52, pick/list :93, the setting/record list fields).
   - goal/list, shortcut/list, test/list, actor/list take it from `app`.
   - readers, `type.Empty`, item/this.cs:240, and the module code pass it from their context.
3. **Case 3: trace to the real maker.** A type's kinds (`type/kind/list`), `variable.code`, the parser. A type does not know its list. Report any maker with no app (file:line).
4. **Every other value constructor** (step 3 at the latest): number, image, choice, null, text, date, setting, separator, content, … take `type.@this type`, required.
5. **Step 2:** `type/this.cs` KindOfClass goes. The kinds come from the app's list.

## Steps 4–5 together, one value type at a time (each pushed green)
Kinds per app and the required constructor are one piece: `KindOfClass` and number's static kinds are used only
while a value builds its own type.
- **For each type:** the constructor takes `type.@this type`, required; its makers pass the app's type (the type's
  `Make`, a reader's context, `context.App.type.list[...]`); its conversion into plang goes; its `Create(raw)` takes
  the type.
- **Order, smallest first:** guid, date, time, culture, base64, prose, note, tag, status, null, binary, image,
  duration, datetime, choice, bool, path, dict, text, number; then setting, content, source, separator.
- **Last:** number's 15 static kinds and `KindOfClass` lose their last user and go; item's `Type` becomes
  `_type ?? (type.@this)this` (item-doors 34).

## Checks
- C# sweep against the baseline (6 Wire snapshot/resume and ImageFromBytes are pre-existing).
- byte-equal `.pr` (`plang build` + diff) and the signing tests.
- surface golden re-pinned, naming item-doors 32/33.

## Limits
No type holding its list. No optional type with a fallback. Never hand-edit a `.pr`.
