# codeanalyzer summary (bugs)

**Version:** v2

**What this is:** The bugs branch (runtime2 3a29633f8 → 4b23498ee): a test's llm answers from a file, consent fixes, the readers, the goal channel and named-actor call through `actor.context.Run`, the command line, `path.Holds`, the Windows window over a shared `Shown`, `module.Remove` answering data, `app.Wiring`, a channel born of its actor's channels.

**What was done:** v1 FAIL: `Window.Apply`'s text-frame rects ran `Shown.Put` in `Parallel.For`, so a refused rectangle reached the decode thread as an `AggregateException` (probed); the view swallowed BadFrame; six smell findings. v2 PASS: `Shown` has one door for a frame's rectangles in both forms, the view reports, F3–F8 closed or ruled. Root birth +48 KB bisected: all of it is stage 1's `answer`/`exchange` types and `IAnswer`. `Tools/Bench` moved to `Wiring`.

**Next:** tester and security. Not reviewed: the Windows run, the json writer's setting.

**Code example:**
```csharp
return Put(parts, max, m[0] == 7 ? m : null);   // Shown.Apply(byte[]); Apply(string) ends the same way: one door, every part bounded first
```
