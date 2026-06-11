# Slice 2 worklist — consumer tail kills (stage-9-demolition §slice 2)

Working rules (agreed with Ingi): filtered `./dev.sh test <Class>` per fix,
full gates only at the slice boundary; transitional-behavior test pins get
skip-with-reason, not rewrites. plang gate via half-runs (/tmp/ph1, /tmp/ph2
symlink sets — full single-process run hits the pre-existing teardown
segfault that truncates the log). REBUILD PlangConsole before any plang run
(dev.sh test does not).

## Kills (each dies WITH its callers; none may gain a new caller)

Caller census (production, non-test):
OpenAi.cs(7), http/code/Default.cs(6), Ed25519.cs(4), builder/code/Default.cs(4),
variable/list/this.cs(2), CommandLineParser.cs(2 — CLI stays outside Data, just
re-route its reads), type/this.cs, object/serializer/json.cs, list/Json.cs,
dict/Json.cs, catalog/this.cs, ui/Fluid.cs, timer/sleep.cs, test/tag.cs,
test/discover.cs, mock/intercept.cs, event/on.cs, builder/goals.cs,
MarkdownTeaching.cs, goal/list/this.cs, data/Wire.cs.

Order:
1. `AsEnumerable()` + `IsPlangIterable` + `IsPlangAssignable` — iteration is the
   collection types' own member; foreach asks the item (text already refuses by
   not implementing IEnumerable — that knowledge moves with it).
2. `ToBoolean()` CLR arms (null/false/0/"") — each type's IsTruthy owns it;
   keep only the IsInitialized guard + item.IsTruthy + the async forward.
3. `SnapshotClone` + `_snapshotClone` options — immutability removed its
   reason; snapshots copy bindings (pointers).
4. `GetValue<T>()` / `GetValue(Type)` — callers → `Value<T>()` (plang T) or the
   item's own lowering at a real .NET edge. NOTE: `Data<T>.Value()` itself
   falls back to GetValue<T> — replace that tail with Conversions/catalog
   dispatch before deleting.
5. `Data.Clr<T>()` / `Clr<T>(fallback)` — sites → `(await x.Value())` + item's
   internal Clr. (Keep item.Clr/ClrConvert — sanctioned.)
6. `UnwrapJsonElement` — JsonElement narrows at entry/parse into dict/list;
   the ctor/SetValue lift keeps a json-element arm via the READER path, not a
   public static everyone calls.
7. Implicit operators TO CLR on text/bool (binary?) — inbound stays.
8. `number : IConvertible` — audit members; keep only checked/loud needs.
9. Peek()/Open() tighten toward `item?` (the carrier stops unwrapping) — as far
   as the remaining raw-shape consumers allow; whatever can't tighten yet gets
   listed for slice 3+.

## State at slice-2 start

- Slice 1 complete: commits 3b981e57e, 2eebbbb29, 5a30503fc (all pushed).
- Both suites green (C# 0 fails/2 follow-on skips; plang 334, 4 skips, via
  halves). PROVISIONAL: scalar-equality-on-unread-reference took the model
  position (compare parses) — ReadConfigJson/ReadCsv goals re-pinned, marked
  in goal comments; flip if Ingi decides raw-face.
- Remaining slices after 2: templates+async Write (3), collections reference
  semantics / CopyStructure removal (4), follow-ons text.Value/ToRaw (5).
  Commit+push at every slice boundary (Ingi's standing instruction).
