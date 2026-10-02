namespace PLang;

/// <summary>Tests read a goal list's <c>Find</c> answer as the goal it holds: null for a miss or a failure (the answer
/// itself says which).</summary>
public static class GoalFindExtensions
{
    public static async System.Threading.Tasks.Task<global::app.goal.@this?> Found(
        this System.Threading.Tasks.Task<global::app.data.@this<global::app.goal.@this>> find)
        => (await find).Peek() as global::app.goal.@this;
}
