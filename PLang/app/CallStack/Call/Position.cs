using ActionEntity = app.Goals.Goal.Steps.Step.Actions.Action.@this;
using GoalEntity = app.Goals.Goal.@this;

namespace app.CallStack.Call;

/// <summary>
/// Snapshot surrogate for one Call frame. Carries the resolved live <see cref="ActionEntity"/>
/// (linked to its Step → Goal in the live App.Goals registry) plus the positional triple
/// captured at issue time. Not a replacement for <see cref="@this"/> — restored frames
/// are NOT pushable into the AsyncLocal Current and have no lifecycle. Callbacks read them
/// to identify the resume Position; Stage 4's <c>callback.Run</c> dispatches the bottom
/// frame's Action through the normal <see cref="app.@this.Run"/> path which Pushes a fresh
/// live Call.
/// </summary>
public sealed record Position(
    ActionEntity Action,
    GoalEntity Goal,
    int StepIndex,
    int ActionIndex,
    string Id);
