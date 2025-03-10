using Org.BouncyCastle.Bcpg;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PLang.Modules.ValidateModule
{
	[Description("Validates a variable, make sure it's not empty, follows a pattern, is a number, etc.")]
	public class Program : BaseProgram
	{
		public Program()
		{
		}

		[Description("Check each %variable% if it is empty. Create error message fitting the intent of the validation. Extract all %variables% from this statement as a JSON array of strings, ensuring it is not wrapped as a single string.")]
		public async Task<IError?> IsNotEmpty([HandlesVariable] List<string> variables, string errorMessage, int statusCode = 400)
		{
			if (variables == null) { 
				return new ProgramError("Variables are empty", goalStep, function, StatusCode: 500);
			}

			var multiError = new GroupedErrors("ValidateModule.IsNotEmpty");
			foreach (var variable in variables)
			{
				var obj = memoryStack.GetObjectValue2(variable);
				if (obj.Initiated && obj.Value != null && !VariableHelper.IsEmpty(obj.Value)) continue;

				if (string.IsNullOrEmpty(errorMessage))
				{
					errorMessage = $"{variable} is empty. It must be set";
				}
				if (!obj.Initiated || obj.Value == null || (obj.Type == typeof(string) && string.IsNullOrWhiteSpace(obj.Value?.ToString())))
				{
					multiError.Add(new ProgramError(variable, goalStep, function, StatusCode: statusCode));
				}

				if (obj.Value is IList list && list.Count == 0)
				{
					multiError.Add(new ProgramError(variable, goalStep, function, StatusCode: statusCode));
				}

				if (obj.Value is IDictionary dict && dict.Count == 0)
				{
					multiError.Add(new ProgramError(variable, goalStep, function, StatusCode: statusCode));
				}
			}
			return (multiError.Count > 0) ? multiError : null;
		}

		[Description("Checks if variable contains a regex pattern. Create error message fitting the intent of the validation")]
		public async Task<IError?> HasPattern([HandlesVariable] string[]? variables, string pattern, string errorMessage, int statusCode = 400)
		{
			if (variables == null) return null;

			foreach (var variable in variables)
			{
				var obj = memoryStack.GetObjectValue2(variable);
				if (obj.Initiated || obj.Value != null) return null;

				if (string.IsNullOrEmpty(errorMessage))
				{
					errorMessage = $"{variable} does not match to the pattern: {pattern}";
				}

				if (!Regex.IsMatch(obj.Value?.ToString() ?? "", pattern))
				{
					return new ProgramError(errorMessage, goalStep, function, StatusCode: statusCode);
				}
			}
			
			return null;
		}

	}
}
