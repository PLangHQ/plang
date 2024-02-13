using LightInject;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using PLangWindowForms;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using System.Text;

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
		public Form1(string[] args)
		{
			debug = args.FirstOrDefault(p => p == "--debug") != null;
			this.args = args;
			container = new ServiceContainer();
			container.RegisterForPLangWindowApp(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString(), new AskUserDialog(), new ErrorDialog(), RenderContent);

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

			await pLang.Execute(args);



		}

		bool initialLoad = false;
		private void RenderContent(string html)
		{
			var outputStream = container.GetInstance<IOutputStream>();
			var stream = outputStream.Stream;
			var errorStream = outputStream.ErrorStream;
			if (html == "Loading...")
			{
				if (initialLoad) return;
				initialLoad = true;
			}
			var strVariables = "";
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

			webView.CoreWebView2.NavigationCompleted += async (object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e) =>
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
					var result = await llmService.Query<string>(llmRequest);
					await webView.CoreWebView2.ExecuteScriptAsync($"console.info('Help:\\n\\n{EscapeChars(result)}');");
				}
			};
			webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
			webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
			webView.CoreWebView2.ContentLoading += CoreWebView2_ContentLoading;
			webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
			webView.CoreWebView2.NavigateToString(html);
			webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
			webView.CoreWebView2.WebResourceRequested += (sender, args) =>
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
			};
			outputStream.Stream.Position = 0;
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