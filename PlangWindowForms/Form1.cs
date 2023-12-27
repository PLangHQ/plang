using LightInject;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang;
using PLang.Exceptions.AskUser;
using PLang.Runtime;
using PLang.Utils;
using PLangWindowForms;
using RazorEngineCore;
using System.Diagnostics;
using System.Dynamic;

namespace PlangWindowForms
{
	public partial class Form1 : Form
	{
		bool debug = false;
		ServiceContainer container;
		public Form1(string[] args)
		{
			debug = args.FirstOrDefault(p => p == "--debug") != null;
			if (debug && !Debugger.IsAttached)
			{
				Debugger.Launch();
			}
			container = new ServiceContainer();
			container.RegisterForPLangConsole(Environment.CurrentDirectory, "\\");

			InitializeComponent();
			InitializeWebView();

		}
		Executor pLang;
		private void InitializeWebView()
		{
			this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
			this.webView.Dock = System.Windows.Forms.DockStyle.Fill;
			//this.webView.Source = new Uri("about:blank");
			this.webView.NavigationStarting += WebView_NavigationStarting;



			this.Controls.Add(this.webView);

			SetInitialHtmlContent();
		}
		public async Task<string> RenderTemplate(IEngine engine, string template)
		{
			var context = engine.GetContext();

			var memoryStack = engine.GetMemoryStack();


			var expandoObject = new ExpandoObject() as IDictionary<string, object>;
			foreach (var kvp in memoryStack.GetMemoryStack())
			{
				expandoObject.Add(kvp.Key, kvp.Value.Value);
			}

			try
			{
				IRazorEngine razorEngine = new RazorEngineCore.RazorEngine();
				IRazorEngineCompiledTemplate compiled = razorEngine.Compile(template);

				return compiled.Run(expandoObject as dynamic);

			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
				Console.WriteLine(ex);
				throw;
			}
		}
		private async Task SetInitialHtmlContent()
		{
			pLang = new Executor();

			await webView.EnsureCoreWebView2Async();
			this.webView.CoreWebView2.WebMessageReceived += async (object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e) =>
			{
				await CoreWebView2_WebMessageReceived(sender, e);
			};
			var engine = await pLang.Run();
			if (debug)
			{
				pLang.SetupDebug();
			}

			await RenderContent(engine);


		}

		private async Task RenderContent(IEngine engine)
		{
			engine.GetContext().TryGetValue("__HTML__", out object? content);
			engine.GetContext().Remove("__HTML__");
			if (content != null)
			{
				content = await RenderTemplate(engine, content.ToString());

				string html = $@"<!DOCTYPE html>
<html lang=""en"">

<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title></title>
	<link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css"" rel=""stylesheet"" integrity=""sha384-1BmE4kWBq78iYhFldvKuhfTAU6auU8tT94WrHftjDbrCEXSU1oBoqyl2QvZ6jIW3"" crossorigin=""anonymous"">
	<script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js"" integrity=""sha384-ka7Sk0Gln4gmtz2MlQnikT1wXgYsOg+OMhuP+IlRH9sENBO0LRn5q+8nbTov4+1p"" crossorigin=""anonymous""></script>
<script src=""https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@5.15.0/js/all.min.js""></script>
<link href=""https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@5.15.0/css/fontawesome.min.css"" rel=""stylesheet"">
<script>
		function callGoal(goalName, args) {{
console.log(args);
			window.chrome.webview.postMessage({{GoalName:goalName, args:args}});
		}}
</script>
<style>body{{margin:2rem;}}</style>
</head>

<body>
	{content}
</body>
</html>";

				webView.CoreWebView2.NavigateToString(html);
			}
		}

		private async Task CoreWebView2_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
		{
			var receivedMessage = e.WebMessageAsJson;
			var dynamic = JsonConvert.DeserializeObject<dynamic>(receivedMessage);
			var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
			var engine = container.GetInstance<IEngine>();
			engine.Init(container);

			var parameters = new Dictionary<string, object>();
			if (dynamic.args is JObject)
			{
				parameters = ((JObject)dynamic.args).ToObject<Dictionary<string, object>>();
			}
			else if (dynamic.args is JArray)
			{

			}
			try
			{
				await pseudoRuntime.RunGoal(engine, engine.GetContext(), "", dynamic.GoalName.ToString(), parameters);

				await RenderContent(engine);
			} catch (AskUserException ex)
			{
				//This does not work.
				//The idea here is that AskUserException is thrown and a dialog show with option that the Exception defines. 
				// e.g. Folder access, it could show Yes, Yes(always), No, Never, the user might be able to type in, 'Yes, for 10 days'
				AskUserDialog.ShowDialog(ex.Message, "Ask User");
			} catch (Exception ex) {
				MessageBox.Show(ex.Message + "\n\n" + ex.ToString());
			}
		}
		private void WebView_NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
		{
			if (e.Uri.StartsWith("app://"))
			{
				e.Cancel = true;

				if (e.Uri == "app://buttonclicked")
				{
					MessageBox.Show("Button Clicked", "You clicked the button in the WebView2!", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
		}

	}
}