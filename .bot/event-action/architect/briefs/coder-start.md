event-action: place the plan, push back, then stage 1 (decisions 1–18)
Start
- git fetch, then git switch event-action (cut from runtime2 6cfe782d7)
- read .bot/event-action/architect/plan/start.md, plan/start.goal and plan/expected.md, then the log .bot/event-action/architect/summary.md (decisions 3–18 hold Ingi's words), then CLAUDE.md
- record the baseline before any change: C# (./dev.sh test) and plang (./dev.sh build, then cd test && ../PlangConsole/bin/Debug/net10.0/plang --test) into .bot/event-action/coder/baseline-failures.txt
- push back on anything in the plan before code: reply with what you'd change and why
- place the plan at test/plan/event-action/ (start.md, start.goal); write each test at the path start.goal names, each red without its stage
- bring me these shapes before their code: the door context.Actor.@event[item]; how %!x.on% reaches the running actor's on; on.unbind's parameter; stage 8's way for a goal to know the app it belongs to
- then build stage 1
LIMITS: no addition to a locked surface beyond decisions 3–18 without asking me first; never hand-edit a .pr (plang-builder rebuilds); the variable part of stage 2 waits for plang-tester's .bot/event-action/tester/variable-events.md; return early (if (!x) return …; never a body wrapped in an if)
DONE WHEN: stage 1's tests and the re-pinned goldens (surface, PickListTests, ModulePageTests) are green, with no new red against baseline-failures.txt
REPLY: subject / Done / HASH / CHECK / OPEN
WHY: what runs when an event fires is an event, held by the actor that bound it, in lists each goal, step and action makes on its first run (Ingi: "list should be ready to use at execution time (or constructed for the first time on first execution because everything is lazy)")
