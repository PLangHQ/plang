namespace app.type.item.setting;

/// <summary>
/// An owner that has settings names their class: <c>goal.list : ISetting&lt;goal.list.setting&gt;</c>.
/// plang reads them through the owner — <c>%!app.goal.list.setting.os%</c> — as the asker's settings
/// build them; <c>set</c> writes this run's, <c>save</c> the actor's row.
/// </summary>
public interface ISetting<T> where T : @this, new();
