using System.Text.RegularExpressions;

namespace PLang.Utils;

public class GoalTree<T>
{
    public GoalTree(string value, int indent)
    {
        Value = value;
        Indent = indent;
        Current = this;
        Depth = 0;
    }

    public string Value { get; set; }
    public int Indent { get; }
    public int Index { get; set; }
    public int Depth { get; set; }
    public string GoalHash { get; set; }
    public string StepHash { get; set; }
    public bool IsRendered { get; set; }
    public GoalTree<T>? Parent { get; set; }
    public GoalTree<T> Current { get; set; }
    public List<GoalTree<T>> Children { get; set; } = new();

    public void AddChild(GoalTree<T> child)
    {
        child.Index = Children.Count;
        child.Parent = this;
        child.Depth = Depth + 1;
        Children.Add(child);
    }

    public static List<GoalTree<T>> GetAllNodes(GoalTree<T> root)
    {
        List<GoalTree<T>> nodes = new();
        nodes.Add(root);
        foreach (var child in root.Children) nodes.AddRange(GetAllNodes(child));
        return nodes;
    }

    public void TraverseFromDeepest(Action<GoalTree<T>> action)
    {
        foreach (var child in Children) child.TraverseFromDeepest(action);
        action(this);
    }

    public string PrintTree()
    {
        var nodes = GetAllNodes(this);
        var orderedByDepth = nodes.OrderByDescending(p => p.Depth);
        foreach (var node in orderedByDepth)
        {
            var parent = node.Parent;

            if (parent == null || parent.IsRendered) continue;
            var children = parent.Children;

            var matches = Regex.Matches(parent.Value, "{{ ChildElement\\d+ }}");
            if (matches.Count == children.Count)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    var content = SetId(children[i].StepHash, children[i].Value);
                    parent.Value = parent.Value.Replace(matches[i].Value, content);
                }

                parent.Value = SetId(parent.GoalHash, parent.Value, "goal");
            }
            else if (matches.Count > 0)
            {
                var collected = "";
                foreach (var child in children)
                {
                    var content = SetId(child.StepHash, child.Value);
                    collected += content;
                }

                var str = parent.Value.Replace(matches[0].Value, collected);
                for (var i = 1; i < matches.Count; i++) str = str.Replace(matches[i].Value, "");
                parent.Value = SetId(children[0].GoalHash, str, "goal");
            }
            else
            {
                var content = "";
                foreach (var child in children) content += SetId(child.StepHash, child.Value);
                parent.Value += SetId(children[0].GoalHash, content, "goal");
            }

            parent.IsRendered = true;
        }

        var html = SetId(GoalHash, Value, "goal");

        return html;


        /*
        StringBuilder sb = new StringBuilder();
        TraverseFromDeepest(node => {

            if (node.Parent != null)
            {
                if (node.Parent.Value.Contains($"{{{{ ChildrenElements{node.Index} }}}}"))
                {
                    var str = node.Parent.Value.ToString().Replace($"{{{{ ChildrenElements{node.Index} }}}}", node.Value);
                    node.Parent.Value = str;
                } else
                {
                    node.Parent.Value += node.Value;
                }

            } else
            {

                sb.Append(node.Value);
            }


        });
        return sb.ToString();*/
    }

    public string SetId(string id, string html, string section = "step")
    {
        /*if (section == "goal")
        {
            return $@"<plang id=""{id}"">{html}</plang>";
        }*/

        var idx = html.IndexOf("<");
        if (idx == -1) return html;

        var endIdx = html.IndexOf('>', idx);
        if (endIdx == -1) return html;
        var newTag = html.Substring(idx, endIdx - idx + 1);
        var newTagWithAttribute = newTag.Insert(newTag.Length - 1, $" plang-{section}-id=\"{id}\"");

        // Reconstruct the HTML string with the new tag
        return html.Substring(0, idx) + newTagWithAttribute + html.Substring(endIdx + 1);
    }
}