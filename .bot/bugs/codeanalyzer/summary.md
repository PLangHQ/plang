# codeanalyzer summary (bugs)

**Version:** v1

**What this is:** The bugs branch (runtime2 3a29633f8 → ad0caedd7): a test's llm answers from a file, consent fixes (N1/N3/N4/N5/N8/S1), the readers (N10/N11), the goal channel and named-actor call through `actor.context.Run`, the command line (N16/N14), `path.Holds`, the Windows window over a shared `Shown`, `module.Remove` answering data, `app.Wiring`, a channel born of its actor's channels.

**What was done:** Read the touched production files (whole where more than a line), ran the bench parts for the diff, `plang --test` with the llm goals offline (300 pass, 1 baseline fail), and three announced mutations (N15, the twin race, S1: each red). A throwaway probe showed `Window.Apply` lets an `AggregateException` out of `Parallel.For` (F1). `Tools/Bench/Program.cs` moved to `Wiring`.

**Verdict:** FAIL — F1 (blocker, the decode thread), F2 (view swallows BadFrame), F3–F8 smell findings. Coder next.

**Next:** the coder fixes F1/F2 (and the smells it agrees with); I re-read the window path and the new test red-without-change. The Windows run and the json writer's setting are not in this review.

**Code example:**
```csharp
Parallel.For(0, parts.Length, i => changed[i] = screen.Put(…, max));   // Window.cs:344 — Put throws AppException; Parallel wraps it
```
