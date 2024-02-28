using PLang.Utils;
using System.Diagnostics;

namespace PlangWindowForms
{
	internal static class Program
	{
		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			if (args.FirstOrDefault(p => p == "--debug") != null)
			{
				AppContext.SetSwitch(ReservedKeywords.Debug, true);
			}
			if (args.FirstOrDefault(p => p == "--csdebug") != null && !Debugger.IsAttached)
			{
				Debugger.Launch();
				AppContext.SetSwitch(ReservedKeywords.CSharpDebug, true);
			}
			var build = args.FirstOrDefault(p => p == "build") != null;
			if (build)
			{
				AppContext.SetSwitch("Builder", true);
			}
			else
			{
				AppContext.SetSwitch("Runtime", true);
			}

			// To customize application configuration such as set high DPI settings or default font,
			// see https://aka.ms/applicationconfiguration.
			ApplicationConfiguration.Initialize();

			var form = new Form1(args);
			form.FormClosed += Form_FormClosed;
			Application.Run(form);

		}

		private static void Form_FormClosed(object? sender, FormClosedEventArgs e)
		{
			Application.Exit();
		}
	}
}