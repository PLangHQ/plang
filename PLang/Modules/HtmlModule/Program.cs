using System.ComponentModel;

namespace PLang.Modules.HtmlModule
{
	[Description("Takes any user command and tries to convert it to html")]
	public class Program : BaseProgram
	{
		public Program() : base()
		{
		}

		public async Task<string> RenderHtml(string html)
		{
			html = variableHelper.LoadVariables(html).ToString();

			if (!string.IsNullOrEmpty(html))
			{
				context.TryGetValue("__HTML__", out object? responseHtml);
				if (responseHtml != null && responseHtml.ToString().Contains($"{{step{goalStep.Number}}}"))
				{
					responseHtml = responseHtml.ToString().Replace($"{{step{goalStep.Number}}}", html);
				} else
				{
					responseHtml = responseHtml + "\n" + html.ToString();

				}

				context.AddOrReplace("__HTML__", responseHtml);
			}
			return html;
		}

	}

}

