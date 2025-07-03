using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

	
	


		[Description("User can force the type of expectedValue and actualValue, it should be FullName type, e.g. System.Int64, System.Double, etc. By default the types are not set and the runtime will try to match them")]
		public async Task<IError?> IsEqual(object? expectedValue, object? actualValue, string resultVariable = "assertResult", string? expectedValueType = null, string? actualValueType = null)
		{
			if (expectedValue is ObjectValue ov)
			{
				expectedValue = ov.Value;
			}
			if (actualValue is ObjectValue ov2)
			{
				actualValue = ov2.Value;
			}

			bool result = false;
			if (expectedValueType == null && actualValueType == null)
			{
				var conditionProgram = GetProgramModule<ConditionalModule.Program>();
				var condition = await conditionProgram.IsEqual(expectedValue, actualValue);
				if (condition.Error != null) return condition.Error;

				result = condition.Result ?? false;
			}
			else
			{

				if (expectedValueType != null)
				{
					expectedValue = Convert.ChangeType(expectedValue, Type.GetType(expectedValueType));
				}
				if (actualValueType != null)
				{
					actualValue = Convert.ChangeType(actualValue, Type.GetType(actualValueType));
				}

				if (expectedValue != null)
				{
					result = expectedValue.Equals(actualValue);
				}
			}

			

			if (result) {
				memoryStack.Put(resultVariable, new { Message = "Success", Success = true, ExpectedValue = expectedValue, ActualValue = actualValue, StepText = goalStep.Text }, goalStep: goalStep);
				return null;
			}
			 
			memoryStack.Put(resultVariable, new { Message = "Failed", Success = false, ExpectedValue = expectedValue , ActualValue = actualValue }, goalStep: goalStep);
			return null;	
		}
	}
}
