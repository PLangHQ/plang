using System.Collections;
using app.actor.context;
using app.data;
using app.module;

namespace app.goal.steps;

public sealed class @this : IList<Step>, IContext
{
    private readonly List<Step> _items = new();

    public @this() { }
    public @this(IEnumerable<Step> steps) { _items = new List<Step>(steps); }

    public actor.context.@this Context { get; set; } = null!;

    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.goal.@this Goal { get; set; } = null!;

    // --- IList<Step> implementation ---

    public Step this[int index]
    {
        get { var s = _items[index]; s.Goal ??= Goal; return s; }
        set => _items[index] = value;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(Step item) => _items.Add(item);
    public void AddRange(IEnumerable<Step> items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(Step item) => _items.Contains(item);
    public void CopyTo(Step[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(Step item) => _items.IndexOf(item);
    public void Insert(int index, Step item) => _items.Insert(index, item);
    public bool Remove(Step item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);

    /// <summary>
    /// Structural iteration — every step, no execution context. The disabled-skip is
    /// execution-only; RunAsync does it via skipBelowIndent, so the enumerator never filters.
    /// </summary>
    public IEnumerator<Step> GetEnumerator()
    {
        foreach (var step in _items) { step.Goal ??= Goal; yield return step; }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns true if the step at the given index has a following step with higher indent.
    /// </summary>
    internal bool HasIndentedChildren(int index)
    {
        return index + 1 < _items.Count && _items[index + 1].Indent > _items[index].Indent;
    }

    /// <summary>
    /// Groups modifier actions onto their preceding executable action for every step
    /// in this collection. Delegates per-step grouping to Actions.GroupModifiers —
    /// Steps owns the iteration (OBP rule 5).
    /// </summary>
    public void GroupAllModifiers(global::app.module.list.@this modules)
    {
        foreach (var step in _items)
            step.Actions.GroupModifiers(modules);
    }

    /// <summary>
    /// Merges LLM-derived fields from a prior Steps collection onto this one.
    /// Exact-text match only — robust to reorder/insert/delete without pairing unrelated
    /// steps. A text change drops the prior mapping; the LLM rebuilds that step fresh.
    /// Earlier positional fallback was dropped because it silently paired structurally
    /// unrelated steps (e.g. refactored goal where NEW step 0 has different intent from
    /// OLD step 0) and fed the builder wrong @known hints.
    /// Sets PriorText on each merged step so the builder template can emit @known.
    /// </summary>
    public void MergeFrom(@this priorSteps)
    {
        if (priorSteps == null || priorSteps.Count == 0) return;

        var consumed = new HashSet<int>();
        for (int cur = 0; cur < _items.Count; cur++)
        {
            var step = _items[cur];
            for (int i = 0; i < priorSteps.Count; i++)
            {
                if (consumed.Contains(i)) continue;
                if (priorSteps[i].Text == step.Text)
                {
                    step.Merge(priorSteps[i]);
                    step.PriorText = priorSteps[i].Text;
                    consumed.Add(i);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Runs all steps in sequence. Owns the iteration loop (OBP rule 5).
    /// Handles sub-step skipping (condition-gated), cancellation, and return propagation.
    /// </summary>
    public async Task<data.@this> RunAsync(actor.context.@this context)
    {
        data.@this result = context.Ok();
        int? skipBelowIndent = null;

        for (int i = 0; i < _items.Count; i++)
        {
            var step = _items[i];
            step.Goal ??= Goal;

            // Sub-step skip: if condition was false, skip indented children
            if (skipBelowIndent != null)
            {
                if (step.Indent > skipBelowIndent)
                    continue;
                skipBelowIndent = null;
            }

            result = await step.RunAsync(context);

            if (result.ShouldExit()) return result;

            // Sub-step control: false condition skips indented children. The
            // verdict reads through the truthiness door — the value answers for
            // itself (bool.@this bottoms out at its backing).
            if (i + 1 < _items.Count && _items[i + 1].Indent > step.Indent
                && step.Actions.Count > 0
                && string.Equals(step.Actions[0].Module, "condition", StringComparison.OrdinalIgnoreCase)
                && result.Success
                && !(await result.ToBooleanAsync()))
            {
                skipBelowIndent = step.Indent;
            }

            if (context.CancellationToken.IsCancellationRequested)
                return context.Error(new app.error.Error("Operation was cancelled", "Cancelled", 499));
        }

        return result;
    }

}
