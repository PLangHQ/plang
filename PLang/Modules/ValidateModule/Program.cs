using Org.BouncyCastle.Bcpg;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SettingsService;
using PLang.Utils;
using ReverseMarkdown.Converters;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static PLang.Utils.VariableHelper;

namespace PLang.Modules.ValidateModule
{
	[Description("Validates a variable, make sure it's not empty, follows a pattern, is a number, etc.")]
	public class Program : BaseProgram
	{
		public Program()
		{
		}

		[Description("Check each %variable% if it is empty. Create error message fitting the intent of the validation. Extract all %variables% from this statement as a JSON array of strings, ensuring it is not wrapped as a single string.")]
		public async Task<IError?> IsNotEmpty([HandlesVariable] List<object> variables, string errorMessage, int statusCode = 400)
		{
			if (string.IsNullOrEmpty(errorMessage)) errorMessage = "Variables are empty";
			if (variables == null)
			{
				return new ProgramError(errorMessage, goalStep, function, StatusCode: 500);
			}
			if (variables.Count == 0)
			{
				return new ProgramError(errorMessage, goalStep, function, StatusCode: 500);
			}

			var multiError = new GroupedErrors("ValidateModule.IsNotEmpty");

			foreach (var variable in variables)
			{
				object? obj;
				if (variable is string variableName && VariableHelper.IsVariable(variableName))
				{
					var objectValue = memoryStack.GetObjectValue2(variableName);
					if (objectValue.Initiated && objectValue.Value != null && !VariableHelper.IsEmpty(objectValue.Value)) continue;

					if (!objectValue.Initiated || objectValue.Value == null || (objectValue.Type == typeof(string) && string.IsNullOrWhiteSpace(objectValue.Value?.ToString())))
					{
						multiError.Add(new ProgramError(variableName, goalStep, function, StatusCode: statusCode));
					}
					obj = objectValue.Value;
				}
				else
				{
					obj = variable;
				}

				if (obj == null)
				{
					multiError.Add(new ProgramError(variable.ToString(), goalStep, function, StatusCode: statusCode));
					continue;
				}

				if (obj is IList list && list.Count == 0)
				{
					multiError.Add(new ProgramError(variable.ToString(), goalStep, function, StatusCode: statusCode));
					continue;
				}

				if (obj is IDictionary dict && dict.Count == 0)
				{
					multiError.Add(new ProgramError(variable.ToString(), goalStep, function, StatusCode: statusCode));
					continue;
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
		[Description("Checks if variable is a valid 2 letter country code")]
		public async Task<IError?> IsValid2LetterCountryCode([HandlesVariable] string[]? variables, string pattern, string errorMessage, int statusCode = 400)
		{
			var multiError = new GroupedErrors("ValidateModule.IsValid2LetterCountryCode");

			foreach (var variable in variables)
			{
				var code = variableHelper.LoadVariables(variable)?.ToString()?.Trim();

				if (string.IsNullOrWhiteSpace(code) || code.Length != 2)
				{
					multiError.Add(new ProgramError($"{code} is not 2 letters"));
				}
				else
				{

					try
					{
						var region = new RegionInfo(code.ToUpperInvariant());

					}
					catch (Exception ex)
					{
						{
							multiError.Add(new ProgramError($"{code} could not be assigned to country", Exception: ex));
						}
					}
				}


			}
			if (multiError.Count > 0) return multiError;
			return null;
		}


		[Description("Validates if contract is valid. Uses properties on the contract from signatureProperties as signatures to validate against. propertiesToMatch specifies properties that should be used on the validation, when null all poperties are used except signatureProperties")]
		public async Task<IError?> ValidateContract(object contract, List<string> signatureProperties, List<string>? propertiesToMatch = null)
		{
			return new ProgramError("Not implemented");
		}
	}
}