using System.Text.RegularExpressions;

namespace PLang.Utils
{
	public class GoalTree<T>
	{
		public string Value { get; set; }
		public int Indent { get; }
		public int Index { get; set; }
		public int Depth { get; set; }
		public bool IsRendered { get; set; }
		public GoalTree<T>? Parent { get; set; }
		public GoalTree<T> Current { get; set; }
		public List<GoalTree<T>> Children { get; set; } = new List<GoalTree<T>>();

		public GoalTree(string value, int indent)
		{
			Value = value;
			Indent = indent;
			Current = this;
			Depth = 0;
		}

		public void AddChild(GoalTree<T> child)
		{
			child.Index = Children.Count;
			child.Parent = this;
			child.Depth = this.Depth + 1;
			Children.Add(child);
		}

		public static List<GoalTree<T>> GetAllNodes(GoalTree<T> root)
		{
			List<GoalTree<T>> nodes = new List<GoalTree<T>>();
			nodes.Add(root);
			foreach (var child in root.Children)
			{
				nodes.AddRange(GetAllNodes(child));
			}
			return nodes;
		}

		public void TraverseFromDeepest(Action<GoalTree<T>> action)
		{
			foreach (var child in Children)
			{
				child.TraverseFromDeepest(action);
			}
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

				var matches = Regex.Matches(parent.Value, $"{{{{ ChildrenElements\\d+ }}}}");
				if (matches.Count == children.Count)
				{
					for (int i=0;i<children.Count;i++)
					{
						var str = parent.Value.ToString().Replace(matches[i].Value, children[i].Value);
						parent.Value = str;
					}
				} else if (matches.Count > 0)
				{
					string collected = "";
					foreach (var child in children)
					{
						collected += child.Value;
					}

					var str = parent.Value.ToString().Replace(matches[0].Value, collected);
					for (var i = 1; i < matches.Count;i++)
					{
						str = str.Replace(matches[i].Value, "");
					}
					parent.Value = str;
				}
				else
				{
					foreach (var child in children)
					{
						parent.Value += child.Value;
					}
				}
				parent.IsRendered = true;
			}
			return this.Value;
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

	}

}
