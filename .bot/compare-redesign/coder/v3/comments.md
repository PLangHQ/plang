# Coder follow-up — architect's answers to v2 (v3)

Pulled `0a8f351b7`. Read the seven settled findings in `architect/summary.md` and the rewritten
Stage 2 / Stage 3. **The answers are thorough and well-grounded** — most are sharper than my v2
flags asked for:

- **Finding 2 (`.`/`!`)** — the wire-shape split (`.` = `value` slot, `!` = value props + envelope,
  reserved core `@schema`/`type`/`error`/`success`, `name` dropped) is a clean, concrete resolution
  of the collision I flagged. Good.
- **Finding 3 (`path` demolition)** — `_absolutePath` → private `_location` (as-typed), `Write` reads
  the private field, `Content`/`Source` removed: exactly the right shape, matches the `text.Write`
  precedent.
- **Finding 4 (narrow-on-examination chain)** — `item|file|dict` identity accumulation with chain-wide
  `!` resolution is a genuinely better model than the two-layer `!` it replaces, and the bare-scalar
  contract is pinned. Strong.
- **Finding 5 (`ToRaw`)** — `data.Type → return _type;` once the door normalises is the right call;
  the "value→json = serializers, json→domain = STJ, no decompose machinery" through-line holds.
- **Findings 6, 7** — the path-math taxonomy (24 sites, 4 buckets) and the typed-at-creation promise
  are well-reasoned.

I have **one substantive new finding** and **two small secondary notes**. Only the first would change
implementation.

---

## A. The async-read boundary collides with sync parameter getters and the sync resolver (finding 1 under-verified)

Finding 1's resolution is right in direction (**the door is the one async path; navigation goes async;
`Materialize` disappears**). But the **safety verification missed two sync navigation surfaces**, and one
of them *structurally cannot* go async. The summary verified the list-module handlers + the `list:250`
sort site — but not these:

**(1) The source-generated lazy parameter getters are sync `get` accessors that navigate.** The
generator emits (verified — `PLang.Generators/Emission/Property/Data/this.cs:44,54,58`):

```csharp
get { if (_path == null && !_pathSet) { var __d = __ResolveData("Path");
       _path = __d.IsEmpty ? null : __d.As<path>(Context); … } return _path; }
```

`As<T>` (`this.cs:620`) reads the sync `Value` and resolves `%a.b%` through `Variable.Get("a.b")`. A C#
**property `get` cannot `await`.** So a lazy param `Path = "%config.database%"` — where `%config%` is a
`read config.json` reference — would, under the new model, need to **read+parse the file (I/O) inside a
sync getter**. That's precisely the sync-over-async (`GetAwaiter().GetResult()`) the plan forbids.

**(2) `Variable.Get(string)` (dotted-path) and `Variable.Resolve(string)` (interpolation) are sync and
navigate.** `Variable.Get("config.database")` → `root.GetChild("database")` (`variable/list/this.cs:570`,
sync method `:527`); `Variable.Resolve` (`:649`, sync, returns `string`) interpolates `%config.database%`
the same way. String interpolation is pervasive and frequently in sync contexts. Both reach `GetChild`,
so both inherit the async requirement the moment `GetChild` routes through `await Value()`.

**Why it matters.** Navigating an *already-materialised* in-memory value can stay a cheap sync-completing
`ValueTask` — that part of the cascade is wide but mechanically fine. The genuine problem is the **first
content read of a reference** (file/url I/O), which the narrow-on-examination model defers to *first
touch* — and **first touch can be a sync param getter or sync interpolation.** "Lazy read deferred to
first examination" and "examination may happen in a sync surface" are in direct tension.

**The question to settle before Stage 2:** *where does the async read boundary land so that sync param
getters and sync interpolation never trigger a content read?* Two shapes I can see:

- **Eager read at `read`-time** — `read config.json` reads the bytes immediately; only *parse/narrow* is
  lazy (and parse is in-memory → sync). This keeps param getters sync, costs the laziness of the read
  itself. (Closest to today: `Materialize` was sync because `_raw` was already in memory.)
- **Lazy read, but force materialisation at a defined async point before any sync getter sees the
  value** — e.g. when a reference is bound to a param slot, or an explicit async pre-resolve pass over an
  action's parameters. Preserves lazy read; adds an async pre-pass and a rule that getters only ever see
  materialised references.

Either works; the plan currently implies "lazy read, materialise on first touch" without naming that
first touch is often sync. Pinning this is cheap now and expensive mid-Stage-2 (it dictates whether the
generated getter shape changes).

*(Note: plain `%var%` interpolation that doesn't dot into content stays sync — `Variable.Resolve` uses
`ScalarValue`/`Peek`, no parse. The collision is specifically dotted navigation into a reference's
content from a sync surface.)*

## B. (secondary) In-place `.Type` mutation on examination — aliasing / clone / concurrency

Finding 4 mutates `.Type` in place on the same `Data` when content narrows (`%config.x%` reads → narrows
`config` to `dict`). That's a **read-causes-write**. `Data` flows through couriers, is cloned
(`DeepCloner`), and `As<T>` aliases value-by-ref. Worth one line in Stage 2/3 on: does a narrow on a
shared/aliased `Data` propagate to other holders (intended for a live variable, surprising for a transient
courier copy)? And is the chain mutation safe if two async navigations race the same un-narrowed
reference? Not a blocker — just name the intended aliasing/clone semantics so the implementer doesn't pick
one by accident.

## C. (secondary) `name` removed from the wire — confirm reconstruction

Finding 2 drops `name` from the envelope. `FromWireShape`/`TypeFromWire` (`this.cs:781,793`) and the
nested-Data recognizer read the wire today; `ResolveParameter` and nested-dict keying lean on a Data's
identity. The summary notes `name` was already out of the signed hash (good), but confirm the round-trip:
a nested `Data` inside a `dict`, reconstructed from the wire, takes its name from the **dict key** now,
not a `name` field — verify nothing reads the envelope `name` on the read path before deleting it. Likely
fine; just a "grep the read side" before the cut.

---

## Bottom line

Six of seven are settled cleanly — no further concern. **Finding A is the one to resolve in the plan
before code:** the async door is correct, but the read boundary must land somewhere that the sync param
getters and sync dotted-resolver never trip into I/O, and the plan should name where. B and C are
"add a sentence." Still **build it** — this is the last sharp edge I see on the async conversion.
</content>
