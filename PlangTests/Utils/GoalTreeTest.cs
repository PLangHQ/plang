using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLangTests.Utils
{
	[TestClass]
	public class GoalTreeTest
	{


		[TestMethod]
		public void TestTree()
		{

			var root = new GoalTree<string>("Root {{ ChildrenElements0 }} {{ ChildrenElements1 }}", 0);
			var child1 = new GoalTree<string>("Child 1.0  {{ ChildrenElements0 }} ", 1);
			var child2 = new GoalTree<string>("Child 2.0  {{ ChildrenElements0 }} ", 1);

			var child1Sibling = new GoalTree<string>("ChildSibling 1.0 ", 1);
			var subChild1 = new GoalTree<string>("SubChild 1.1  {{ ChildrenElements0 }}  {{ ChildrenElements1 }}", 2);
			var subSubChild1 = new GoalTree<string>("SubSubChild 1.1.1", 3);
			var subSubChild2 = new GoalTree<string>("SubSubChild 1.1.2", 3);
			var subChild2 = new GoalTree<string>("SubChild 2.1  {{ ChildrenElements0 }}  {{ ChildrenElements1 }}", 2);
			var subSubChild3 = new GoalTree<string>("SubSubChild 2.1.1", 3);
			var subSubChild4 = new GoalTree<string>("SubSubChild 2.1.2", 3);
			var subSubChild5 = new GoalTree<string>("SubSubChild 2.1.3", 3);

			root.AddChild(child1);
			root.AddChild(child1Sibling);
			child1.AddChild(subChild1);
			subChild1.AddChild(subSubChild1);
			subChild1.AddChild(subSubChild2);
			root.AddChild(child2);
			child2.AddChild(subChild2);
			subChild2.AddChild(subSubChild3);
			subChild2.AddChild(subSubChild4);
			subChild2.AddChild(subSubChild5);

			// Print the tree from the deepest children up
			var result = root.PrintTree();

			Assert.AreEqual("Root Child 1.0  SubChild 1.1  SubSubChild 1.1.1  SubSubChild 1.1.2 ChildSibling 1.0 Child 2.0  SubChild 2.1  SubSubChild 2.1.1SubSubChild 2.1.2SubSubChild 2.1.3", result.Trim());
		}
	}
}
