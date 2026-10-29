# A goal in a snapshot comes back as what finds it (snapshot 91)

Patterns: saved data maps by a view and its tags (1); a type per kind, decided at birth (2); the value answers (Select).

1. **The kind:** `PLang/app/goal/kind/key/this.cs`, a `kind<goal>` named `key`, enlisted by the type list's kind scan.
2. **Who writes the kind:** the snapshot view, once. `View.Type(record, context)` is the type a record shows under
   (default: the record's own, or its error's); the snapshot view answers a found value's (one that tags `[Snapshot]`)
   type under its `key` kind. The plang writer asks the view, as it asks `View.Name(record)`. No type name in the wire.
3. **The read:** `goal/serializer/Key.cs`, the `(goal, key)` token reader: reads the record as a dict and sets each
   member the goal's key names by its own type (`pr` a file, made unread from `{path}`). The goal's `.pr` reader is
   untouched.
4. **The find:** `goal.Select` takes the record beside the text: `goal.list.Read(pr.path)`, then `Find(name, top)`, then
   the hash; a changed one is `GoalHashMismatch` (409). The re-sign that lets a changed goal through is stage 11.
5. **Tests:** FoundElsewhereTests' goal test green, the same instance; a changed goal refused. Surface re-pinned citing
   snapshot 91.
