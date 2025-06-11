using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Models;
using PLang.Modules.LoopModule;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLangTests.Modules.LoopModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{
		Program p;

		[TestInitialize]
		public void Init() {
			base.Initialize();


			var goal = new Goal();
			goal.RelativeAppStartupFolderPath = "/";

			p = new Program(logger, pseudoRuntime, engine);
			p.Init(container, goal, null, null, null);
		}

		[TestMethod]
		public async Task RunLoop_List_Test()
		{
			var listName = "products";
			
			var parameters = new Dictionary<string, object>();

			var products = new List<string>();
			products.Add("Product 1");
			products.Add("Product 2");
			products.Add("Product 3");
			products.Add("Product 4");
			memoryStack.Put(listName, products);

			GoalToCallInfo goalNameToCall = new("!Process", parameters);

			await p.RunLoop(listName, goalNameToCall);

			await pseudoRuntime.Received(4).RunGoal(Arg.Any<IEngine>(), context, Arg.Any<string>(), goalNameToCall, Arg.Any<Goal>());
		}

		public record Product(string Name, double Price);

		[TestMethod]
		public async Task RunLoop_Dictionary_Test()
		{
			var dictName = "products";
			string goalNameToCall = "!Process";
			var parameters = new Dictionary<string, object>();

			var products = new Dictionary<string, object>();
			products.Add("Product 1", new Product("Nr1", 100));
			products.Add("Product 2", new Product("Nr2", 200));
			products.Add("Product 3", new Product("Nr3", 300));
			products.Add("Product 4", new Product("Nr4", 400));
			memoryStack.Put(dictName, products);

			await p.RunLoop(dictName, goalNameToCall);

			await pseudoRuntime.Received(4).RunGoal(Arg.Any<IEngine>(), context, Arg.Any<string>(), goalNameToCall, Arg.Any<Goal>());
		}

		[TestMethod]
		public async Task RunLoop_ChangeDefaultValues_Test()
		{
			var listName = "products";
			
			var parameters = new Dictionary<string, object>();
			parameters.Add("list", "products");
			parameters.Add("item", "product");
			parameters.Add("idx", "index");
			parameters.Add("listCount", "numberOfProducts");

			var products = new List<string>();
			products.Add("Product 1");
			products.Add("Product 2");
			products.Add("Product 3");
			products.Add("Product 4");
			memoryStack.Put(listName, products);

			GoalToCallInfo goalNameToCall = new("!Process", parameters);

			await p.RunLoop(listName, goalNameToCall);

			await pseudoRuntime.Received(4).RunGoal(Arg.Any<IEngine>(), context, Arg.Any<string>(), goalNameToCall, Arg.Any<Goal>());
		}

		private bool DictionaryCheck(Dictionary<string, object> p)
		{
			return p.ContainsKey("products") || p.ContainsKey("product") || p.ContainsKey("index") || p.ContainsKey("numberOfProducts");
		}
	}
}
