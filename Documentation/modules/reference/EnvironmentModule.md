# EnvironmentModule

Hint: `[environment]`  
Type: `PLang.Modules.EnvironmentModule.Program`

Information about the environment that the software is running, such as machine name, os user name, process id, settings, debug modes (`set debug mode = true`, `set debug mode = false`, `remove debug mode`, `is in debug mode`; debug mode is a setting of the app, not a %variable%), set culture, open file in default app, npm install, disks (size and free space), memory (total, used, free), running tasks (dont wait goals, scheduled runs, webservers), running engines (one per request or dont wait run, call stack, variable sizes), runtime stats (gc heap, process memory, call stack depth, caches), drain app (gracefully stops the running process itself: stops accepting requests, waits for requests in flight, closes long lived connections, then exits; `drain app`, `drain app, timeout 45 seconds`, `drain app, do not exit`; this is not a call to another app), run setup

## Methods

### CanSeeErrorDetails

```
CanSeeErrorDetails() : Boolean
```


### DrainApp

```
DrainApp(Int32 timeoutInSeconds = 0, Boolean exitWhenDone = True) : PLang.Runtime.DrainResult
```

Gracefully drains the running app so a new process (started with --standby, listening on the same port with `reuse port`) can take over without dropped requests. Stops the webservers from accepting new connections (requests in flight finish), starts no new scheduled runs, waits until the running requests and goals have finished or the timeout comes, then closes what is still open (long lived http responses, iroh endpoints and connections) so the clients connect again. timeoutInSeconds: 0 means the setting DrainTimeoutSeconds, default 30. exitWhenDone (default true): the process ends with exit code 0 when the drain is done, false leaves it running without listeners. Returns phase timings in ms (stop_accepting_ms, wait_ms, going_away_ms, total_ms), engines_waited_for, timed_out, live_connections_closed, iroh_closed, webservers_stopped, exited. A second call while draining is an error. e.g. `drain app, write to %result%`


Examples:

- drain app => timeoutInSeconds=0, exitWhenDone=true
- drain app, timeout 45 seconds => timeoutInSeconds=45, exitWhenDone=true
- drain app, then exit => exitWhenDone=true
- drain app, do not exit, write to %result% => exitWhenDone=false

### EndApp

```
EndApp(Int32 exitCode = 0) : object
```

Stops the app. exitCode is the process exit code the step states, e.g. `end app, exit code 1`; 0 when the step gives none


### GetAppName

```
GetAppName() : String
```


### GetCallStackDepth

```
GetCallStackDepth() : Int32
```

The call stack depth (number of goal frames) of the current context, an int. Cheap, for a watcher to poll. e.g. `get call stack depth, write to %depth%`


Examples:

- get call stack depth, write to %depth% =>
- how deep is the call stack, write to %depth% =>

### GetCurrentCulture

```
GetCurrentCulture() : Globalization.CultureInfo
```


### GetDisks

```
GetDisks() : List<DiskInfo>
```

The disks (mounted file systems) of this machine with size, used, free and percent used, in MB. Each row: Filesystem, Mount, SizeMb, UsedMb, AvailableMb, UsedPercent. Linux. e.g. `get disks, write to %disks%`


Examples:

- get disks, write to %disks% =>
- how much disk space is there, write to %disks% =>

### GetEnvironmentVariable

```
GetEnvironmentVariable(String key) : String
```


### GetMachineName

```
GetMachineName() : String
```


### GetMemory

```
GetMemory() : PLang.Modules.HostModule.MemoryInfo
```

The memory of this machine in MB: TotalMb, UsedMb, FreeMb, AvailableMb, SwapTotalMb, SwapUsedMb. Linux. e.g. `get memory, write to %memory%`


Examples:

- get memory, write to %memory% =>
- how much memory is free, write to %memory% =>

### GetMemoryInfo

```
GetMemoryInfo(Boolean forceGarbageCollection = False) : PLang.Modules.EnvironmentModule.Program+MemoryInfo
```

Get current process memory usage details


### GetMocks

```
GetMocks() : PLang.Modules.MockModule.Program+MockData
```

Get active mocks on engine, used for testing


### GetOSDescription

```
GetOSDescription() : String
```


### GetProcessId

```
GetProcessId() : Int32
```


### GetRunningEngines

```
GetRunningEngines(Boolean withSizes = False, Int32 top = 20, Boolean includeRootEngine = False) : PLang.Modules.EnvironmentModule.RunningEngine
```

The engines running in the app: one is rented for each web request and each `dont wait` or scheduled goal run, and returned when it ends. Each row: id, name, is_root, state (running/idle), goal and step where it is now (goal file:line), step_text, called_from (the step that rented it), started_utc, running_seconds, thread_id, memory_stack_variables, call_stack (depth, executed_steps, running_ms, frames: goal, step, running_ms, executed_steps). request (method, path; only an engine serving a web request, else null), tasks (number of dont wait/scheduled goals this engine started that still run). Longest running first, top 20 (top=0 for all). withSizes=true also serializes each engine's variables to count their size: serialize_bytes, capped, truncated, largest_variables (name, bytes), size_errors, sorted largest first; it takes up to 200 ms, set it only when the user asks for sizes. includeRootEngine=true adds the root engine. e.g. `get running engines, write to %engines%`


Examples:

- get running engines, write to %engines% => withSizes=false, top=20
- get running engines, with sizes, write to %engines% => withSizes=true
- get top 5 running engines, include root engine, write to %engines% => top=5, includeRootEngine=true

### GetRunningTasks

```
GetRunningTasks() : PLang.Modules.EnvironmentModule.RunningTask
```

What keeps the app running now: goals called with `dont wait`, scheduled runs in progress, webservers, the scheduler and other listeners. Each row: Kind (WaitForExecution for a dont wait call, Schedule for a scheduled run, Webserver, Scheduler, ...), Name, Goal (path of the goal that runs, empty for a listener), CalledFrom (path of the goal that called it), StartedUtc, RunningSeconds. e.g. `get running tasks, write to %tasks%`


Examples:

- get running tasks, write to %tasks% =>
- what is running in the app, write to %running% =>

### GetRuntimeStats

```
GetRuntimeStats(Boolean forceGc = False) : PLang.Modules.EnvironmentModule.RuntimeStats
```

Diagnostics of the running app's memory, to tell whether growth is objects PLang holds or native/.NET memory. One object with three groups. gc: heap_mb, heap_after_collect_mb (only when forceGc, else null), gen0_collections, gen1_collections, gen2_collections, total_allocated_mb, heap_size_mb, fragmented_mb, committed_mb, large_object_heap_mb, pinned_object_heap_mb, finalization_pending_count, pinned_objects_count. process: rss_mb, rss_anon_mb, rss_file_mb, swap_mb, private_mb, virtual_mb, not_gc_mb (rss minus gc committed), threads, open_files, handle_count, thread_pool_threads, thread_pool_pending_work_items, timers, loaded_assemblies. plang: call_stack_depth, call_stack_max_depth, executed_steps_on_call_stack, memory_stack_variables, keep_alive_entries, running_tasks (count by kind), engine_pool (total, rented, free), engines_running, oldest_running_engine_seconds, largest_engine (id, name, serialize_bytes, measured_utc; only from the last `get running engines, with sizes`, null when not measured), context_items, context_shared_items, context_event_bindings, regex_cache_limit, caches (entry count of each runtime cache by name), standby (started with --standby), scheduler_enabled (false when started with --no-scheduler or --standby, until `enable scheduler`), draining (true once `drain app` has started). forceGc=true runs a full blocking garbage collection first, which stops the whole app briefly; default false, set it only when the user asks to force/collect garbage. e.g. `get runtime stats, write to %stats%`


Examples:

- get runtime stats, write to %stats% => forceGc=false
- get runtime stats, force garbage collection, write to %stats% => forceGc=true

### GetSettings

```
GetSettings() : PLang.Models.Setting
```


### GetUserName

```
GetUserName() : String
```


### HideErrorDetails

```
HideErrorDetails() : object
```


### InstallNpm

```
InstallNpm(String packageName) : object
```


### IsInCSharpDebugMode

```
IsInCSharpDebugMode() : Boolean
```


### IsInDebugMode

```
IsInDebugMode() : Boolean
```


### KeepAlive

```
KeepAlive(String message = App KeepAlive) : object
```


### OpenFileInDefaultApp

```
OpenFileInDefaultApp(String filePath) : object
```


### RemoveDebugMode

```
RemoveDebugMode() : object
```

Turns debug mode off for the app. For a step that sets debug mode to false or off, or removes it. Debug mode is not a %variable%, so this is never VariableModule.SetBoolVariable


Examples:

- set debug mode = false =>
- set debug mode off =>
- remove debug mode =>

### RunSetup

```
RunSetup() : object
```

Runs the setup goals now. They run at every start of the app, except when it was started with --standby. e.g. `run setup`


Examples:

- run setup =>

### SetCultureLanguageCode

```
SetCultureLanguageCode(String code = en-US) : object
```

Make sure to convert user code to valid BCP 47 code, language-country


### SetCultureUILanguageCode

```
SetCultureUILanguageCode(String code = en-US) : object
```

Make sure to convert user code to valid BCP 47 code, language-country


### SetDebugMode

```
SetDebugMode() : object
```

Turns debug mode on for the app: errors show their details. For a step that sets debug mode to true or on. Debug mode is not a %variable%, so this is never VariableModule.SetBoolVariable


Examples:

- set debug mode = true =>
- set debug mode on =>

### SetEnvironment

```
SetEnvironment(String name) : object
```


### SetSettingsDbPath

```
SetSettingsDbPath(String path) : object
```


### ShowErrorDetails

```
ShowErrorDetails() : object
```


