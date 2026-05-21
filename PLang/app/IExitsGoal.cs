namespace app;

/// <summary>
/// Marker interface. Any type whose presence in a step result means
/// "stop here, capture a Snapshot, return through the channel" implements
/// this. The engine queries via <c>result.Type?.ClrType?.Exit() == true</c>
/// — it never decomposes the Data's Value.
///
/// Stage 2a only implementer: <see cref="app.modules.output.Ask"/>.
/// </summary>
public interface IExitsGoal { }
