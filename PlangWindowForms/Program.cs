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
			var debug = args.FirstOrDefault(p => p == "--debug") != null;
			if (debug && !Debugger.IsAttached)
			{
				Debugger.Launch();
			}

			// To customize application configuration such as set high DPI settings or default font,
			// see https://aka.ms/applicationconfiguration.
			ApplicationConfiguration.Initialize();
			Application.Run(new Form1(args));
		}
	}
}