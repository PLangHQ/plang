using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PLang.Modules.WebCrawlerModule
{
	[Description("Run a browser instance, browse a website, input values and click on html elements, sendkeys, wait for browser and extract content")]
	public class Program : BaseProgram, IDisposable
	{

		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;
		private readonly IEngine engine;
		private readonly IPseudoRuntime runtime;
		private readonly string PlayWrightContextKey = "!PlayWrightContextKey";
		private readonly string BrowserContextKey = "!BrowserContextKey";

		private readonly string RequestContextKey = "!RequestContextKey";
		private readonly string ResponseContextKey = "!ResponseContextKey";



		private readonly string PageContextKeyIndex = "!PageContextKeyIndex";
		private readonly string CurrentPageContextKey = "!PageContextKey";
		private readonly string DialogContextKey = "!DialogContextKey";
		private readonly string BrowserStartPropertiesContextKey = "!BrowserStartPropertiesContextKey";

		public Program(PLangAppContext context, IPLangFileSystem fileSystem, ILogger logger, IEngine engine, IPseudoRuntime runtime) : base()
		{
			this.context = context;
			this.fileSystem = fileSystem;
			this.logger = logger;
			this.engine = engine;
			this.runtime = runtime;
		}

		private string GetChromeUserDataDir()
		{
			string userDataDir = "";
			string userName = Environment.UserName;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				userDataDir = $@"C:\Users\{userName}\AppData\Local\Google\Chrome\User Data";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				userDataDir = $@"/Users/{userName}/Library/Application Support/Google/Chrome";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				userDataDir = $@"/home/{userName}/.config/google-chrome";
			}

			return userDataDir;
		}

		[Description("browserType=Chrome|Edge|Firefox|Safari. hideTestingMode tries to disguise that it is a bot.")]
		public async Task<IBrowserContext> StartBrowser(string browserType = "Chrome", bool headless = false, string profileName = "",
			bool kioskMode = false, Dictionary<string, string>? argumentOptions = null, int? timoutInSeconds = 30, bool hideTestingMode = false,
			GoalToCall? onRequest = null, GoalToCall? onResponse = null)
		{
			var playwright = await Playwright.CreateAsync();
			var browser = await GetBrowserType(playwright, browserType, headless, profileName, kioskMode, argumentOptions, hideTestingMode);

			browser.SetDefaultTimeout((timoutInSeconds ?? 30) * 1000);
			if (onRequest != null)
			{
				browser.Request += async (object? sender, IRequest e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onRequest, new Dictionary<string, object?> { { "!sender", sender }, { "!Request", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}
			
			if (onResponse != null)
			{
				browser.Response += async (object? sender, IResponse e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onResponse, new Dictionary<string, object?> { { "!sender", sender }, { "!Response", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}
			browser.RequestFailed += (object? sender, IRequest e) =>
			{
				context.AddOrReplace(RequestContextKey, e);
			};

			context.TryAdd(PlayWrightContextKey, playwright);
			context.TryAdd(BrowserContextKey, browser);

			return browser;
		}

		private async Task<IBrowserContext> GetBrowserType(IPlaywright playwright, string browserType, bool headless,
			string profileName, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			switch (browserType)
			{
				case "Edge":
					throw new NotImplementedException("Not implemented for Safari. You can help building plang https://github.com/PLangHQ");
				case "Firefox":
					throw new NotImplementedException("Not implemented for Firefox. You can help building plang https://github.com/PLangHQ");
				case "Safari":
					throw new NotImplementedException("Not implemented for Safari. You can help building plang https://github.com/PLangHQ");
				default:
					return await GetChromeDriver(playwright, headless, profileName, kioskMode, argumentOptions, hideTestingMode);
			}
		}


		private async Task<IBrowserContext> GetBrowser(string browserType = "Chrome", bool headless = false, bool useUserSession = false, string userSessionPath = "",
			bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null, int? timeoutInSeconds = null)
		{
			var browser = context[BrowserContextKey] as IBrowserContext;
			if (browser != null) return browser;

			logger.LogDebug("Key BrowserContextKey not existing. Starting browser");
			browser = await StartBrowser(browserType, headless, userSessionPath, kioskMode, argumentOptions, timeoutInSeconds);
			
			//browser.
			return browser;
		}


		public async Task<(PlangWebElement?, IError?)> GetElement(string? cssSelector = null)
		{
			var result = await GetWebElement(cssSelector);
			if (result.Error != null) return (null, result.Error);

			return (await GetPlangWebElement(result.Element), null);
		}

		private async Task<(IElementHandle? Element, IError? Error)> GetWebElement(string? cssSelector = null, string? shadowDomCssSelector = null)
		{
			if (cssSelector == null) cssSelector = await GetCssSelector();

			var page = await GetCurrentPage();
			var element = await page.QuerySelectorAsync(cssSelector);
			if (element != null)
			{
				SetCssSelector(cssSelector);

				return (element, null);
			}
			return (null, new ProgramError($"Element {cssSelector} does not exist.", goalStep, function));
		}

		public async Task CloseBrowser()
		{

			if (context.ContainsKey(BrowserContextKey))
			{
				var browser = context[BrowserContextKey] as IBrowserContext;
				if (browser != null)
				{
					try
					{
						await browser.CloseAsync();
					}
					catch { }
				}
			}
			if (context.ContainsKey(BrowserContextKey))
			{
				var playwright = context[PlayWrightContextKey] as IPlaywright;
				if (playwright != null)
				{
					try
					{
						playwright.Dispose();
					}
					catch { }
				}
			}
			context.Remove(PlayWrightContextKey);
			context.Remove(BrowserContextKey);
			context.Remove(PageContextKeyIndex);
			context.Remove(CurrentPageContextKey);
		}

		public void Dispose()
		{
			CloseBrowser().Wait();
		}

		public async Task WaitForUrl(string expectedUrl, int timeoutInSeconds)
		{
			var page = await GetCurrentPage();
			await page.WaitForURLAsync(expectedUrl, new PageWaitForURLOptions() { Timeout = timeoutInSeconds * 1000 });
		}

		private async Task<IPage> GetCurrentPage(string? url = null, int idx = -1)
		{
			IPage? page = null;
			if (url == null && idx == -1 && context.ContainsKey(CurrentPageContextKey))
			{
				page = context[CurrentPageContextKey] as IPage;
				if (page != null) return page;
			}

			if (idx == -1 && context.ContainsKey(PageContextKeyIndex))
			{
				idx = context[PageContextKeyIndex] as int? ?? -1;
			}

			var browser = await GetBrowser();
			if (idx > browser.Pages.Count - 1)
			{
				idx = browser.Pages.Count - 1;
			}
			if (idx == -1 && browser.Pages.Count > 0)
			{
				idx = 0;
			}


			if (idx == -1)
			{
				page = await browser.NewPageAsync();
				if (url == null) url = "about:blank";
				var response = await page.GotoAsync(url);
			}
			else
			{
				page = browser.Pages[idx];
				if (url != null)
				{
					await page.GotoAsync(url);
				}
			}
			page.Dialog += async (object? sender, IDialog e) =>
			{
				List<IDialog> dialogs;
				if (context.ContainsKey(DialogContextKey))
				{
					dialogs = context[DialogContextKey] as List<IDialog> ?? new();
				}
				else
				{
					dialogs = new List<IDialog>();
				}
				dialogs.Add(e);
			};
			context.AddOrReplace(PageContextKeyIndex, idx);
			return page;
		}


		[Description("opens a page to a url. browserType=Chrome|Edge|Firefox|IE|Safari. hideTestingMode tries to disguise that it is a bot.")]
		public async Task NavigateToUrl(string url, string browserType = "Chrome", bool headless = false,
				string profileName = "", bool kioskMode = false, Dictionary<string, string>? argumentOptions = null,
				int? timeoutInSecods = null, bool hideTestingMode = false, 
				GoalToCall? onRequest = null, GoalToCall? onResponse = null, GoalToCall? onWebsocketReceived = null, GoalToCall? onWebsocketSent = null,
				GoalToCall? onConsoleOutput = null, GoalToCall? onWorker = null, GoalToCall? onCrash = null, 
				GoalToCall? onDialog = null, GoalToCall? onLoad = null, GoalToCall? onDOMLoad = null, GoalToCall? onFileChooser = null,
				GoalToCall? onIFrameLoad = null, GoalToCall? onDownload = null)
		{
			if (string.IsNullOrEmpty(url))
			{
				throw new RuntimeException("url cannot be empty");
			}


			if (!url.StartsWith("http"))
			{
				url = "https://" + url;
			}
			if (!context.ContainsKey(PlayWrightContextKey))
			{
				await StartBrowser(browserType, headless, profileName, kioskMode, argumentOptions, timeoutInSecods, hideTestingMode);
			}
			var page = await GetCurrentPage(url);
			BindEventsToPage(page, onRequest, onResponse, onWebsocketReceived, onWebsocketSent,
				 onConsoleOutput, onWorker, onCrash,
				 onDialog, onLoad, onDOMLoad, onFileChooser,
				 onIFrameLoad, onDownload);
			
		}

		public async Task ScrollToBottom()
		{
			var page = await GetCurrentPage();
			await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight);");
		}
		public async Task ScrollToElementByCssSelector(string cssSelector)
		{
			var page = await GetCurrentPage();
			var element = page.QuerySelectorAsync(cssSelector);
			await page.EvaluateAsync("(element) => { element.scrollIntoView(true);", element);
		}

		public async Task ScrollToElement(PlangWebElement element)
		{
			var page = await GetCurrentPage();
			await page.EvaluateAsync("(element) => { element.scrollIntoView(true);", element.WebElement);
		}

		[Description("operatorOnText can be equals|contains|startswith|endswith")]
		public async Task<PlangWebElement?> GetElementByText(string text, string operatorOnText = "equals", int? timeoutInSeconds = null, string? cssSelector = null)
		{
			var page = await GetCurrentPage();

			string escapedText = Regex.Escape(text);
			string pattern = operatorOnText switch
			{
				"equals" => $"^{escapedText}$",
				"startswith" => $"^{escapedText}",
				"endswith" => $"{escapedText}$",
				_ => escapedText
			};

			var locator = page.Locator($"text=/{pattern}/i");
			await locator.First.WaitForAsync();
			var element = await locator.First.ElementHandleAsync();

			if (element != null) return await GetPlangWebElement(element);
			return null;
		}


		public async Task WaitForElementToAppear(string cssSelector, int timeoutInSeconds = 30, bool waitForElementToChange = false)
		{
			var timeoutMs = timeoutInSeconds * 1000;
			var page = await GetCurrentPage();

			var locator = page.Locator(cssSelector);
			await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeoutMs });

			if (waitForElementToChange)
			{
				// Get the original element content
				var originalContent = await locator.InnerHTMLAsync();

				// Wait for the element content to change
				await page.WaitForFunctionAsync(
					$"selector => document.querySelector(selector)?.innerHTML !== {EscapeJsString(originalContent)}",
					cssSelector,
					new PageWaitForFunctionOptions { Timeout = timeoutMs }
				);
			}
		}

		private static string EscapeJsString(string value)
		{
			return value.Replace("'", "\\'").Replace("\"", "\\\"");
		}
	
		public async Task SetFocus(string? cssSelector = null, int? timoutInSeconds = null)
		{
			var page = await GetCurrentPage();
			cssSelector = await GetCssSelector(cssSelector);

			await page.FocusAsync(cssSelector, new PageFocusOptions() { Timeout = timoutInSeconds * 1000 });
		}

		public async Task ClickOnElement(PlangWebElement element)
		{
			await element.WebElement.ClickAsync();
		}

		public async Task Click(string cssSelector, int elementAtToClick = 0, bool clickAllMatchingElements = false, int? timeoutInSeconds = null)
		{
			var page = await GetCurrentPage();
			var elements = await page.QuerySelectorAllAsync(cssSelector);
			if (clickAllMatchingElements)
			{
				foreach (var element in elements)
				{
					await element.ClickAsync(new ElementHandleClickOptions() { Timeout = timeoutInSeconds * 1000 });
				}
			}
			else
			{
				var element = elements[elementAtToClick];
				await element.ClickAsync(new ElementHandleClickOptions() { Timeout = timeoutInSeconds * 1000 });
			}

		}
		/*
		 * 
		 **/
		public async Task<string?> AcceptAlert()
		{
			var page = await GetCurrentPage();
			List<IDialog>? dialogs = null;
			if (context.ContainsKey(DialogContextKey))
			{
				dialogs = context[DialogContextKey] as List<IDialog>;
			}
			if (dialogs == null) return null;

			string message = "";
			foreach (var dialog in dialogs)
			{
				message = dialog.Message;
				await dialog.AcceptAsync();
			}
			return message;
		}



		private async Task<string> GetCssSelector(string? cssSelector = null)
		{
			if (!string.IsNullOrEmpty(cssSelector)) return cssSelector;

			if (string.IsNullOrEmpty(cssSelector) && context.ContainsKey("prevCssSelector"))
			{
				cssSelector = context["prevCssSelector"]?.ToString();
			}

			if (cssSelector == null)
			{
				cssSelector = "body";
			}

			return cssSelector;
		}
		private void SetCssSelector(string? cssSelector)
		{
			context.AddOrReplace("prevCssSelector", cssSelector);
		}

		[Description("Writes a text to an element")]
		public async Task SendKey(string value, string? cssSelector = null, int? timeoutInSeconds = null, bool humanStyle = false)
		{
			var page = await GetCurrentPage();
			cssSelector = await GetCssSelector();

			var element = await page.QuerySelectorAsync(cssSelector);
			if (element == null) return;

			if (humanStyle)
			{
				await element.TypeAsync(value, new ElementHandleTypeOptions() { Timeout = timeoutInSeconds * 1000 });
			}
			else
			{
				await element.FillAsync(value, new ElementHandleFillOptions() { Timeout = timeoutInSeconds * 1000 });
			}
		}

		[Description("set the text of an element other than input by cssSelector")]
		public async Task SetTextOnElement(string text, string? cssSelector = null, int? timeoutInSeconds = null, bool clearElementFirst = false)
		{
			await SendKey(text, cssSelector, timeoutInSeconds);
		}

		[Description("set the value of an input by cssSelector")]
		public async Task Input(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			await SendKey(value, cssSelector, timeoutInSeconds);
		}

		[Description("select an option by its value in select input by cssSelector")]
		public async Task SelectByValue(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			var page = await GetCurrentPage();
			cssSelector = await GetCssSelector();

			var element = await page.QuerySelectorAsync(cssSelector);
			if (element == null) return;

			await element.SelectOptionAsync(new SelectOptionValue() { Value = value });
			SetCssSelector(cssSelector);
		}

		[Description("select an option by its text in select input by cssSelector")]
		public async Task SelectByText(string text, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			var page = await GetCurrentPage();
			cssSelector = await GetCssSelector();

			var element = await page.QuerySelectorAsync(cssSelector);
			if (element == null) return;

			await element.SelectOptionAsync(new SelectOptionValue() { Label = text });
			SetCssSelector(cssSelector);
		}

		public async Task Submit(string? cssSelector = null, int? timeoutInSeconds = null)
		{
			var page = await GetCurrentPage();
			cssSelector = await GetCssSelector();

			var element = await page.QuerySelectorAsync(cssSelector);
			if (element == null) return;

			await element.ClickAsync();

			SetCssSelector(cssSelector);
		}
		private async Task<List<PlangWebElement>> GetPlangWebElements(IReadOnlyList<IElementHandle> elements)
		{
			List<PlangWebElement> plangElements = new();
			foreach (var element in elements)
			{
				var e = await GetPlangWebElement(element);
				plangElements.Add(e);
			}
			return plangElements;
		}

		private async Task<PlangWebElement?> GetPlangWebElement(IElementHandle? element)
		{
			if (element == null) return null;
			var plangWebElement = new PlangWebElement();
			plangWebElement.Text = await element.InnerTextAsync();

			plangWebElement.TagName = (await element.EvaluateAsync<string>("el => el.tagName")).ToLower();
			plangWebElement.WebElement = element;
			plangWebElement.InnerHtml = await element.InnerHTMLAsync();

			return plangWebElement;
		}



		public async Task<List<PlangWebElement>> GetElements(string? cssSelector = null, string? shadowDomCssSelector = null)
		{
			var page = await GetCurrentPage();
			cssSelector = await GetCssSelector();

			var elements = await page.QuerySelectorAllAsync(cssSelector);
			return await GetPlangWebElements(elements);
		}

		public async Task<(List<PlangWebElement>?, IError?)> GetElementsInsideElement(string elementName, IElementHandle element)
		{
			if (element == null) return (null, new ProgramError("You must send in element to look inside", goalStep, function));

			var page = await GetCurrentPage();
			var elements = await element.QuerySelectorAllAsync(elementName);

			return (await GetPlangWebElements(elements), null);
		}
		/*
		public async Task<List<dynamic>> SerializeElements(List<List<PlangWebElement>> elementsArray)
		{
			List<dynamic> list = new List<dynamic>();
			foreach (var elements in elementsArray)
			{


				var driver = await GetDriver();
				foreach (var element in elements)
				{

					string tagName = element.TagName;

					Dictionary<string, string> attributes = GetAllAttributes(driver, element);

					if (tagName == "form")
					{
						var inputs = element.WebElement.FindElements(By.XPath(".//input"));
						var serializedInputs = await SerializeElements([GetPlangWebElements(inputs)]);
						var serializableElement = new
						{
							TagName = tagName,
							Attributes = attributes,
							Text = element.Text,
							Inputs = serializedInputs
						};
						list.Add(serializableElement);
					}
					else
					{
						var serializableElement = new
						{
							TagName = tagName,
							Attributes = attributes,
							Text = element.Text
						};
						list.Add(serializableElement);
					}



				}
			}
			int i = 0;
			return list;
		}*/


		public async Task<string?> FindElementAndExtractAttribute(string attribute, string? cssSelector = null, PlangWebElement? element = null)
		{

			var page = await GetCurrentPage();

			IElementHandle? e;
			if (element != null)
			{
				e = await element.WebElement.QuerySelectorAsync(cssSelector);
			}
			else
			{
				cssSelector = await GetCssSelector(cssSelector);
				e = await page.QuerySelectorAsync(cssSelector);
			}
			if (e == null) return null;

			return await e.GetAttributeAsync(attribute);

		}


		public async Task<List<string>> ExtractContent(string? cssSelector = null, PlangWebElement? element = null, string outputFormat = "html")
		{
			var page = await GetCurrentPage();

			cssSelector = await GetCssSelector(cssSelector);
			List<string> results = new List<string>();

			IReadOnlyList<IElementHandle> elements;
			if (element != null)
			{
				elements = await element.WebElement.QuerySelectorAllAsync(cssSelector);
			}
			else
			{
				elements = await page.QuerySelectorAllAsync(cssSelector);
			}

			foreach (var e in elements)
			{
				string text = (outputFormat == "text") ? await e.InnerTextAsync() : await e.InnerHTMLAsync();
				results.Add(text);
			}
			if (outputFormat == "md")
			{
				var config = new ReverseMarkdown.Config
				{
					// Include the unknown tag completely in the result (default as well)
					UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
					// generate GitHub flavoured markdown, supported for BR, PRE and table tags
					GithubFlavored = true,
					// will ignore all comments
					RemoveComments = true,
					// remove markdown output for links where appropriate
					SmartHrefHandling = true
				};
				var converter = new ReverseMarkdown.Converter(config);
				for (int i = 0; i < results.Count; i++)
				{
					string ble = converter.Convert(results[i]);
					results[i] = ble;
				}
			}

			SetCssSelector(cssSelector);
			return results;
		}
		public async Task SwitchTab(int tabIndex)
		{
			await GetCurrentPage(null, tabIndex);
		}

		[Description("type is defines which type of header to get. type:response|request|null")]
		public async Task<Dictionary<string, string>> GetHeaders(string? type = null)
		{
			return null;
		}

		private void Driver_Response(object? sender, IResponse e)
		{
			throw new NotImplementedException();
		}

		public async Task ListenToNetworkTraffic(string url, string oper)
		{
			var browser = await GetBrowser();


		}

		public async Task Wait(int milliseconds = 1000)
		{
			await Task.Delay(milliseconds);
		}

		public async Task<IError?> TakeScreenshotOfWebsite(string saveToPath, bool overwrite = false, string? cssSelector = null)
		{
			if (string.IsNullOrWhiteSpace(saveToPath))
			{
				return new ProgramError("The path where to save the screenshot cannot be empty", goalStep, function);
			}

			var absolutePath = GetPath(saveToPath);
			var folderPath = Path.GetDirectoryName(absolutePath);
			if (!fileSystem.Directory.Exists(folderPath))
			{
				fileSystem.Directory.CreateDirectory(folderPath);
			}

			if (!overwrite && fileSystem.File.Exists(absolutePath))
			{
				return new ProgramError("File exists and will not be overwritten.", goalStep, function, FixSuggestion: $"Rewrite your step to include that you want to overwrite the file, e.g. `- {goalStep.Text}, overwrite`");
			}

			var page = await GetCurrentPage();
			if (cssSelector != null)
			{
				var element = await page.QuerySelectorAsync(cssSelector);
				if (element == null)
				{
					return new ProgramError($"The element {cssSelector} coult not be found on page.", goalStep, function);
				}
				await element.ScreenshotAsync(new ElementHandleScreenshotOptions() { Path = absolutePath });
			}
			else
			{
				await page.ScreenshotAsync(new PageScreenshotOptions() { Path = absolutePath });
			}


			return null;
		}

		private string ConvertKeyCommand(string value)
		{
			if (value == "\\t") return "\t";
			if (value == "\\r") return "\r";
			if (value == "\\n") return "\n";
			return value;
		}




		private string GetChromeProfileFolder(string profileName)
		{
			string localStatePath = fileSystem.Path.Join(GetChromeUserDataDir(), "Local State");

			string localStateContent = File.ReadAllText(localStatePath);
			JObject localStateJson = JObject.Parse(localStateContent);

			var profileInfo = localStateJson["profile"]?["info_cache"];
			if (profileInfo == null)
			{
				throw new InvalidOperationException("Profile info not found in Local State file.");
			}

			foreach (JToken profile in profileInfo)
			{
				var property = profile as JProperty;
				if (property == null) continue;


				string? name = property.Value["name"]?.ToString();

				if (string.Equals(profileName, name, StringComparison.OrdinalIgnoreCase))
				{
					return property.Name;
				}
			}

			throw new InvalidOperationException("Profile info not found in Local State file.");
		}
		private BrowserTypeLaunchOptions GetChromeIcognitoOptions(bool headless, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			BrowserTypeLaunchOptions options = new BrowserTypeLaunchOptions();
			options.Headless = headless;
			List<string> args = new();
			if (hideTestingMode)
			{
				args.Add("--disable-blink-features=AutomationControlled");
			}
			if (kioskMode)
			{
				args.Add("kios");
			}
			if (hideTestingMode)
			{
				args.Add("--disable-blink-features=AutomationControlled");
				//options..UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";

			}
			if (argumentOptions != null)
			{
				foreach (var argument in argumentOptions)
				{
					args.Add(argument.Key + "=" + argument.Value);
				}
			}
			options.Args = args.ToArray();
			return options;
		}

		private BrowserTypeLaunchPersistentContextOptions GetChromeOptions(bool headless, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			var options = new BrowserTypeLaunchPersistentContextOptions();
			options.Headless = headless;

			List<string> args = new();

			if (kioskMode)
			{
				args.Add("kios");
			}
			if (argumentOptions != null)
			{
				foreach (var argument in argumentOptions)
				{
					args.Add(argument.Key + "=" + argument.Value);
				}
			}
			options.Args = args.ToArray();
			return options;
		}
		private async Task<IBrowserContext> GetChromeDriver(IPlaywright playwright, bool headless, string profileName,
			bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode, int errorCount = 0)
		{
			string? userProfile = null;

			if (profileName.Equals("default", StringComparison.OrdinalIgnoreCase))
			{
				userProfile = GetChromeUserDataDir();
				logger.LogDebug($"Using user path: {userProfile}");
			}
			else if (!string.IsNullOrEmpty(profileName))
			{
				userProfile = GetChromeProfileFolder(profileName);
				logger.LogDebug($"Using user path: {userProfile}");
			}

			var argHasUserAgent = (argumentOptions != null && argumentOptions.FirstOrDefault(p => p.Key.Equals("user-agent", StringComparison.OrdinalIgnoreCase)).Value != null);

			IBrowserContext browser;
			if (userProfile == null)
			{
				var options = GetChromeIcognitoOptions(headless, kioskMode, argumentOptions, hideTestingMode);
				try
				{
					options.DownloadsPath = fileSystem.Path.Join(fileSystem.RootDirectory, "Downloads");
					var chromium = await playwright.Chromium.LaunchAsync(options);
					var contextOptions = new BrowserNewContextOptions();
					if (hideTestingMode && !argHasUserAgent)
					{
						contextOptions.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
					}
					
					browser = await chromium.NewContextAsync(contextOptions);
					
				}
				catch (PlaywrightException pe)
				{
					if (errorCount < 2 && pe.Message.Contains("Executable doesn't exist"))
					{
						var program = GetProgramModule<PLang.Modules.TerminalModule.Program>();
						await program.RunTerminal("playwright.ps1", ["install"], pathToWorkingDirInTerminal: AppContext.BaseDirectory);

						return await GetChromeDriver(playwright, headless, profileName, kioskMode, argumentOptions, hideTestingMode, ++errorCount);
					}
					throw;
				}
			}
			else
			{
				try
				{
					var options = GetChromeOptions(headless, kioskMode, argumentOptions, hideTestingMode);
					if (hideTestingMode && !argHasUserAgent)
					{
						options.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
					}

					var path = GetChromeUserDataDir();
					options.Channel = "chrome";
					//options.Args = options.Args!.Append(@$"user-data-dir={path}");
					options.Args = options.Args!.Append($"--profile-directory={userProfile}");
					if (hideTestingMode)
					{
						options.Args = options.Args!.Append($"--disable-blink-features=AutomationControlled");
						options.Args = options.Args!.Append($"--disable-blink-features");
						options.Args = options.Args!.Append($"--disable-infobars");
					}

					browser = await playwright.Chromium.LaunchPersistentContextAsync(path, options);

				}
				catch (PlaywrightException pe)
				{
					if (pe.Message.Contains("Executable doesn't exist"))
					{
						var program = GetProgramModule<PLang.Modules.TerminalModule.Program>();
						await program.RunTerminal("playwright.ps1", ["instal"]);

						return await GetChromeDriver(playwright, headless, profileName, kioskMode, argumentOptions, hideTestingMode);
					}
					throw;
				}
			}

			if (hideTestingMode)
			{
				await browser.AddInitScriptAsync(@"Object.defineProperty(navigator, 'webdriver', {
            get: () => undefined
        });");
			}
			browser.Close += async (object? sender, IBrowserContext e) =>
			{
				await CloseBrowser();
			};
			return browser;
		}


		private void BindEventsToPage(IPage page, GoalToCall? onRequest, GoalToCall? onResponse, GoalToCall? onWebsocketReceived, GoalToCall? onWebsocketSent, GoalToCall? onConsoleOutput, GoalToCall? onWorker, GoalToCall? onCrash, GoalToCall? onDialog, GoalToCall? onLoad, GoalToCall? onDOMLoad, GoalToCall? onFileChooser, GoalToCall? onIFrameLoad, GoalToCall? onDownload)
		{
			if (onConsoleOutput != null)
			{
				page.Console += async (object? sender, IConsoleMessage e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onConsoleOutput, new Dictionary<string, object?> { { "!sender", sender }, { "!console", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}
			if (onRequest != null)
			{
				page.Request += async (object? sender, IRequest e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onRequest, new Dictionary<string, object?> { { "!sender", sender }, { "!request", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}
			if (onResponse != null)
			{
				page.Response += async (object? sender, IResponse e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onResponse, new Dictionary<string, object?> { { "!sender", sender }, { "!response", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}
			if (onWebsocketReceived != null || onWebsocketSent != null)
			{
				page.WebSocket += (object? sender, IWebSocket e) =>
				{
					if (onWebsocketReceived != null)
					{
						e.FrameReceived += async (object? sender, IWebSocketFrame e) =>
						{
							var result = await runtime.RunGoal(engine, context, "/", onWebsocketReceived, new Dictionary<string, object?> { { "!sender", sender }, { "!websocket", e } }, Goal);
							if (result.error != null)
							{
								throw new ExceptionWrapper(result.error);
							}
						};
					}
					if (onWebsocketSent != null)
					{
						e.FrameSent += async (object? sender, IWebSocketFrame e) =>
						{
							var result = await runtime.RunGoal(engine, context, "/", onWebsocketSent, new Dictionary<string, object?> { { "!sender", sender }, { "!websocket", e } }, Goal);
							if (result.error != null)
							{
								throw new ExceptionWrapper(result.error);
							}
						};
					}


				};
			}

			if (onWorker != null)
			{
				page.Worker += async (object? sender, IWorker e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onWorker, new Dictionary<string, object?> { { "!sender", sender }, { "!worker", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}

			if (onCrash != null)
			{
				page.Crash += async (object? sender, IPage e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onCrash, new Dictionary<string, object?> { { "!sender", sender }, { "!page", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}

			if (onDialog != null)
			{
				page.Dialog += async (object? sender, IDialog e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onDialog, new Dictionary<string, object?> { { "!sender", sender }, { "!dialog", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}
			if (onLoad != null)
			{
				page.Load += async (object? sender, IPage e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onLoad, new Dictionary<string, object?> { { "!sender", sender }, { "!page", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}
			if (onDOMLoad != null)
			{
				page.DOMContentLoaded += async (object? sender, IPage e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onDOMLoad, new Dictionary<string, object?> { { "!sender", sender }, { "!page", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}

			if (onFileChooser != null)
			{
				page.FileChooser += async (object? sender, IFileChooser e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onFileChooser, new Dictionary<string, object?> { { "!sender", sender }, { "!filechooser", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}
			if (onIFrameLoad != null)
			{
				page.FrameNavigated += async (object? sender, IFrame e) =>
				{
					var result = await runtime.RunGoal(engine, context, "/", onIFrameLoad, new Dictionary<string, object?> { { "!sender", sender }, { "!iframe", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}

			if (onDownload != null)
			{
				page.Download += async (object? sender, IDownload e) =>
				{

					var result = await runtime.RunGoal(engine, context, "/", onDownload, new Dictionary<string, object?> { { "!sender", sender }, { "!Download", e } }, Goal);
					if (result.error != null)
					{
						throw new ExceptionWrapper(result.error);
					}
				};
			}
		}

	}
}
