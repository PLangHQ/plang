using System.Collections;

namespace app.goal.steps.step.actions;

public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, IList<action.@this>
{
    private readonly List<action.@this> _items = new();

    public @this() { }
    public @this(IEnumerable<action.@this> actions) { _items = new List<action.@this>(actions); }

    [System.Text.Json.Serialization.JsonIgnore]
    public Step? Step { get; set; }

    public action.@this this[int index]
    {
        get { var a = _items[index]; a.Step ??= Step; return a; }
        set => _items[index] = value;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(action.@this item) => _items.Add(item);
    public void AddRange(IEnumerable<action.@this> items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(action.@this item) => _items.Contains(item);
    public void CopyTo(action.@this[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(action.@this item) => _items.IndexOf(item);
    public void Insert(int index, action.@this item) => _items.Insert(index, item);
    public bool Remove(action.@this item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);

    public IEnumerator<action.@this> GetEnumerator()
    {
        for (int i = 0; i < _items.Count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public List<action.@this> Value => _items;

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
    public bool IsFirstCondition(action.@this action)
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
    public List<(action.@this? condition, List<action.@this> body)> SplitAtConditions(int startIndex)
    {
        var branches = new List<(action.@this? condition, List<action.@this> body)>();
        List<action.@this>? currentBody = null;
        action.@this? currentCondition = null;

        for (int i = startIndex; i < _items.Count; i++)
        {
            var action = this[i];
            if (action.IsCondition)
            {
                if (currentBody != null)
                    branches.Add((currentCondition, currentBody));
                currentCondition = action;
                currentBody = new List<action.@this>();
            }
            else
            {
                currentBody ??= new List<action.@this>();
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
    public void GroupModifiers(global::app.module.@this modules)
    {
        if (_items.Count == 0) return;

        var flat = _items.ToList();
        _items.Clear();
        action.@this? current = null;

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
