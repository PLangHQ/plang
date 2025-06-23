

using IdGen;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Runtime;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Dynamic.Core;

namespace PLang.Modules.MathModule
{
	[Description("Misc math method")]
	public class Program : BaseProgram
	{
		public Program() : base()
		{

		}


		public async Task<(decimal?, IError?)> MultiplyVariableByPower(object variable, int baseValue, int exponent)
		{
			if (!decimal.TryParse(Convert.ToString(variable), out var numericAmount))
				return (null, new ProgramError("Could not convert variable to decimal", goalStep, function, FixSuggestion: $"The variable value is: '{variable}' (without quotes)"));

			return (numericAmount * (decimal)Math.Pow(baseValue, exponent), null);
		}



	}
}

