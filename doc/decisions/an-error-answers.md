# An error answers; it travels as an exception only where it must

**Chosen.** A method that can return a result answers `Fail` / `FromError` with the error's own key. Where no result can be returned (a constructor, a stream callback, a member with no `Data` return) it throws `AppException`, which carries its `Error` whole. Every catch answers `ex.Error`. `Error.FromException(ex)` is the one door from an exception to an error: an `AppException` comes back whole, anything else is a `ServiceError` (500) naming the C# type (`CLAUDE.md`, "A program's error answers"; `PLang/app/error/this.cs:269-278`).

**Why, as recorded.** The source comments state the shape: an `AppException` is "A program's error that has to travel as an exception — from a place that can't answer a result (a constructor, a stream callback, a member with no Data return). It carries its Error whole; every catch answers that Error" (`PLang/app/error/Exceptions.cs:3-7`). And: "Any other exception is one plang didn't raise — a C# fault — and has no program key: ServiceError (500), its message naming the exception's type" (`error/this.cs:271-272`).

**Not recorded.** Why a key is never built from the class name, and why a caught error is never taken apart into message, key and status: `CLAUDE.md` states both as rules without a reason. Reason not recorded.

**Held by.** Review. The action's and the step's catches follow it (`goal/step/action/this.cs:333-337`, `goal/step/this.cs:153-157`). The action's catch lets `NullReferenceException` through to the step (`action/this.cs:334`); the repository does not say why.
