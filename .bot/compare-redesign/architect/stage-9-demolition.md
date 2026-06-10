# Stage 9 demolition worklist — what must not survive the stage

Audit of `PLang/app/data/` and the value types against [`coder/data-value-model.md`](../coder/data-value-model.md), member by member (architect + Ingi, 2026-06-10). **The verdicts are the contract; the order and mechanics inside each slice are yours.** Line numbers are anchors as of today's commit — they will drift; the member names are the truth.

Checkboxes are yours to tick. If you hit a member where the verdict seems wrong against real callers, flag it back — don't silently keep it.

## Dies in slice 1 — the core collapse

These ARE the Data shape change; they go down together.

- [ ] `_value` (`data/this.cs:27`) and `_type` (`:41`) — replaced by the one typed-instance member. The instance IS the value; the type entity is minted on ask (`!type`), not stored.
- [ ] `_raw` (`:36`) — off Data, onto the types that load (`file`, `url`) / born-with-bytes values. Private there; gate exempts it by name.
- [ ] `Materialize()` (`:389`), `ForceMaterialize()` (`:540`), `_materializeCount` (`:40`) — parse lives inside the type's own `Value()` path. Test instrumentation dies with it.
- [ ] `NarrowReference()` (`:290`) — Data running the narrow is Data doing the file's job. Narrow is now: the type's `Value()` answers as a different type, Data rebinds. One assignment.
- [ ] `As<T>` (`:813`), `AsT_Impl` (`:1051`), `WrapAs<T>`, `_resolvingValues` cycle guard (`:1034`) — replaced by `Value<T>()` (T : plang type; answer-is-T/chain-facet → hand over, else the answer's own Convert hook, else `Data.Error`; **never rebinds**). The cycle guard dies because single-pass render has no recursion to guard.
- [ ] `SetValue(Func<object?> factory)` (`:460`) + `_valueFactory` (`:28`, and the lazy subclass copy `:1802`) — factory-lazy on Data is a second lazy mechanism; lazy is the type's job (file loads itself). The generator's lazy-param carrier gets rethought on the `Value<T>()` shape.

## Dies in slice 2 — with the consumer tail

Each of these dies when its callers convert; none may gain a new caller.

- [ ] `GetValue<T>()` (`:643`), `GetValue(Type)` (`:658`) — Data converting contents to CLR targets, `default` on failure: package-opening + silent CLR exit + swallowed error. Callers → `Value<T>()` (plang T) or the item's own lowering at a real .NET edge.
- [ ] `Clr<T>()` / `Clr<T>(fallback)` on Data (`:349`, `:358`) — lowering belongs to the item; a courier-level exit door makes lowering easy everywhere instead of at edges. Sites → `(await x.Value())` then the item's internal `Clr<T>`.
- [ ] `AsEnumerable()` (`:693`), `IsPlangIterable` (`:674`), `IsPlangAssignable` (`:682`) — `is IEnumerable`/`is not string` sniffing above the type. Iteration is the collection types' own member; foreach asks the item. (text already refuses char-iteration by not implementing IEnumerable — that knowledge moves with it.)
- [ ] `ToBoolean()` sync CLR arms (`:1454` — null/false/0/"" judgments) — truthiness per CLR type judged by Data. Each type's `IsTruthy` owns it (number: ≠ 0; text: non-empty). The async forward (`ToBooleanAsync` → IBooleanResolvable) survives as a pure forward.
- [ ] `UnwrapJsonElement` (`:1584`) — the canonical Unwrap smell; JsonElement narrows at entry/parse into dict/list.
- [ ] `SnapshotClone` (`:1577`) + `_snapshotClone` options (`:1571`) — defensive deep-clone via json round-trip; immutability removed its reason. Snapshots copy bindings (pointers) — values can't change underneath them.
- [ ] **Implicit operators TO CLR** on the types — `text → string?`, `bool → bool` (number's surface too, if any). Every call site is a silent CLR exit; "the item lowers itself, checked, loud" forbids it, and Stage 7's worked example already removed text's. **Inbound stays** (`string → text` is the entry lift). In: yes. Out: never silently.
- [ ] `number : IConvertible` — `Convert.ToInt32(number)` is a silent lowering door (your own todo, 2026-06-10, agrees). Audit each interface member; keep only what a checked, loud, explicit path needs.

## Transitional — dies later; do NOT extend, do NOT add callers

- [ ] `TryFullVarMatch` (`:791`) + the runtime %ref% scan — the legacy template detection. Dies when builder stamps land (slice 3); until then it only serves unstamped .prs.
- [ ] `SetValueDirect` (`:556`) — courier reconstruction; marked transitional debt, the schema-layer branch deletes it.
- [ ] Wire envelope recognition: `IsDataMarked` (`:64`), `IsWireShape` (`:975`), `WireSlot` (`:964`), `TypeFromWire` (`:993`) — retire-envelope-recognition is already a todo; schema branch kills them.
- [ ] `EnsureSigned` (`this.Transport.cs:49`) — sign-if-missing fires inside `Wire.Write`; owners don't call this at egress. Verify zero callers, then delete; signature leaves Data on the schema branch anyway.

## Stays — do not delete in this stage

- `Peek()`, `Value()`, `Value(fallback)` — the doors.
- `Compare` / `CompareValues` — THE comparison entry by design (Stage 5).
- `FireOnChange` / `FireOnCreate` / `FireOnDelete` — Data is the cell; lifecycle is its job.
- `item.Clr(Type)` / `item.Clr<T>` (internal) + `ClrConvert` — the sanctioned lowering, the item lowering itself.
- The per-type `Convert` hooks and the catalog Conversion dispatch — `Value<T>()`'s mechanics ride on them.
- `IsTruthy` / `AsBooleanAsync` on item — truthiness where it belongs.
- `ToError<T>` / `FromError<T>` — Result plumbing.
- Inbound implicit operators (`string → text`, `bool → @this`) — entry lifts.
- `item.ToRaw()` — dies, but in the follow-ons slice (its stub is pinned), not here; listed so nobody deletes it early and breaks the tail mid-conversion.
