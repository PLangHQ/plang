# coder: kind-is-type v2

Traces to: decision 774 in `.bot/kind-is-type/architect/summary.md`; the plan is `test/plan/kind-is-type/start.md` (rewritten 2026-10-15), its index `start.goal`.

## What changed

Ingi stopped stage 1 as it was. No type is renamed: a type's name stays its `[PlangType]` word, else its namespace. A kind becomes a type under its parent, `kind<T> : kind : type`, with its name unique only there, and found only through its parent. Your uncommitted B1 goes, except the nested-class-through-its-owner fix, which stage 2 needs. Commit A (`error`, 7f20d50c1) and 767 stay. 773's move of table, code and clr under `app/type/item/` stays (their words keep their names). 761 (module folders) is postponed.

## In plang

```plang
kind-is-type v2, the coder's part (decision 774)
Start
- set your tree back to origin/kind-is-type, keeping only the nested-class fix for stage 2 (stash or branch the rest; nothing of B1 is committed)
- stage 1, the probe: number's kinds as kind<number> (kind<T> : kind : type); a kind answers what is its own and passes every other type question to number, written once in kind<T>
- send the probe's report to the architect before stage 2: each clash with item's verbs (Set, Output, Read, Convert, Clr, IsSequence, Names) and how it was settled; what decimal answers itself, what it passes up; that number never asks its kind back; DecimalIsANumber green
- then stage 2 (settings are settings), stage 3 (every family), 4, 5, 6 as start.md, the shape of each to the architect before it's built
LIMITS: no type renamed; nothing stored or written in a .pr changes shape except where a stage says; locked-core surface changes re-pinned with 774 named
DONE WHEN: the probe's report is with the architect
REPLY: Done/HASH/CHECK/OPEN, text first
WHY: the pain was renaming every type; a kind needs a name unique only under its parent

/ the shape, a sketch (you own the code)
/ namespace app.type.kind;
/ public abstract class @this : global::app.type.@this { }                          → a type that lives under another
/ public abstract class @this<T> : @this where T : global::app.type.item.@this { }  → T: the type it sits under; passes up what it doesn't answer
/ decimal: public sealed class @this : global::app.type.kind.@this<global::app.type.item.number.@this> { … }
```
