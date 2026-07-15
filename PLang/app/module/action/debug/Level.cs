namespace app.module.action.debug;

/// <summary>Debug detail level. Selected via <c>--debug={"level":"step"|"action"}</c>;
/// the walk's <c>choice&lt;Level&gt;</c> conversion rejects any other value. Owner's
/// namespace (<c>debug</c>) carries the context — no <c>Debug</c> prefix (mirrors <c>test.Format</c>).</summary>
public enum Level
{
    /// <summary>Trace at step boundaries (default).</summary>
    Step,
    /// <summary>Trace between actions too — shows state on every action.</summary>
    Action
}
