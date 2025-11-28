using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Bcpg;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.OutputStream.Messages;
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
	[Description("DO NOT USE for if statements. Validates a variable, make sure it's not empty, follows a pattern, is a number, etc. ")]
	public class Program : BaseProgram
	{
		public Program()
		{
		}

		[Description("Check each %variable% if it is empty. Create error message fitting the intent of the validation. channel=error|warning|info|debug|trace. channel is null unless sepecifically defined by user e.g. channel:error")]
		public async Task<IError?> IsNotEmpty([HandlesVariable] List<ObjectValue> variables, ErrorMessage errorMessage)
		{
			if (string.IsNullOrEmpty(errorMessage.Content)) errorMessage = errorMessage with { Content = "Variables are empty" };
			if (variables == null)
			{
				return new UserInputError(errorMessage.Content, goalStep, errorMessage.Key, errorMessage.StatusCode, null, errorMessage.FixSuggestion, errorMessage.HelpfullLinks, null, errorMessage);
			}
			if (variables.Count == 0)
			{
				return new UserInputError(errorMessage.Content, goalStep, errorMessage.Key, errorMessage.StatusCode, null, errorMessage.FixSuggestion, errorMessage.HelpfullLinks, null, errorMessage);
			}

			var multiError = new GroupedErrors("ValidateModule.IsNotEmpty");

			foreach (var variable in variables)
			{
				if (variable.IsEmpty)
				{
					return new UserInputError(errorMessage.Content, goalStep, errorMessage.Key, errorMessage.StatusCode, null, errorMessage.FixSuggestion, errorMessage.HelpfullLinks, null, errorMessage);
				}
			}
			return (multiError.Count > 0) ? multiError : null;
		}

		[Description("[Depricated] Check each %variable% if it is empty. Create error message fitting the intent of the validation. Extract all %variables% from this statement as a JSON array of strings, ensuring it is not wrapped as a single string. channel=error|warning|info|debug|trace. channel is null unless sepecifically defined by user e.g. channel:error")]
		public async Task<IError?> IsNotEmpty([HandlesVariable] List<ObjectValue> variables, string errorMessage, int statusCode = 400)
		{
			if (string.IsNullOrEmpty(errorMessage)) errorMessage = "Variables are empty";
			if (variables == null)
			{
				return new ProgramError(errorMessage, goalStep, StatusCode: 500);
			}
			if (variables.Count == 0)
			{
				return new ProgramError(errorMessage, goalStep, StatusCode: 500);
			}

			var multiError = new GroupedErrors("ValidateModule.IsNotEmpty");

			foreach (var variable in variables)
			{
				if (variable.IsEmpty)
				{
					multiError.Add(new ProgramError(errorMessage, goalStep, StatusCode: statusCode, FixSuggestion: $"{variable.PathAsVariable} is empty and it must not be empty."));
					continue;
				}
			}
			return (multiError.Count > 0) ? multiError : null;
		}

		[Description("compairer:==|<|>|<=|>=|!=")]
		public async Task<IError?> IsLength([HandlesVariable] string variableName, int length, string compairer, string errorMessage, int statusCode = 400)
		{

			var variable = memoryStack.GetObjectValue(variableName);
			if (string.IsNullOrEmpty(errorMessage))
			{
				errorMessage = $"{variable.Name} is not {compairer} than {length}";
			}


			int varLength = 0;

			
			var value = variable.Value;
			if (value is int || value is long)
			{
				varLength = (int)value;
			} else
			{
				varLength = variable.ToString().Length;
			}

			if (compairer.Equals("==") && varLength != length)
			{
				return new ProgramError(errorMessage, goalStep);
			}
			if (compairer.Equals("!=") && varLength == length)
			{
				return new ProgramError(errorMessage, goalStep);
			}
			if (compairer.Equals(">") && varLength <= length)
			{
				return new ProgramError(errorMessage, goalStep);
			}
			if (compairer.Equals("<") && varLength >= length)
			{
				return new ProgramError(errorMessage, goalStep);
			}
			if (compairer.Equals("<=") && varLength > length)
			{
				return new ProgramError(errorMessage, goalStep);
			}

			if (compairer.Equals(">=") && varLength < length)
			{
				return new ProgramError(errorMessage, goalStep);
			}
			return null;
		}

			[Description("Checks if variable contains a regex pattern. Create error message fitting the intent of the validation")]
		public async Task<IError?> HasPattern([HandlesVariable] string[]? variables, string pattern, string errorMessage, int statusCode = 400)
		{
			if (variables == null) return new ProgramError("Variables are empty", goalStep);

			foreach (var variable in variables)
			{
				var obj = memoryStack.GetObjectValue(variable);
				string? valueToTest = null;
				if (obj.Initiated && obj.Value != null) { 
					valueToTest = obj.Value.ToString();	
				}

				if (string.IsNullOrEmpty(errorMessage))
				{
					errorMessage = $"{variable} does not match to the pattern: {pattern}";
				}

				if (!Regex.IsMatch(valueToTest ?? "", pattern))
				{
					return new ProgramError(errorMessage, goalStep, StatusCode: statusCode);
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
				var code = memoryStack.LoadVariables(variable)?.ToString()?.Trim();

				if (string.IsNullOrWhiteSpace(code) || code.Length != 2)
				{
					multiError.Add(new ProgramError($"{code} is not 2 letters", goalStep));
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

		public async Task<(List<object>?, IError?)> ValidateItemIsInList(object[] itemsToCheckInList, IList list, string? errorMessage = "item is not in list", bool caseSensitive = false)
		{
			StringComparison comparisonType = (caseSensitive) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			List<object> returnValues = new();
			foreach (var itemToCheckInList in itemsToCheckInList)
			{
				foreach (var item in list)
				{
					if (item is ObjectValue ov)
					{
						if (ov.Equals(itemToCheckInList.ToString(), comparisonType))
						{
							returnValues.Add(item);
						}
					} else if (item is string str)
					{
						if (str.Equals(itemToCheckInList.ToString(), comparisonType))
						{
							returnValues.Add(item);
						}
					}
					else if (item.Equals(itemToCheckInList))
					{
						returnValues.Add(item);
					}
				}
			}
			if (returnValues.Count > 0) return (returnValues, null);

			if (string.IsNullOrEmpty(errorMessage)) errorMessage = "item is not in list";

			return (null, new ProgramError(errorMessage, goalStep));
		}


		[Description("Validates if signed contract is valid. Uses properties on the contract from signatureProperties as signatures to validate against. propertiesToMatch specifies properties that should be used on the validation, when null all poperties are used except signatureProperties")]
		public async Task<IError?> ValidateSignedContract(object contract, List<string> signatureProperties, List<string>? propertiesToMatch = null)
		{
			return new ProgramError("Not implemented");
		}
	}
}