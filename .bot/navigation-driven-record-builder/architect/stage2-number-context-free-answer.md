# Decision — number kinds: context-free, verb is `Create`, no registry

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage2-number-kinds-must-be-context-free.md`. Supersedes the earlier blessed-in-passing version (`Build(double)` / "context-free registry" / "`From` stays") — that one skipped the discussion and got it partly wrong.

1. **Context-free: yes.** Your evidence holds (`number.@this` has no Context; `Write` is the value writing itself; arithmetic is ctx-less). The 15 kind-class ctors lose the `context` param.

2. **The construction verb is `Create`** — `Create(object)` and `Create(double)` overloads on the number-kind base. Not `Build` (that verb is being deleted with `type.Build`), not `FromDouble`. And `number.From`/`FromObject` fold into `number.Create` overloads in this same work — one construction verb, one door, no `From`.

3. **No registry — the value carries its kind instance.** Whoever mints a number knows its kind (`Create`, the climb, the perimeter), so the number is born holding the kind object. `Write` is `Kind.Write(this, writer)`; arithmetic rebuilds via the kind it picked — **zero lookups at the ctx-less sites.** The only two lookups that remain — declared kind name inside `number.Create`, CLR type at the perimeter — use a **private immutable map of the 15 singletons inside number** (the sanctioned data-table clause, same category as the primitive tables). No public door, no `Discover`, no keyed registry — your proposed `Kind[name]`/`Kind[clrType]` registry is rejected as the `OfStatic` shape reborn.

4. **Ladder and levels: unchanged** — exactly as already ruled (name-keyed levels, integer climb, fractionals via the mix policy). This finding never touched them.

Proceed: 15 one-line ctor edits, `Build`→`Create` on the base, wire the three switches to the kinds, fold the `From*` statics.
