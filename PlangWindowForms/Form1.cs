using LightInject;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang;
using PLang.Building.Model;
using PLang.Container;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.UiModule;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using PLangWindowForms;
using RazorEngineCore;
using Sprache;
using System.Diagnostics;
using System.Dynamic;
using System.IO.Abstractions;
using System.Reflection;
using System.Resources;
using System.Text;
using Websocket.Client.Logging;
using static PLang.Executor;
using static System.Net.Mime.MediaTypeNames;

namespace PlangWindowForms
{


	public partial class Form1 : Form, IForm
	{
		bool debug = false;
		ServiceContainer container;
		IEngine engine;
		IPLangFileSystem fileSystem;
		Executor pLang;
		private string[] args;
		public SynchronizationContext SynchronizationContext { get; set; }
		public Form1(string[] args)
		{

			AppContext.TryGetSwitch(ReservedKeywords.Debug, out debug);
			if (!debug)
			{
				AppContext.TryGetSwitch(ReservedKeywords.CSharpDebug, out debug);
			}
			AppContext.TryGetSwitch(ReservedKeywords.Debug, out debug);
			SynchronizationContext = SynchronizationContext.Current;
			this.args = args;
			container = new ServiceContainer();
			this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
			container.RegisterForPLangWindowApp(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString(),
				new AskUserDialog(), new ErrorDialog(), this);

			fileSystem = container.GetInstance<IPLangFileSystem>();
			pLang = new Executor(container);

			InitializeComponent();
			InitializeWebView();

		}

		private void InitializeWebView()
		{
			this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
			this.webView.Dock = System.Windows.Forms.DockStyle.Fill;
			//this.webView.Source = new Uri("about:blank");
			this.webView.NavigationStarting += WebView_NavigationStarting;
			this.Controls.Add(this.webView);

			this.AllowDrop = true;
			this.DragEnter += new DragEventHandler(Form_DragEnter);
			SetInitialHtmlContent();

			

			
		}
		private void Form_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Effect = DragDropEffects.Move;
			else
				e.Effect = DragDropEffects.None;
		}

		IPseudoRuntime pseudoRuntime;
		private async Task SetInitialHtmlContent()
		{
			var task = webView.EnsureCoreWebView2Async();
			await task;

			if (task.Exception != null)
			{
				Console.WriteLine(task.Exception);
			}
			webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
			webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
			webView.CoreWebView2.ContentLoading += CoreWebView2_ContentLoading;
			webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
			webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
			webView.CoreWebView2.WebResourceRequested += (sender, args) =>
			{
				HandleResourcesRequests(args);
			};

			this.webView.CoreWebView2.WebMessageReceived += async (object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e) =>
			{
				await CoreWebView2_WebMessageReceived(sender, e);
			};
			if (debug)
			{
				pLang.SetupDebug();
			}
			var context = container.GetInstance<PLangAppContext>();
			context.AddOrReplace("__WindowApp__", this);

			engine = container.GetInstance<IEngine>();

			await pLang.Execute(args, ExecuteType.Runtime);
		}

		StringBuilder sb = new StringBuilder();
		public async Task BufferContent(object? obj)
		{
			if (obj == null) return;

			var content = await CompileAndRun(obj);

			sb.Append(content);
		}

		public async Task<string> CompileAndRun(object? obj)
		{
			var memoryStack = container.GetInstance<MemoryStack>();
			var expandoObject = new ExpandoObject() as IDictionary<string, object>;
			foreach (var kvp in memoryStack.GetMemoryStack())
			{
				expandoObject.Add(kvp.Key, kvp.Value.Value);
			}
			IRazorEngineCompiledTemplate compiled = null;

			var razorEngine = container.GetInstance<IRazorEngine>();
			compiled = await razorEngine.CompileAsync("@using PLang.Modules.UiModule\n\n" + obj.ToString(), (compileOptions) =>
			{
				compileOptions.Options.IncludeDebuggingInfo = true;
				compileOptions.AddAssemblyReference(typeof(Html).Assembly);
			});
			var content = compiled.Run(expandoObject as dynamic);
			return content;
		}

		public async Task Flush()
		{
			var html = await GetFrame();
			if (html == null) return;

			webView.CoreWebView2.NavigateToString(html);
		}
		

		public async Task<string> GetFrame()
		{
			if (sb.Length == 0) return null;

			string html = $@"
<!DOCTYPE html>
<html lang=""en"">

<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title></title>
	<link href=""local:ui/bootstrap.min.css"" rel=""stylesheet"">
	<link href=""local:ui/fontawesome.min.css"" rel=""stylesheet"">
	<script src=""local:ui/bootstrap.bundle.min.js""></script>
	<script src=""local:ui/fontawesome.min.js""></script>
	<script>
		function callGoal(goalName, args) {{
			console.log(args);
			window.chrome.webview.postMessage({{GoalName:goalName, args:args}});
		}}

		function showNotification(message) {{
			var notification = document.getElementById('notification');
			notification.textContent = message;
			notification.style.display = 'block';
        
			// Hide after 5 seconds
			setTimeout(function() {{
				notification.style.display = 'none';
			}}, 5000);
		}}
	</script>
	<style>body{{margin:2rem;}}
		.notification {{position: fixed;
            top: 20px;
            right: 20px;
            display: none; /* Hide by default */
            z-index: 1050; /* Ensure it appears above other elements */
        }}</style>
</head>

<body>
";
			html += sb.ToString();
			sb.Clear();
			html += "<div id=\"notification\" class=\"notification alert alert-success\" role=\"alert\"></div></body></html>";
			return html;
		}

		public async Task AppendContent(string cssSelector, string? content)
		{
			string escapedHtmlContent = JsonConvert.ToString(content);
			string script = $@"
        var targetElement = document.querySelector('{cssSelector}');
        if (targetElement) {{
            targetElement.innerHTML += {escapedHtmlContent};
        }}
    ";
			SynchronizationContext.Post(async _ =>
			{
				//var task = webView.EnsureCoreWebView2Async();
				//task.Wait();
				await webView.CoreWebView2.ExecuteScriptAsync(script);
			}, null);

		}

		public async Task ExecuteCode(string content)
		{
			SynchronizationContext.Post(async _ =>
			{
				await webView.CoreWebView2.ExecuteScriptAsync(content);
			}, null);

		}

		bool initialLoad = false;
		private void RenderContent(string html)
		{
			var outputStreamFactory = container.GetInstance<IOutputStreamFactory>();
			var outputStream = outputStreamFactory.CreateHandler();

			var stream = outputStream.Stream;
			var errorStream = outputStream.ErrorStream;
			if (html == "Loading...")
			{
				if (initialLoad) return;
				initialLoad = true;
			}

			(string strVariables, string errorConsole) = ExtractErrorMessages(errorStream);

			webView.CoreWebView2.NavigationCompleted += async (object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e) =>
			{
				await HandleErrors(strVariables, errorConsole);
			};
			webView.CoreWebView2.OpenDevToolsWindow();
			webView.CoreWebView2.ExecuteScriptAsync($"console.error('{EscapeChars(html)}');");

			outputStream.Stream.Position = 0;
		}

		private (string strVariables, string errorConsole) ExtractErrorMessages(Stream errorStream)
		{
			string strVariables = "";
			string errorConsole = "";
			if (errorStream != null)
			{
				errorStream.Position = 0;

				using (StreamReader reader = new StreamReader(errorStream, Encoding.UTF8, leaveOpen: true))
				{
					errorConsole = reader.ReadToEnd();
				}


				if (!string.IsNullOrEmpty(errorConsole))
				{
					var variables = engine.GetMemoryStack().GetMemoryStack();

					foreach (var variable in variables)
					{
						strVariables += "\\t" + variable.Key + ":" + JsonConvert.SerializeObject(variable.Value.Value) + "\\n";
					}
				}

			}
			return (strVariables, errorConsole);
		}

		private void HandleResourcesRequests(CoreWebView2WebResourceRequestedEventArgs args)
		{
			var resourceType = args.ResourceContext;
			if (args.Request.Uri.StartsWith("local:ui/"))
			{
				string fileName = args.Request.Uri.Replace("local:", "");
				if (fileSystem.File.Exists(fileName))
				{
					var fs = fileSystem.File.Open(fileName, FileMode.Open, FileAccess.Read);

					fs.Position = 0;
					string mimeType = (Path.GetExtension(fileName) == ".css") ? "text/css" : "application/javascript";
					args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(fs, 200, "Ok", $"Content-Type: {mimeType}");

					fileStreams.Add(fs);
					return;

				}
			}


				// Allow only image, video, and audio requests
				if (resourceType != CoreWebView2WebResourceContext.Image &&
				resourceType != CoreWebView2WebResourceContext.Media)
			{
				args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Blocked", "");
			}
		}

		private async Task HandleErrors(string strVariables, string errorConsole)
		{
			if (string.IsNullOrEmpty(errorConsole)) return;
			if (debug)
			{
				webView.CoreWebView2.OpenDevToolsWindow();
			}

			await webView.CoreWebView2.ExecuteScriptAsync($"console.error('{EscapeChars(errorConsole)}');");
			await webView.CoreWebView2.ExecuteScriptAsync($"console.info('Available Variables:\\n\\n{EscapeChars(strVariables)}');");
			AppContext.TryGetSwitch("llmerror", out bool llmError);
			if (llmError)
			{
				string system = @"You are c# expert developer debugging an error. You job is to identify why an error occured that user provides. 

Model variable is an ExpandoObject. 
Objects in Model are Json serialized for you, it maps to an object, e.g. 
Model.address = {""street"":""Mainstreet"", zip:123} is an object that can be accessed by Model.address.street and Model.address.zip

The Razor template is generated using plang programming language. User will provide source code of ```plang. Suggest changes to the plang source file since the user is programming in plang programming language. User will not know what Razor is, so keep your facts to plang. Razor code is generated automatically by plang builder, it is not in the control of the user.  
The ""Model."" object is not visible in plang, dont mention it in your output. If error is in Model.name, then reference it as %name%

Be straight to the point, point out the most obvious reason and how to fix in plang source code. 
Be Concise";
				string question = $@"I am getting this error:{errorConsole}

These variables are available:
{strVariables}
";
				var promptMessage = new List<LlmMessage>();
				promptMessage.Add(new LlmMessage("system", system));
				promptMessage.Add(new LlmMessage("user", question));

				var llmRequest = new LlmRequest("UIError", promptMessage);
				llmRequest.llmResponseType = "text";

				await webView.CoreWebView2.ExecuteScriptAsync($"console.info('Analyzing error... will be back with more info in few seconds....');");
				var llmService = container.GetInstance<ILlmService>();
				(var result, var queryError) = await llmService.Query<string>(llmRequest);

				await webView.CoreWebView2.ExecuteScriptAsync($"console.info('Help:\\n\\n{EscapeChars(result)}');");
			}
		}

		List<FileSystemStream> fileStreams = new List<FileSystemStream>();

		private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
		{
			foreach (var fs in fileStreams)
			{
				fs.Close();
			}
			fileStreams.Clear();
		}

		private void CoreWebView2_ContentLoading(object? sender, CoreWebView2ContentLoadingEventArgs e)
		{
			int i = 0;
		}

		private void CoreWebView2_WebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
		{
			int i = 0;
		}

		private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
		{
			int i = 0;
		}

		private string EscapeChars(string? content)
		{
			if (content == null) return "";

			return content.Replace("\\", "\\\\") // Escape backslashes
								.Replace("'", "\\'")   // Escape single quotes
								.Replace("\n", "\\n")  // Escape new lines
								.Replace("\r", "\\r");
		}


		private async Task CoreWebView2_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
		{
			var receivedMessage = e.WebMessageAsJson;
			var dynamic = JsonConvert.DeserializeObject<dynamic>(receivedMessage);

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
				var pseudoRuntime = container.GetInstance<IPseudoRuntime>();
				await pseudoRuntime.RunGoal(engine, engine.GetContext(), "", dynamic.GoalName.ToString(), parameters);

			}
			catch (Exception ex)
			{
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

		public void SetSize(int width, int height)
		{
			this.Size = new Size(width, height);

		}

		public void SetIcon(string? iconPath)
		{
			if (iconPath == null) return;
			this.Icon = Icon.ExtractAssociatedIcon(iconPath);
		}

		public void SetTitle(string? title)
		{
			if (title == null) return;
			this.Text = title;

		}


	}
}