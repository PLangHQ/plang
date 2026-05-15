using System.Collections;

namespace app.Goals.Goal.Steps.Step.Actions;

public sealed class @this : IList<Action.@this>
{
    private readonly List<Action.@this> _items = new();

    public @this() { }
    public @this(IEnumerable<Action.@this> actions) { _items = new List<Action.@this>(actions); }

    [System.Text.Json.Serialization.JsonIgnore]
    public Step.@this? Step { get; set; }

    public Action.@this this[int index]
    {
        get { var a = _items[index]; a.Step ??= Step; return a; }
        set => _items[index] = value;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(Action.@this item) => _items.Add(item);
    public void AddRange(IEnumerable<Action.@this> items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(Action.@this item) => _items.Contains(item);
    public void CopyTo(Action.@this[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(Action.@this item) => _items.IndexOf(item);
    public void Insert(int index, Action.@this item) => _items.Insert(index, item);
    public bool Remove(Action.@this item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);

    public IEnumerator<Action.@this> GetEnumerator()
    {
        for (int i = 0; i < _items.Count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public List<Action.@this> Value => _items;

    /// <summary>
    /// Index of the first condition.if action, or -1 if none.
    /// </summary>
    public int FirstConditionIndex()
    {
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].IsCondition) return i;
        return -1;
    }

    /// <summary>
    /// True when the given action is the first condition.if in this collection.
    /// Used by the coverage subscriber to ignore inner-elseif simple-path firings.
    /// </summary>
    public bool IsFirstCondition(Action.@this action)
    {
        foreach (var a in _items)
        {
            if (!a.IsCondition) continue;
            return ReferenceEquals(a, action);
        }
        return false;
    }

    /// <summary>
    /// Declared branch-label chain for a condition.if site rooted at myIndex.
    /// Single-action step: simple path → [true, false]. Multi-action step:
    /// orchestrate path → one entry per condition.* action, tagged by its action name:
    /// condition.if → "if", condition.elseif → "elseif[N]", condition.else → "else".
    /// Shared by runtime (If.Run) and discovery (test.discover seeding) so both agree
    /// on site shape.
    /// </summary>
    public List<string> ComputeBranchChain(int myIndex)
    {
        if (_items.Count == 1)
            return new List<string> { "true", "false" };

        var chain = new List<string>();
        for (int i = myIndex; i < _items.Count; i++)
        {
            var a = _items[i];
            if (!a.IsCondition) continue;
            if (string.Equals(a.ActionName, "if", StringComparison.OrdinalIgnoreCase))
                chain.Add("if");
            else if (string.Equals(a.ActionName, "else", StringComparison.OrdinalIgnoreCase))
                chain.Add("else");
            else
                chain.Add($"elseif[{chain.Count}]");
        }
        return chain;
    }

    /// <summary>
    /// Groups actions from startIndex into (condition, body) branches. A condition.if
    /// starts a new branch; non-condition actions append to the current branch's body.
    /// Trailing body-only actions attach to the last condition.
    /// Reads via the indexer so every returned action has Step propagated — callers
    /// (condition.if.Orchestrate) invoke these actions and need Step set for the
    /// alreadyOrchestrating guard, DisableChildrenOf, and coverage site keys.
    /// </summary>
    public List<(Action.@this? condition, List<Action.@this> body)> SplitAtConditions(int startIndex)
    {
        var branches = new List<(Action.@this? condition, List<Action.@this> body)>();
        List<Action.@this>? currentBody = null;
        Action.@this? currentCondition = null;

        for (int i = startIndex; i < _items.Count; i++)
        {
            var action = this[i];
            if (action.IsCondition)
            {
                if (currentBody != null)
                    branches.Add((currentCondition, currentBody));
                currentCondition = action;
                currentBody = new List<Action.@this>();
            }
            else
            {
                currentBody ??= new List<Action.@this>();
                currentBody.Add(action);
            }
        }
        if (currentBody != null)
            branches.Add((currentCondition, currentBody));
        return branches;
    }

    /// <summary>
    /// Takes a flat list where modifier actions follow their target action, and groups
    /// each modifier onto the preceding executable action's Modifiers collection.
    /// Modifiers are sorted by [Modifier(Order = N)] so the outermost wrapper comes first.
    /// A leading modifier with no preceding executable is dropped. Mutates in place.
    /// </summary>
    public void GroupModifiers(Modules.@this modules)
    {
        if (_items.Count == 0) return;

        var flat = _items.ToList();
        _items.Clear();
        Action.@this? current = null;

        foreach (var action in flat)
        {
            if (modules.IsModifier(action.Module, action.ActionName))
            {
                if (current == null)
                {
                    Step?.Warnings.Add(new Info
                    {
                        Key = "DroppedLeadingModifier",
                        Message = $"Modifier '{action.Module}.{action.ActionName}' has no preceding action and was dropped"
                    });
                    continue;
                }
                current.Modifiers.Add(action);
            }
            else
            {
                current = action;
                _items.Add(action);
            }
        }

        foreach (var action in _items)
        {
            if (action.Modifiers.Count <= 1) continue;
            var sorted = action.Modifiers
                .OrderBy(m => modules.GetModifierOrder(m.Module, m.ActionName))
                .ToList();
            action.Modifiers.Clear();
            foreach (var m in sorted) action.Modifiers.Add(m);
        }
    }
}
