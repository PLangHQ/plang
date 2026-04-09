# Plan v4 — Fix Codeanalyzer Findings

Addressing findings from codeanalyzer v1 analysis.

## Medium Findings

### 1. PrPath backslash fallback on Linux (Goal/this.cs:84)
**Problem:** `"\\"`  fallback when Path has no separators → invalid paths on Linux.
**Fix:** Change `"\\"` to `"/"` — PLang paths use forward slash internally.

### 2. GoalCall parameter pollution (GoalCall.cs:94-97)
**Problem:** Parameters injected into context.Variables BEFORE .pr load. If load fails, parameters stay on caller's scope.
**Fix:** Move parameter injection after successful load + goal match.

### 3. Newtonsoft in CommandLineParser (CommandLineParser.cs)
**Problem:** Active `JToken.Parse` dependency. Should use System.Text.Json per CLAUDE.md.
**Fix:** Replace `JToken.Parse` with `JsonDocument.Parse` / `JsonElement.ValueKind` switch. Remove Newtonsoft using directives.

## Minor Findings

### 4. Bare catch(Exception) in Step.RunAsync (Step/this.cs:133)
**Fix:** Add negative filter: `catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))`

### 5. Bare catches across App/ (5 locations)
- `CommandLineParser.cs:101` — narrow to `catch (JsonException)`  (will be `System.Text.Json.JsonException` after migration)
- `CommandLineParser.cs:144` — same
- `DefaultGrepProvider.cs:59` — narrow to `catch (RegexMatchTimeoutException)`
- `TypeMapping.cs:337` — investigate, narrow
- `Modules/this.cs:272` — narrow to `catch (MissingMethodException)`

## Files
- `PLang/App/Goals/Goal/this.cs` — one char fix
- `PLang/App/Goals/Goal/GoalCall.cs` — move parameter injection
- `PLang/App/Utils/CommandLineParser.cs` — Newtonsoft → System.Text.Json
- `PLang/App/Goals/Goal/Steps/Step/this.cs` — bare catch filter
- `PLang/App/FileSystem/Default/DefaultGrepProvider.cs` — bare catch
- `PLang/App/Engine/Utility/TypeMapping.cs` or equivalent — bare catch
- `PLang/App/Modules/this.cs` — bare catch
