# Contributor guide

For a person or a bot who changes the PLang runtime and has never seen it. Each page answers one question: how is it built, what rules must I keep, and why is it this way?

Every statement is run, or read at a `file:line`, and recorded in [verify.md](verify.md). Where an older document and the code disagree, these pages follow the code.

1. [The shape of the code](shape-of-the-code.md): where things live and what they are called.
2. [How a step runs](how-a-step-runs.md): from a goal to the C# that does the work.
3. [The type system](the-type-system.md): types, kinds, items, and a value that writes itself.
4. [The rules](rules.md): the rules you must keep, each with its gate or its test.
5. [The locked core](the-locked-core.md): what you may not change without a yes, and how you ask.
6. [Build and test](build-and-test.md): the commands, and the stale-binary trap.
7. [Add an action](add-an-action.md): the smallest change, run end to end, and what it trips.
8. [How a bot works here](how-a-bot-works.md): roles, branches, `.bot/`, proposals.

The reasons behind the rules are in [decisions](../decisions/start.md). A reason that is not written anywhere in the repository is marked "reason not recorded".

If you only read one page, read [the rules](rules.md) and [the locked core](the-locked-core.md).
