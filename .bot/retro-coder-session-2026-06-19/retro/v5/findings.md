# v5 Findings — Docs bot sessions 2026-06-20

## Sessions scanned
| File | Size | Result |
|------|------|--------|
| d5563a1b | 79KB | empty — clean commit session |
| 57c33c2e | 2.1MB | 3 findings |
| 1a85a5ec | 567KB | empty — clean docs pass |

## Kept

### D1 — OBP decomposition in written examples
- **Category:** wrong-doc + frustration
- **Session:** 57c33c2e
- **Quote (Ingi):** "wrong here: items.FirstOrDefault(f => f.path.Equals(path)); <= you are decomposing an object, just item.find(path) then the items will know how to find item, item will know how to find it self"
- **Trigger:** Docs bot wrote C# examples showing list reaching into element properties (`f.path.Equals(path)`) instead of putting the operation on the element (`file.is(path)`).
- **Resolution:** Bot corrected to `file.is(path)` / `list.find(path)` pattern.
- **Lesson:** OBP smell #5 applies equally to written doc examples. The docs bot must not write examples that decompose — examples teach by example, and a wrong example is a wrong-doc.

### D2 — Plural API shape in examples (`goal.steps` vs `goal.step.list`)
- **Category:** wrong-doc
- **Session:** 57c33c2e
- **Quote (Ingi):** "this is wrong 'await goal.steps.start();', there is no plural, it's await goal.step.list.start(); because we want to run the list of all steps in a goal"
- **Trigger:** Docs bot wrote `goal.steps.start()` — pluralized the concept name — instead of the correct `goal.step.list.start()`.
- **Resolution:** Corrected to singular+list pattern. Bot acknowledged: "goal.start() wants to run all the steps → that's the list → start the list."
- **Lesson:** PLang collection API is always `<concept>.list`, never `<concept>s`. The `singular + .list` rule applies to every collection in every example the docs bot writes.

### D3 — C# types leaking into PLang shape examples
- **Category:** wrong-doc
- **Session:** 57c33c2e
- **Quote (Ingi):** "why is there IReadOnlyList<file> files? what is that? it is list<file> files, and it has, list.first(() => { f=> f.is(path)})"
- **Trigger:** Docs bot used `IReadOnlyList<file>` (C# interface) in an example meant to show PLang API shape.
- **Resolution:** Corrected to `list<file>` with PLang-native navigation (`list.first(...)`).
- **Lesson:** PLang doc examples use PLang shapes. C# infrastructure types (`IReadOnlyList<T>`, `IEnumerable<T>`, `Dictionary<K,V>`) never appear in user-facing API examples.

## Note
D1 and D3 are two faces of the same failure: the docs bot pattern-matched on C# code it had just read instead of writing PLang-shaped examples. D2 is the separate plural/singular confusion. All three from one session — recurring theme, not a one-off.
