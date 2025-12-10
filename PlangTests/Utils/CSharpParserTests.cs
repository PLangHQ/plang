using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Models;
using PLang.Modules.PlangModule.Data;
using PLang.Utils;
using PLangTests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils.Tests
{
	[TestClass()]
	public class CSharpParserTest
	{


		[TestMethod()]
		public void GetStringToHashTest()
		{
			var code = File.ReadAllText(@"C:\Users\Ingi Gauti\source\repos\plang\PLang\Modules\PlangModule\Data.cs");
			var parser = new CSharpParser();
			parser.LoadCode(code);

			
			
			var prStepType = parser.GetType("PrStep");
			var constructorTypes = parser.GetConstructorTypes(prStepType);

			Console.WriteLine(prStepType.ConstructorSource + ";");
			foreach (var ctype in constructorTypes)
			{
				Console.WriteLine(ctype.ConstructorSource + ";");
			}

			var type = typeof(GoalToCallInfo);

			var path = SourceCodeLocator.GetSourceFileForType(type);
			var code2 = File.ReadAllText(path);
			parser.LoadCode(code2);

			prStepType = parser.GetType("GoalToCallInfo");
			constructorTypes = parser.GetConstructorTypes(prStepType);

			Console.WriteLine(prStepType.ConstructorSource + ";");
			foreach (var ctype in constructorTypes)
			{
				Console.WriteLine(ctype.ConstructorSource + ";");
			}


			var records = parser.GetRecords();
			var classes = parser.GetClasses();
			int i = 0;
		}

	}
}