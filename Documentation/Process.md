# Running a program beside the app

`start process` starts a program that keeps running while the app runs, e.g. a bot that reads json lines on its
standard input and writes json lines back. The step returns the process, so the step writes it to a variable.

```plang
Start
- start process "claude", parameters ["-p", "--input-format", "stream-json", "--output-format", "stream-json"],
    on output line call OnLine, on error line call OnErrLine, on exit call OnExit,
    write to %bot%
- set cache "bot1" = %bot%, absolute expiration in 10 years

Send
- get cache "bot1", write to %bot%
- write out %json% to %bot%

Stop
- get cache "bot1", write to %bot%
- stop process %bot%

OnLine
/ %line% is one line of standard output, %process% the process
- append "%line%\n" to file "/bot.log"

OnExit
- write out "bot ended with %exitCode%"
```

- **Each line, in order.** OnLine is called once for every line the program writes to standard output, one at a time
  and in the order they were written: the next line waits until the goal for the last one has ended. Standard error
  goes to OnErrLine the same way. The goals get `%line%` and `%process%`.
- **OnExit** is called once, after both streams have ended, with `%exitCode%` (137 after `stop process`, -1 when
  reading the output failed; `%process.Error%` then says why).
- **`write out %x% to %bot%`** writes one line to the program's standard input and flushes it. Text is written as it
  is, any other value as one line of json. Writing to a process that has ended is an error.
- **`stop process %bot%`** ends the program and every process it started, then waits (at most 5 s) for OnExit.
  Starting it again is `stop process` and then `start process`.
- **Other goals reach it through the cache.** A route or an iroh handler has its own variables; `set cache` keeps the
  process itself (not a copy) and `get cache` in the other goal gives the same running process.
- **It keeps the app alive** (`Process` in "Keeping app alive, reasons") until it ends. `drain app` and the end of the
  app stop it.
- The goals run as the app's own (`%Identity%` is `%MyIdentity%`), not as the request that started the process.

`terminal ...` (RunTerminal) is still the step for a command whose output the step waits for.
