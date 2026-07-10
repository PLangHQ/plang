# Decision — `Load()` dies; materialize-and-emit lives in ONE door: `data.Output`

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage2-write-output-collapse-and-load-via-value.md`. Good trace — the sync-`Write`-callers finding and the raw-passthrough collision are both real, and they shaped the ruling.

## 1. Scope — contained now; the `Write` collapse is its own pass

- **Now:** `image` + `directory` gain loading `Value(data)` overrides (parallel to `file`/`url`) — `.Value()` becomes the uniform materialize door for all four reference fundamentals. `data.Load()` + `data/this.Load.cs` + the two channel call sites die.
- **NOT now:** deleting sync `Write(IWriter)`. It's a value-emission primitive with ~15 direct callers (the json writer's own `case item v: v.Write(this)` dispatch, signature's field writes, three type serializers) — removing it is an `IWriter` contract change. Own pass, logged in `todos.md` (2026-07-10).

## 2. The one door — container loops NEVER hand-roll materialization

The coder's sketch (`await element.Value(); emit` in each container loop) would force the `RawUntouched` guard and the Store-mode rule to be re-applied identically in every loop — the divergence trap. Instead:

```
container Output loop:   await element.Output(writer, mode, ctx);     // ALL a loop does

inside data.Output — the ONE materialize-and-emit door:
    RawUntouched  →  emit _raw VERBATIM              // the Load() short-circuit, moved INTACT
    Store mode    →  refs stay verbatim (existing);  // no %var% render on .pr writes;
                     load only what the Store form actually emits — coder VERIFIES which
                     fundamentals' Store shape needs bytes at all (an image's Store form is
                     likely its source/path, not bytes)
    Out mode      →  await Value()                   // renders %var%, loads image/file bytes
                     → the item writes (sync Write reads now-loaded bytes)
```

**The raw-passthrough dream is protected by one line in one place**: `read file.json → %json% → write out %json%` emits the original bytes because `data.Output`'s first branch never opens the value. No container loop can forget the guard, because no container loop ever sees the question.

## 3. Answers to your three decisions

1. **Scope:** as above — your lean confirmed.
2. **`RawUntouched`:** inside `data.Output`, first branch, moved intact from `Load()`. Nowhere else.
3. **Store view:** mode-awareness also lives inside `data.Output` — Store preserves refs verbatim (existing behavior), Out renders. The "container-loop `.Value()` must stay Out-only" problem dissolves because loops don't call `.Value()` at all.

## Acceptance

- **Verbatim round-trip:** read a `.json` file, `write out %json%` — byte-identical to the source (the passthrough pin).
- **Store-write with a ref:** a goal containing a `%var%` param Store-writes with the ref verbatim, unrendered.
- **Leaf loading:** an image element rendered via Out loads its bytes exactly once, at the leaf, in the single async pass (no `Load()` pre-pass anywhere — grep zero).
- Types/Data suites hold baseline through the change (as your json-channel landing did).
