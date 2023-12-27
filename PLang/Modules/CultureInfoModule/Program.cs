using System.ComponentModel;
using System.Globalization;

namespace PLang.Modules.CultureInfoModule
{
	[Description("Various settings for the program, such as culture, date & number formatting")]
	public class Program : BaseProgram
	{
		public Program() : base()
		{
		}

		[Description("Make sure to convert user code to valid BCP 47 code")]
		public async Task SetCultureLanguageCode(string code = "en-US")
		{
			var ci = new CultureInfo(code);
			Thread.CurrentThread.CurrentCulture = ci;
			Thread.CurrentThread.CurrentUICulture = ci;
			CultureInfo.DefaultThreadCurrentCulture = ci;
			CultureInfo.DefaultThreadCurrentUICulture = ci;
		}

		[Description("Make sure to convert user code to valid BCP 47 code")]
		public async Task SetCultureUILanguageCode(string code = "en-US")
		{
			var ci = new CultureInfo(code);
			Thread.CurrentThread.CurrentUICulture = ci;
			CultureInfo.DefaultThreadCurrentUICulture = ci;
		}


	}
}

