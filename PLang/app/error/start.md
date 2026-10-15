# Errors — how a failure is answered

This is where you start when a step fails and you need to know what the runtime
does with it. It covers the one error type, the kinds that derive from it, the
rule for answering versus throwing, and where a caller finds the errors in play.
It is for the developer working *on* Runtime2 in C#. The PLang user reads the
same errors as `%!error%` in a goal; that surface is covered elsewhere.

```
app.error           PLang/app/error/          namespace app.error
app.error.Error     PLang/app/error/Error.cs  class Error   (the one error type)
app.error.list      PLang/app/error/list/this.cs   the errors observed, in order
```

`app.error` is `type.current.@this<Error, error.list.@this>`
(`PLang/app/this.cs`). It has both a `list` and a `current`, because execution
flows through it: `app.error.current` is the error in play, and `app.error.list`
is every error the asker has seen.

---

## 1. What an error is

`Error` is a plain class that *is* a plang value: it derives from
`app.type.item.@this`, so it renders and navigates the same way every value does.
Its fields are the things a caller needs to act on a failure:

| Member | What it holds |
|---|---|
| `Key` | the program's name for the failure (`"MissingParameter"`, `"GoalNotFound"`). Matched by `%!app.error["Key"]%` |
| `Message` | the human line |
| `Status` | an `app.type.item.status.@this`: the code and reason (`400`, `404`, `500`) |
| `Id`, `CreatedUtc` | assigned at first construction; kept on read-back |
| `list` | the errors that *caused* this one, never null. Empty when nothing did |
| `Context`, `Step`, `Goal`, `CallFrames` | where it happened. `Record` (§4) fills what is missing |
| `Details`, `Data`, `FixSuggestion` | structured context, the typed payload, and the programmer's fix |

The constructors that matter:

```csharp
public Error(string message, string key = "Error", status? status = null)
public Error(string message, Step? step, string key = "Error", status? status = null)
public Error(string message, actor.context.@this context, string key = "Error", status? status = null)
```

A bare `Error` defaults to key `"Error"` and status `400`. Pass a `context` when
the error should know the step, goal, and call chain it came from.

### How it rides on a `Data`

An error reaches a caller as the `Error` of a `Data`. A failed `Data` has
`Success == false` and its `Error` set. The two constructors you will use:

- `data.@this<T>.FromError(error)` — answer a failure, typed as `T`
  (`PLang/app/data/this.cs`).
- `Context.Error(error)` — the same answer from an execution context, untyped
  (`PLang/app/actor/context/this.cs`). `Context.Ok(...)` is the success side.

> `Error` the class and `Data.Error` the property are different things. The
> property is the sidecar a failed answer carries. The class is the failure
> itself, and it is also a value.

---

## 2. The rule: answer, or throw only where no answer can be returned

> **A program's error answers.** A method that can return a result returns
> `FromError` (or `Fail`) carrying the error with its own key.
>
> **It travels as an exception only where no result can be returned** — a
> constructor, a stream callback, a member with no `Data` return. There it is
> thrown as `new AppException(error)`, carrying the `Error` whole.
>
> **Every catch answers `ex.Error`.** `catch (AppException ex)` returns
> `data.@this<T>.FromError(ex.Error)`. The error is not rebuilt on the way.

```csharp
// answers: the failure rides on the Data
return data.@this<path>.FromError(new ValidationError("'path' is required", "MissingParameter", 400));

// travels: a constructor cannot return a Data
if (path is null) throw new AppException(new ValidationError("'path' is required", "MissingParameter", 400));

// every catch answers the Error whole
catch (AppException ex) { return data.@this<path>.FromError(ex.Error); }
```

`AppException` (`PLang/app/error/Exceptions.cs`) is the carrier. It has three
constructors: one that takes an `Error` (the normal case), and two that build an
`Error` from a message, key, and status (default key `"AppError"`, status `500`).

Two details to hold onto:

- **`Error.FromException(ex)` is the one door** from any exception to an `Error`.
  A carried `AppException` comes back as its `Error`, whole. Any other exception
  is a C# fault plang did not raise. It becomes a `ServiceError` with status
  `500`, key `"ServiceError"`, its message naming the exception's C# type, and
  `Exception` set to the original.
- **Never crack a caught `Error` apart.** Do not read its message, key, and
  status to rebuild a new one. Answer it whole, or make a new `Error` that holds
  the caught one in its `list` (the cause).

> A throw that stays a plain .NET exception means plang itself is broken — a
> constructor guard, a boot invariant. A program's failure is never that.

---

## 3. The kinds

Every kind is an `Error`, so every kind answers the same way. The kind tells you
*where* the failure was raised. Its key tells you *what* failed. The table is the
full list in `PLang/app/error/`. Status is what the constructor sets unless
noted.

| Class | Key | Status | Used when |
|---|---|---|---|
| `Error` | `Error` | 400 | the base; any failure not covered below |
| `ServiceError` | `ServiceError` | 400 (500 via `Error.FromException`) | a handler or service failed inside its own code. `FromException` marks an unexpected C# exception |
| `StepError` | `StepError` | 400 | the failure belongs to a step |
| `GoalError` | `GoalError` | 400 | the failure belongs to a goal. `NotFound` → `NotFound` 404. `Cancelled` → `Cancelled` 499 |
| `ActionError` | `ActionError` | 400 | an action failed (a missing parameter, a file not there). `NotFound` → `ActionNotFound` 404 |
| `ProgramError` | `ProgramError` | 400 | legacy, kept for compatibility |
| `AssertionError` | `AssertionFailed` | 400 | a PLang test assertion failed. Carries `Expected`, `Actual`, `UserMessage`. Always keeps its variables |
| `ValidationError` | `ValidationError` | 400 | an input is wrong. `Required` → `MissingParameter`, `InvalidType` → `InvalidType`, `OutOfRange` → `OutOfRange`. Carries `ParameterName` |
| `PermissionDenied` | `PermissionDenied` | 403 | the actor refused consent, or nobody could answer. Carries the `Permission` that was needed. Raised by `path.Authorize` (`PLang/app/type/item/path/this.Authorize.cs`) |
| `NoAnswer` | `ChannelEof` | 400 | an ask no one can answer (a closed input). It is a `ServiceError`, and `Unanswered` is `true` |
| `SettingsError` | `SettingsError` | 500 | a settings (SQLite) read, write, or delete failed. `FromException` classifies the key: `DatabaseLocked`, `DiskError`, `DatabaseCorrupt`, `PermissionDenied`, else `SettingsError` |

Modules may subclass these too. `PLang/app/module/http/code/failure.cs` derives
from `ServiceError`.

### The exceptions that carry an `Error`

These are `AppException` subclasses, used where the answer has to be a throw
(§2). Each sets its own key and status.

| Class | Key | Status |
|---|---|---|
| `PrFormatOutdatedException` | `PrFormatOutdated` | 400 |
| `GoalNotFoundException` | `GoalNotFound` | 404 |
| `StepExecutionException` | `StepExecutionFailed` | 500 |
| `ModuleNotFoundException` | `ModuleNotFound` | 404 |
| `VariableNotFoundException` | `VariableNotFound` | 404 |
| `CallStackOverflowException` | `CallStackOverflow` | 500 |
| `SerializationException` | `SerializationFailed` | 500 |
| `DeclinedException` | the wrapped `Error`'s own | — (a type declined to make a value) |
| `CallbackGoalNotFound` | `CallbackGoalNotFound` | 404 (`CallbackGoalErrors.cs`) |
| `CallbackGoalHashMismatch` | `CallbackGoalHashMismatch` | 409 |
| `CallbackActionMismatch` | `CallbackActionMismatch` | 409 |
| `CallbackFrameIncomplete` | `CallbackFrameIncomplete` | 400 |

> **Subclass `FromException` methods hide the base one.** `ServiceError`,
> `GoalError`, `ActionError`, and `ProgramError` each declare their own
> `FromException` (with `new`), and they answer key `"Exception"`. The base
> `Error.FromException` is the door in §2. Call that one.

---

## 4. Where an error lives: `current` and `list`

An error is recorded against a call frame, and the frame keeps it in two places.

- **`Call.Errors`** — the errors this one frame observed (`list`).
- **`CallStack.Audit`** — the same errors, across the whole stack, handled or
  not.

`Call.Record(error, context)` is the one door into both. It stamps what the error
does not carry yet (the failing chain, the context, and the variables when the
error keeps them). It adds the error to the frame's list and the stack's audit.
The same instance is recorded once, so a layer that passes an error up does not
add it twice. A retry makes a new error, and that one is kept.

| plang | C# | Source |
|---|---|---|
| `%!app.error.current%` | `Error.Current(context)` → `context.call.Error` | `error/Error.cs`, `call/this.cs` |
| `%!app.error.list%` | `Error.Of(context)` → `context.call.Audit` | `error/Error.cs` |
| the frame's errors | `call.Errors` (an `app.error.list.@this`) | `call/this.cs` |

- **`current`** is the error in play for the asker: the newest error on its
  frame, and `null` once that frame has recovered (`Handled`). "Recovered" means
  the error is no longer in play.
- **`list`** is every error on the asker's stack, in the order they came. It is
  an `app.error.list.@this`: sealed, read-only to callers, and it owns its own
  append lock because parallel branches can add at once. It has `Newest` and an
  indexer. The app itself holds no errors: `Error.List(app)` returns an empty
  list.

> `current` depends on the frame's `Handled` state. `list` does not. That is the
> difference. Use `current` to answer "what is failing now", and `list` to
> answer "what has gone wrong in this run".

---

## 5. Written form

An error writes its own flat form. `Error.Write` emits its `id`, `message`,
`key`, `status`, `createdUtc`, `fixSuggestion`, `helpfulLinks`, its subclass
fields, and its `list` of causes. The live back-references (`Exception`, `Step`,
`Goal`, `CallFrames`) are not written, because they cannot round-trip. The
snapshot's call-stack section carries that chain instead. The reader is
`PLang/app/error/serializer/Reader.cs`. It rebuilds an error with `Restore`,
keeping its original `Id` and `CreatedUtc`.

---

## Find ... — cheat-sheet

| You have | You want | Look in |
|---|---|---|
| a `%!error%` line in a goal | the error's fields | `error/Error.cs` (`Key`, `Message`, `Status`) |
| `%!app.error.current%` | the error in play | `error/Error.cs` → `Current`, `call/this.cs` → `Error` |
| `%!app.error.list%` | every error this run | `error/Error.cs` → `Of`, `error/list/this.cs` |
| a failed `Data` | its error | `data/this.cs` → `FromError` |
| a key like `MissingParameter` | the class that sets it | `error/ValidationError.cs` |
| a key like `GoalNotFound` | the exception that carries it | `error/Exceptions.cs` |
| the wrap for an unknown C# exception | `ServiceError` 500 | `error/Error.cs` → `FromException` |
| a permission refusal | `PermissionDenied` 403 | `error/PermissionDenied.cs`, `type/item/path/this.Authorize.cs` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, and the path = namespace = source rule |
| `../goal/start.md` | goals and steps: where most errors are raised |
| `../data/start.md` | `Data`, the carrier every answer rides on |
