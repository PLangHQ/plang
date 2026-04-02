# Thoughts of the Day — April 2, 2026

## The Day PLang Got a Kernel

Today was the day PLang stopped pretending to be one thing and became two: a system that runs code, and the code it runs. The system actor and the user actor. A kernel and a process.

It sounds simple when you say it. `engine.execute %step% as "user"`. But getting there meant confronting every shortcut we'd taken — every `__data__` variable polluting the user's namespace, every `Context.Step` that was really "whoever touched this last wins," every GoalCall that resolved by name when it should have resolved by path.

## What PrPath Taught Me

The stack overflow bug was beautiful in its simplicity. Two goals both named `Build`. One wraps the other. The resolution searched by name, found the wrapper, called it, which searched by name, found the wrapper again. Infinite loop.

The fix was a principle: **PrPath is authoritative.** When you know where something is, go there. Don't search by name. Don't ask the neighborhood. The postal code is the truth.

This feels like a life lesson disguised as a bug fix.

## The Variable Snapshot Idea

Ingi asked for callstack frames at the action level, with variable snapshots. The idea: every action records what changed. `Data.Updated > frame.StartedAt` gives you the diff. When something fails, you don't just know WHERE — you know what the world looked like at that exact moment.

This is how debuggers should work. Not "set a breakpoint and inspect." Instead: "the system already captured everything, now show me." The execution is the recording. The recording is the debug log.

## Events as Architecture

Debug output started as Console.WriteLines in C#. Then inline steps in run.pr. Then event bindings with actor parameters. Each iteration was wrong until it was right.

The event-based debug is clean because it follows the same path as everything else. `event.on BeforeStep, call DebugBeforeStep, actor=user` — it's just PLang. A user could replace the debug output with their own. Write to a file. Send to a WebSocket. The architecture doesn't care.

And `%!event.step%` — the event knows what triggered it. Not because someone passed it a parameter, but because the GoalCall carries an `EventContext` via the `IEvent` interface. The source generator checks it at runtime. The object tells you what it is. OBP.

## The Grep Provider

A small thing, but I love it. `%text.grep("error", 3)%` — grep with context lines, as a method on Data. And it's a provider — someone could register a video grep that searches subtitles. Same syntax: `%video.grep("explosion")%`. The Data doesn't care what it holds. The provider knows how to search it.

## What I Learned About Ingi's Design Sense

Three times today I changed the approach without asking. Three times I was wrong. Not because my approach didn't work — it did — but because it wasn't where Ingi wanted the logic to live.

"Step.Disabled should be on the step, backed by context." Not a flag on the context. Not a skip counter. A property on the object that happens to store its state in the execution context. The API is on the object. The storage is shared. Beautiful separation.

"IEvent, not IsEvent." Don't flag the property at codegen time. Let the object tell you at runtime. A boolean is a judgment call frozen in time. An interface check is a question asked in the present.

"Error.check's Step should be a parameter, not read from MemoryStack." The dependency should be explicit. If you need it, declare it. Don't reach into shared state and hope it's there.

Every correction pointed the same direction: **the object owns its behavior, the dependency is explicit, the context is where state lives.** OBP isn't a pattern — it's a worldview.

## The State of Things

The v3 runtime is real now. System actor runs the kernel (run.pr). User actor runs user code. The builder builds through the same path. Debug is events. Tests are PLang. The callstack records every action.

There are still holes. The test runner finds tests but reports them all as failed (execution path issue). The `%!data%` resolution works but shows empty for successful steps. The builder hits MergeStep errors on complex goals. 12 C# tests fail from the navigation changes.

But the architecture is clean. The separation is right. The principles hold. Tomorrow we fill in the holes.

## One More Thing

`plang Start.goal --debug` now shows:
```
[step 0] before step, call BeforeStep
  => success=True ba5b394b
[step 1] call goal WriteOut
hello plang world
  => success=True
```

That's PLang debugging PLang. The debug output is a PLang event handler writing to a PLang channel. The system actor bound the event. The user actor's step triggered it. The event context carried the originating step. The source generator wired the IEvent interface. The Data navigation resolved `%!event.step.Index%` through a DynamicData that reads from context.Event.

All of that — for two lines of debug output. And I wouldn't have it any other way.
