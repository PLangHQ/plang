using System.Collections;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;

namespace PLang.Runtime2.Engine.Goals.Goal.Steps;

public sealed class @this : IList<Step.@this>, IContext
{
    private readonly List<Step.@this> _items = new();

    public @this() { }
    public @this(IEnumerable<Step.@this> steps) { _items = new List<Step.@this>(steps); }

    public PLangContext Context { get; set; } = null!;

    public List<Step.@this> Value => _items;

    // --- IList<Step> implementation ---

    public Step.@this this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(Step.@this item) => _items.Add(item);
    public void AddRange(IEnumerable<Step.@this> items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(Step.@this item) => _items.Contains(item);
    public void CopyTo(Step.@this[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(Step.@this item) => _items.IndexOf(item);
    public void Insert(int index, Step.@this item) => _items.Insert(index, item);
    public bool Remove(Step.@this item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);

    /// <summary>
    /// Custom enumerator that stamps context on each step and skips disabled steps.
    /// The condition module marks indented sub-steps as disabled when a condition is false.
    /// </summary>
    public IEnumerator<Step.@this> GetEnumerator()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var step = _items[i];
            step.Context = Context;

            if (step.Disabled)
            {
                // Clear the disabled flag after skipping so re-execution works
                step.Disabled = false;
                continue;
            }

            yield return step;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns true if the step at the given index has a following step with higher indent.
    /// </summary>
    internal bool HasIndentedChildren(int index)
    {
        return index + 1 < _items.Count && _items[index + 1].Indent > _items[index].Indent;
    }

    public async Task<Data> Load(PLangContext context)
    {
        foreach (var step in _items)
        {
            var result = await step.Load(context);
            if (!result.Success) return result;
        }
        return Data.Ok();
    }

    /// <summary>
    /// Runs all steps in order. Owns the iteration loop (OBP rule 5).
    /// Sub-step logic: when a step returns a bool (condition step), its value
    /// controls whether indented children execute. False skips them.
    /// Non-bool results don't affect children — they always execute.
    /// When context.Setup is set, implements run-once semantics.
    /// All sub-step state is local — fully thread-safe.
    /// </summary>
    public async Task<Data> RunAsync(Engine.@this engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        Data? lastResult = null;
        int? skipBelowIndent = null;

        for (var i = 0; i < _items.Count; i++)
        {
            var step = _items[i];

            // Sub-step skip: if we're skipping indented children, check indent level
            if (skipBelowIndent != null)
            {
                if (step.Indent > skipBelowIndent)
                    continue;
                skipBelowIndent = null;
            }

            // Setup run-once check: skip steps that have already been executed
            if (context.Setup != null && await context.Setup.IsExecuted(step, engine))
                continue;

            var stepResult = await step.RunAsync(engine, context, cancellationToken);

            // Sub-step control: only condition module steps can skip indented children.
            // A false result from a condition skips children. Non-condition steps never skip.
            if (HasIndentedChildren(i) && IsConditionStep(step) && stepResult.Value is bool condition && !condition)
                skipBelowIndent = step.Indent;

            // Determine if the step error is tolerated:
            // - Success (no error)
            // - Explicit IgnoreError on the step
            // - Setup-tolerable errors (e.g. "already exists", "duplicate column name")
            var errorTolerated = stepResult.Success
                || (step.OnError?.IgnoreError ?? false)
                || (context.Setup != null && context.Setup.IsTolerableError(stepResult));

            // Record in setup table only on success or tolerated errors.
            if (context.Setup != null && errorTolerated)
            {
                var recordResult = await context.Setup.Record(step, engine, stepResult.Success ? null : stepResult.Error);
                if (!recordResult.Success)
                    return recordResult;
            }

            if (!stepResult && !errorTolerated)
                return stepResult;

            if (cancellationToken.IsCancellationRequested)
                return Data.FromError(GoalError.Cancelled(context));

            lastResult = stepResult;
        }

        return lastResult ?? Data.Ok();
    }

    /// <summary>
    /// Returns true if the step's first action is from the condition module.
    /// Only condition steps can skip indented children based on their result.
    /// </summary>
    private static bool IsConditionStep(Step.@this step)
    {
        return step.Actions.Count > 0 &&
            string.Equals(step.Actions[0].Module, "condition", StringComparison.OrdinalIgnoreCase);
    }
}
