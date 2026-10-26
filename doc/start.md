# PLang

PLang is a programming language you write in plain sentences. A program is a folder of `.goal` files. Each file holds goals, and each goal is a list of steps, one sentence per step.

```plang
Start
- read person.json, write to %person%
- write out %person.name%
```

`Start` is the goal. It is where a program begins. Each line that starts with `- ` is a step. `%person%` is a variable.

## Two phases

PLang works in two phases, and they are separate commands.

- **Build** (`plang build`). Each step is sent to a language model. It picks the action the sentence means and fills in the action's parameters. The result is written beside your goal as a `.pr` file in a `.build/` folder. Building needs a model service and a key. See [install](install.md#the-key-a-build-needs).
- **Run** (`plang`). The runtime reads the `.pr` file and executes it. No model is involved, so running needs no key and no network.

A run never builds. A goal that has not been built answers `GoalNotBuilt (404)` and tells you to run `plang build`.

## Where to go next

1. [Install](install.md) gets a `plang` command on your machine.
2. [Your first goal](first-goal.md) writes a goal that reads a JSON file and prints one field, builds it and runs it.
3. [When a step fails](when-a-step-fails.md) says what a failed build looks like and what to change.
4. To write programs: [goals and steps](goals-and-steps.md), [variables](variables.md), [conditions](conditions.md), [loops](loops.md), [calling goals](calling-goals.md), [errors](errors.md), [events](events.md), and [how a step becomes an action](how-a-step-becomes-an-action.md).
5. Testing and configuring: [testing your goals](testing-your-goals.md), [settings](settings.md), and the map of [modules](modules.md).
6. To decide whether to trust PLang: [trust](trust.md).
7. More things a program does: [goals in other folders](goals-in-other-folders.md), [reading a file line by line](reading-a-file-line-by-line.md), [HTTP](http.md), [timeouts](timeouts.md), [caching](caching.md), and [databases](databases.md) (there is no database module).

[verify.md](verify.md) lists how each claim in these pages was checked, and which ones could not be.
