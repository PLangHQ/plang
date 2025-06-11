using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;

namespace PLang.Modules.AssertModule
{
	[Description("Assert object/variable/text or other entity to be what is epxected. For unit testing")]
	public class Program : BaseProgram
	{

		public Program()
		{
		}
		
		public async Task IsEqual(object? expectedValue, object? actualValue, string resultVariable = "assertResult")
		{
			if (expectedValue is ObjectValue ov)
			{
				expectedValue = ov.Value;
			}
			if (actualValue is ObjectValue ov2)
			{
				actualValue = ov2.Value;
			}

			if (expectedValue?.Equals(actualValue) == true) {
				memoryStack.Put(resultVariable, new { Message = "Success", Success = true });
			}

			var outputExpectedValue = expectedValue;
			var outputActualValue = actualValue;
			if (TypeHelper.IsConsideredPrimitive(actualValue.GetType())) {
				outputExpectedValue = JsonConvert.SerializeObject(expectedValue, Formatting.Indented);
				outputActualValue = JsonConvert.SerializeObject(expectedValue, Formatting.Indented);
			}
			memoryStack.Put(resultVariable, new { Message = "Failed", Success = false, ExpectedValue = expectedValue , ActualValue = actualValue });
			
		}
	}
}
