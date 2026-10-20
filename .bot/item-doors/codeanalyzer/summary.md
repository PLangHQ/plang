# codeanalyzer summary

**Version:** v1

**What this is:** The item-doors OBP audit (decision 160). Read only. Output: `audit.md`, with a plain summary first, then a table of verdicts per stage, then findings with why-chains.

**What was done:** I audited the production diff 1adfb9555..b91e019b5. Keep the branch. Fix at the root: the pair registry with PLNG006 (stage 2/3), the wire's decode and `Owns` fork (decision 158's c2 is not built), stage 1's fallback birth and six undone demolition items, stage 5 (the C# lowering fork, literal-key arms, "add an unknown key" not delivered), `variable.set` renaming at the consumer, and the hand-listed `app.Output`. Keep: 1b, the one door, the record door, the token reader, the nodes.

**Next:** Ingi decides per stage. The pair fix needs an `IWriter` addition, which is a locked-core change.

**Code example:**
```csharp
// today: 103 sites, each through the registry, enforced by an analyzer
await writer.Kind.pair.Output(member, writer, mode, context);
// root-level: the value writes itself; the writer's default-method primitives vary the form
await member.Output(writer, mode, context);
```
