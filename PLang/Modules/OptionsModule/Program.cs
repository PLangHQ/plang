using Ganss.Xss;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Errors.Types;
using PLang.Exceptions;
using PLang.Models;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.ComponentModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using static PLang.Utils.StepHelper;

namespace PLang.Modules.OptionsModule
{
	[Description("Set options for the code. Only set the options that are defined by user")]
	public class Program : BaseProgram
	{
		
		public Program() : base()
		{
		}

		[Description("Only set the options that are defined by user")]
		public async Task SetHtmlSanitizerOptions(HtmlSanitizerOptions options, bool keepDefaults = true)
		{

			HtmlSanitizerOptions defaults = SetDefaults(new(), options);


			context.AddVariable(defaults);
		}

		private T SetDefaults<T>(T defaults, T userDefined)
		{
			foreach (var prop in typeof(T).GetProperties())
			{
				var userValue = prop.GetValue(userDefined);
				if (userValue != null)
				{
					prop.SetValue(defaults, userValue);
				}
			}
			return defaults;
		}

	}
}
