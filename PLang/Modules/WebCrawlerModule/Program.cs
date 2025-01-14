using NBitcoin.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Safari;
using OpenQA.Selenium.Support.UI;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Utils; 
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SeleniumExtras.WaitHelpers;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PLang.Errors;
using PLang.Errors.Runtime;
using System.Xml.Linq;
using System.CodeDom.Compiler;

namespace PLang.Modules.WebCrawlerModule
{
	[Description("Run a browser instance, browse a website, input values and click on html elements, sendkeys, wait for browser and extract content")]
	public class Program : BaseProgram, IDisposable
	{

		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;
		private readonly string BrowserContextKey = "!BrowserContextKey";
		private readonly string BrowserStartPropertiesContextKey = "!BrowserStartPropertiesContextKey";

		public Program(PLangAppContext context, IPLangFileSystem fileSystem, ILogger logger) : base()
		{
			this.context = context;
			this.fileSystem = fileSystem;
			this.logger = logger;
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

		[Description("browserType=Chrome|Edge|Firefox|IE|Safari. hideTestingMode tries to disguise that it is a bot.")]
		public async Task StartBrowser(string browserType = "Chrome", bool headless = false, bool useUserSession = false, string userSessionPath = "",
			bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null, int? timoutInSeconds = 30, bool hideTestingMode = false)
		{
			Dictionary<string, object?> startProperties = new();
			startProperties.Add("browserType", browserType);
			startProperties.Add("headless", headless);
			startProperties.Add("useUserSession", useUserSession);
			startProperties.Add("userSessionPath", userSessionPath);
			startProperties.Add("incognito", incognito);
			startProperties.Add("kioskMode", kioskMode);
			startProperties.Add("argumentOptions", argumentOptions);
			startProperties.Add("timoutInSeconds", timoutInSeconds);
			startProperties.Add("hideTestingMode", hideTestingMode);

			var driver = GetBrowserType(browserType, headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);

			driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(timoutInSeconds ?? 30);
			driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(timoutInSeconds ?? 30);
			driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timoutInSeconds ?? 30);

			context.TryAdd(BrowserContextKey, driver);
			context.TryAdd(BrowserStartPropertiesContextKey, startProperties);
		}

		private WebDriver GetBrowserType(string browserType, bool headless, bool useUserSession,
			string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			switch (browserType)
			{
				case "Edge":
					return GetEdgeDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);
				case "Firefox":
					return GetFirefoxDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);
				case "Safari":
					return GetSafariDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);
				default:
					return GetChromeDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, hideTestingMode);
			}
		}

		private async Task RestartBrowserInstance()
		{
			await CloseBrowser();

			var startProperties = context[BrowserStartPropertiesContextKey] as Dictionary<string, object?>;
			await StartBrowser(startProperties["browserType"] as string, (bool)startProperties["headless"], (bool)startProperties["useUserSession"], startProperties["userSessionPath"] as string,
				(bool)startProperties["incognito"], (bool)startProperties["kioskMode"], startProperties["argumentOptions"] as Dictionary<string, string>, startProperties["timoutInSeconds"] as int?,
				(bool)startProperties["hideTestingMode"]);
		}


		private async Task<WebDriver> GetDriver(string browserType = "Chrome", bool headless = false, bool useUserSession = false, string userSessionPath = "",
			bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null, int? timeoutInSeconds = null)
		{
			if (!context.ContainsKey(BrowserContextKey))
			{
				logger.LogDebug("Key SeleniumBrowser not existing. Starting browser");
				await StartBrowser(browserType, headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, timeoutInSeconds);
			}
			return context[BrowserContextKey] as WebDriver;
		}

		public async Task<PlangWebElement> GetElement(string? cssSelector = null)
		{
			return GetPlangWebElement(await GetWebElement(cssSelector));
		}

		private async Task<IWebElement> GetWebElement(string? cssSelector = null)
		{
			var driver = await GetDriver();
			if (cssSelector == null) cssSelector = await GetCssSelector();

			var element = driver.FindElement(By.CssSelector(cssSelector));


			if (element == null)
			{
				throw new Exception($"Element {cssSelector} does not exist.");
			}
			SetCssSelector(cssSelector);
			return element;
		}

		public async Task CloseBrowser()
		{
			if (!context.ContainsKey(BrowserContextKey)) return;

			var driver = context[BrowserContextKey] as WebDriver;
			if (driver == null) return;

			driver.Quit();
			context.Remove(BrowserContextKey);
		}

		public void Dispose()
		{
			CloseBrowser().Wait();
		}


		[Description("browserType=Chrome|Edge|Firefox|IE|Safari. hideTestingMode tries to disguise that it is a bot.")]
		public async Task NavigateToUrl(string url, string browserType = "Chrome", bool headless = false, bool useUserSession = false,
				string userSessionPath = "", bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null,
				string? browserConsoleOutputVariableName = null, int? timeoutInSecods = null, bool hideTestingMode = false)
		{
			if (string.IsNullOrEmpty(url))
			{
				throw new RuntimeException("url cannot be empty");
			}


			if (!url.StartsWith("http"))
			{
				url = "https://" + url;
			}

			var driver = await GetDriver(browserType, headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions, timeoutInSecods);
			driver.Navigate().GoToUrl(url);

			if (browserConsoleOutputVariableName != null)
			{
				var logs = driver.Manage().Logs.GetLog(LogType.Browser);
				memoryStack.Put(browserConsoleOutputVariableName, logs);
			}

		}

		public async Task ScrollToBottom()
		{
			var driver = await GetDriver();
			IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
			js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
		}

		public async Task ScrollToElement(PlangWebElement element)
		{
			var driver = await GetDriver();

			IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
			js.ExecuteScript("arguments[0].scrollIntoView(true);", element.WebElement);
		}

		[Description("operatorOnText can be equals|contains|startswith|endswith")]
		public async Task<PlangWebElement?> GetElementByText(string text, string operatorOnText = "equals", int? timeoutInSeconds = null, string? cssSelector = null)
		{
			ISearchContext driver = await GetDriver(timeoutInSeconds: timeoutInSeconds);
			if (cssSelector != null)
			{
				driver = driver.FindElement(By.CssSelector(cssSelector));
			}

			IWebElement? element = null;
			if (operatorOnText == "equals")
			{
				element = driver.FindElement(By.XPath($"//*[text() = '{text}']"));
			}
			if (operatorOnText == "contains")
			{
				element = driver.FindElement(By.XPath($"//*[contains(text(), '{text}')]"));
			}
			if (operatorOnText == "startswith")
			{
				element = driver.FindElement(By.XPath($"//*[starts-with(text(), '{text}')]"));

			}
			if (operatorOnText == "equals")
			{
				element = driver.FindElement(By.XPath($"//*[substring(text(), string-length(text()) - string-length('{text}') + 1) = '{text}']"));

			}

			if (element != null) return GetPlangWebElement(element);
			return null;
		}

		public async Task WaitForElementToDissapear(object elementOrCssSelector, int timeoutInSeconds)
		{
			var driver = await GetDriver();
			await SetTimeout(timeoutInSeconds);

			WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
			var element = (elementOrCssSelector is string) ? driver.FindElement(By.TagName(elementOrCssSelector.ToString())) : elementOrCssSelector as IWebElement;

			wait.Until(ExpectedConditions.StalenessOf(element));
		}

		public async Task WaitForElementToAppear(string cssSelector, int timeoutInSeconds = 30, bool waitForElementToChange = false)
		{
			var driver = await GetDriver();

			await SetTimeout(timeoutInSeconds);

			WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
			if (waitForElementToChange)
			{
				var originalBody = driver.FindElement(By.TagName(cssSelector));

				wait.Until(drv =>
				{
					try
					{
						var newBody = drv.FindElement(By.TagName(cssSelector));
						return !newBody.Equals(originalBody);
					}
					catch (NoSuchElementException)
					{
						return false;
					}
				});
			}
			else
			{
				wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector(cssSelector)));
			}

			await ResetTimeout();

		}

		TimeSpan? originalAsyncJsTimeout = null;
		TimeSpan? originalPageLoad = null;
		TimeSpan? originalImplicitWait = null;
		private async Task ResetTimeout()
		{
			if (originalAsyncJsTimeout == null) return;

			var driver = await GetDriver();
			driver.Manage().Timeouts().AsynchronousJavaScript = originalAsyncJsTimeout ?? TimeSpan.FromSeconds(30);
			driver.Manage().Timeouts().PageLoad = originalPageLoad ?? TimeSpan.FromSeconds(30);
			driver.Manage().Timeouts().ImplicitWait = originalImplicitWait ?? TimeSpan.FromSeconds(30);
		}

		public async Task SetTimeout(int? timoutInSeconds = null)
		{
			if (timoutInSeconds == null) return;

			var driver = await GetDriver();
			originalAsyncJsTimeout = driver.Manage().Timeouts().AsynchronousJavaScript;
			originalPageLoad = driver.Manage().Timeouts().PageLoad;
			originalImplicitWait = driver.Manage().Timeouts().ImplicitWait;

			driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds((int)timoutInSeconds);
			driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds((int)timoutInSeconds);
			driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds((int)timoutInSeconds);
		}

		public async Task SetFocus(string? cssSelector = null, int? timoutInSeconds = null)
		{
			var driver = await GetDriver();
			try
			{
				await SetTimeout(timoutInSeconds);
				var element = await GetWebElement(cssSelector);

				new Actions(driver).MoveToElement(element).Perform();
			}
			finally
			{
				await ResetTimeout();
			}
		}

		public async Task ClickOnElement(PlangWebElement element)
		{
			
			element.WebElement.Click();
		}

		public async Task Click(string cssSelector, int elementAtToClick = 0, bool clickAllMatchingElements = false, int? timeoutInSeconds = null)
		{
			try
			{
				var driver = await GetDriver();
				await SetTimeout(timeoutInSeconds);
				if (!clickAllMatchingElements)
				{
					var element = await GetWebElement(cssSelector);
					element.Click();
				}
				else
				{
					var elements = await GetElements(cssSelector);
					if (elementAtToClick != 0)
					{
						if (elements.Count >= elementAtToClick)
						{
							elements[elements.Count - 1].WebElement.Click();
						}
					}
					foreach (var element in elements)
					{
						element.WebElement.Click();
					}
				}
				SetCssSelector(cssSelector);
			}
			finally
			{
				await ResetTimeout();
			}
		}

		public async Task<string> AcceptAlert()
		{
			var driver = await GetDriver();
			IAlert alert = driver.SwitchTo().Alert();
			string text = alert.Text;
			alert.Accept();
			return text;
		}

		private async Task<string> GetCssSelector(string? cssSelector = null)
		{
			if (string.IsNullOrEmpty(cssSelector) && context.ContainsKey("prevCssSelector"))
			{
				cssSelector = context["prevCssSelector"].ToString();
			}

			if (cssSelector == null)
			{
				var driver = await GetDriver();
				var focusedElement = (IWebElement)((IJavaScriptExecutor)driver).ExecuteScript("return document.activeElement;");
				cssSelector = GetCssSelector(focusedElement, driver);
			}

			return cssSelector;
		}
		private void SetCssSelector(string? cssSelector)
		{
			context.AddOrReplace("prevCssSelector", cssSelector);
		}

		public async Task SendKey(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = await GetCssSelector(cssSelector);
				var element = await GetWebElement(cssSelector);
				var input = ConvertKeyCommand(value);
				element.SendKeys(input);
				SetCssSelector(cssSelector);
			}
			finally
			{
				await ResetTimeout();
			}
		}

		[Description("set the text of an element other than input by cssSelector")]
		public async Task SetTextOnElement(string text, string? cssSelector = null, int? timeoutInSeconds = null, bool clearElementFirst = true)
		{

			await SetTimeout(timeoutInSeconds);
			cssSelector = await GetCssSelector(cssSelector);
			var element = await GetWebElement(cssSelector);
			if (clearElementFirst) element.Clear();
			element.SendKeys(text);
			SetCssSelector(cssSelector);
		}

		[Description("set the value of an input by cssSelector")]
		public async Task Input(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = await GetCssSelector(cssSelector);
				var element = await GetWebElement(cssSelector);
				element.SendKeys(value);
				SetCssSelector(cssSelector);
			}
			finally
			{
				await ResetTimeout();
			}
		}

		[Description("select an option by its value in select input by cssSelector")]
		public async Task SelectByValue(string value, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = await GetCssSelector(cssSelector);
				var element = await GetWebElement(cssSelector);


				var selectElement = new SelectElement(element);
				selectElement.SelectByValue(value);
			}
			finally
			{
				await ResetTimeout();
			}

			SetCssSelector(cssSelector);
		}

		[Description("select an option by its text in select input by cssSelector")]
		public async Task SelectByText(string text, string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = await GetCssSelector(cssSelector);
				var element = await GetWebElement(cssSelector);
				var selectElement = new SelectElement(element);
				selectElement.SelectByText(text);
			}
			finally
			{
				await ResetTimeout();
			}
			SetCssSelector(cssSelector);
		}

		public async Task Submit(string? cssSelector = null, int? timeoutInSeconds = null)
		{
			try
			{
				await SetTimeout(timeoutInSeconds);
				cssSelector = await GetCssSelector(cssSelector);
				var element = await GetWebElement(cssSelector);
				element.Submit();
			}
			finally
			{
				await ResetTimeout();
			}
			SetCssSelector(cssSelector);
		}
		/*
		public async Task<List<string>> ExtractContent(string content, string cssSelector, bool clearHtml = true)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(content);
			if (cssSelector.StartsWith("."))
			{
				cssSelector = cssSelector.Replace(" ", ".");
			}
			var nodes = doc.DocumentNode.SelectNodes(cssSelector);

			List<string> strings = new List<string>();
			foreach (var node in nodes)
			{
				if (clearHtml)
				{
					strings.Add(node.InnerHtml.ClearHtml());
				}
				else
				{
					strings.Add(node.InnerHtml);
				}
			}
			return strings;
		}
		*/

		private List<PlangWebElement> GetPlangWebElements(ReadOnlyCollection<IWebElement> elements)
		{
			List<PlangWebElement> plangElements = new();
			foreach (var element in elements)
			{
				plangElements.Add(GetPlangWebElement(element));
			}
			return plangElements;	
		}

		private PlangWebElement GetPlangWebElement(IWebElement element)
		{
			var plangWebElement = new PlangWebElement();

			plangWebElement.Location = new Location() { X = element.Location.X, Y = element.Location.Y };
			plangWebElement.Size = new Size() { Height = element.Size.Height, Width = element.Size.Width };
			plangWebElement.Displayed = element.Displayed;
			plangWebElement.Enabled = element.Enabled;
			plangWebElement.Location = new Location() { X = element.Location.X, Y = element.Location.Y };
			plangWebElement.Selected = element.Selected;
			plangWebElement.Text = element.Text;
			plangWebElement.TagName = element.TagName;
			plangWebElement.WebElement = element;
			if (element is WebElement webElement)
			{
				plangWebElement.ComputedAccessibleLabel = webElement.ComputedAccessibleLabel;
				plangWebElement.ComputedAccessibleRole = webElement.ComputedAccessibleRole;
				plangWebElement.Coordinates = new Coordinates()
				{
					AuxiliaryLocator = webElement.Coordinates.AuxiliaryLocator.ToString(),
					LocationInDom = new Location() { X = webElement.Coordinates.LocationInDom.X, Y = webElement.Coordinates.LocationInDom.Y },
					LocationInViewport = new Location() { X = webElement.Coordinates.LocationInViewport.X, Y = webElement.Coordinates.LocationInViewport.Y }
				};				
			}
			
			return plangWebElement;
		}



		public async Task<List<PlangWebElement>> GetElements(string? cssSelector = null)
		{
			cssSelector = await GetCssSelector(cssSelector);
			var driver = await GetDriver();
			var elements = driver.FindElements(By.CssSelector(cssSelector));
			
			return GetPlangWebElements(elements);
		}

		public async Task<(List<PlangWebElement>?, IError?)> GetElementsInsideElement(string elementName, IWebElement element)
		{
			if (element == null) return (null, new ProgramError("You must send in element to look inside", goalStep, function));

			return (GetPlangWebElements(element.FindElements(By.CssSelector(elementName))), null);
		}

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
		}

		private Dictionary<string, string> GetAllAttributes(IWebDriver driver, PlangWebElement element)
		{
			string script = @"
        var attributes = arguments[0].attributes;
        var result = {};
        for (var i = 0; i < attributes.length; i++) {
            result[attributes[i].name] = attributes[i].value;
        }
        return result;
    ";
			var jsExecutor = ((IJavaScriptExecutor)driver);

			// Execute the script and return the attributes as a dictionary
			var attributes = jsExecutor.ExecuteScript(script, element.WebElement);
			return ((Dictionary<string, object>)attributes)
					.ToDictionary(k => k.Key, k => k.Value?.ToString() ?? string.Empty);
		}

		public async Task<string> FindElementAndExtractAttribute(string attribute, string? cssSelector = null, PlangWebElement? element = null)
		{
			cssSelector = await GetCssSelector(cssSelector);
			List<string> results = new List<string>();

			var driver = await GetDriver();
			IWebElement ielement;

			if (element == null)
			{
				ielement = driver.FindElement(By.CssSelector(cssSelector));
			} else
			{
				ielement = element.WebElement;
			}
			return ielement.GetAttribute(attribute);
		}

		public async Task<List<string>> ExtractContent(bool clearHtml = true, string? cssSelector = null, PlangWebElement? element = null)
		{
			cssSelector = await GetCssSelector(cssSelector);
			List<string> results = new List<string>();
			
			var driver = await GetDriver();
			ReadOnlyCollection<IWebElement> elements;
			if (element != null)
			{
				elements = element.WebElement.FindElements(By.CssSelector(cssSelector));
			}
			else
			{				
				elements = driver.FindElements(By.CssSelector(cssSelector));
			}
			foreach (var e in elements)
			{
				string text = (clearHtml) ? e.GetAttribute("innerText") : e.GetAttribute("outerHTML");
				results.Add(text);
			}
			SetCssSelector(cssSelector);
			return results;
		}
		public async Task SwitchTab(int tabIndex)
		{
			var driver = await GetDriver();
			ReadOnlyCollection<string> tabs = driver.WindowHandles;
			if (tabs.Count == tabIndex)
			{
				tabIndex--;
			}

			driver.SwitchTo().Window(tabs[tabIndex]);

		}

		public async Task Wait(int milliseconds = 1000)
		{
			await Task.Delay(milliseconds);
		}

		public async Task<IError?> TakeScreenshotOfWebsite(string saveToPath, bool overwrite = false)
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

			var driver = await GetDriver();
			var screenShot = driver.GetScreenshot();
			screenShot.SaveAsFile(absolutePath);
			return null;
		}

		private string ConvertKeyCommand(string value)
		{
			if (value == "\\t") return "\t";
			if (value == "\\r") return "\r";
			if (value == "\\n") return "\n";
			return value;
		}


		private string GetCssSelector(IWebElement element, IJavaScriptExecutor js)
		{
			string tagName = element.TagName;
			string id = element.GetAttribute("id");
			string classes = element.GetAttribute("class");

			if (!string.IsNullOrEmpty(id))
			{
				return $"#{id}";
			}
			else if (!string.IsNullOrEmpty(classes))
			{
				return $"{tagName}.{string.Join('.', classes.Split(' '))}";
			} else if (!string.IsNullOrEmpty(tagName))
			{
				return tagName;
			}
			else
			{
				// This is a more advanced approach: finding the nth-child index
				var index = js.ExecuteScript(
					"return Array.from(arguments[0].parentNode.children).indexOf(arguments[0]) + 1;",
					element
				);

				string parentSelector = GetCssSelector(element.FindElement(By.XPath("..")), js);
				return $"{parentSelector} > {tagName}:nth-child({index})";
			}
		}

		private SafariDriver GetSafariDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			var options = new SafariOptions();

			return new SafariDriver(options);
		}
		private FirefoxDriver GetFirefoxDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			FirefoxOptions options = new FirefoxOptions();
			if (useUserSession && string.IsNullOrEmpty(userSessionPath))
			{
				options.AddArgument(@$"user-data-dir={GetChromeUserDataDir()}");
			}
			if (headless)
			{
				options.AddArgument("headless");
			}
			if (incognito)
			{
				options.AddArgument("incognito");
			}

			if (kioskMode)
			{
				options.AddArgument("kios");
			}

			if (argumentOptions != null)
			{
				foreach (var args in argumentOptions)
				{
					options.AddArgument(args.Key + "=" + args.Value);
				}
			}
			return new FirefoxDriver(options);
		}
		private ChromiumDriver GetEdgeDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			var options = new EdgeOptions();
			if (useUserSession && string.IsNullOrEmpty(userSessionPath))
			{
				options.AddArgument(@$"user-data-dir={GetChromeUserDataDir()}");
			}
			if (headless)
			{
				options.AddArgument("headless");
			}
			if (incognito)
			{
				options.AddArgument("incognito");
			}

			if (kioskMode)
			{
				options.AddArgument("kios");
			}
			if (argumentOptions != null)
			{
				foreach (var args in argumentOptions)
				{
					options.AddArgument(args.Key + "=" + args.Value);
				}
			}

			return new EdgeDriver(options);
		}
		private ChromiumDriver GetChromeDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions, bool hideTestingMode)
		{
			ChromeOptions options = new ChromeOptions();

			if (useUserSession && string.IsNullOrEmpty(userSessionPath))
			{
				var path = GetChromeUserDataDir();
				logger.LogDebug($"Using user path: {path}");
				options.AddArgument(@$"user-data-dir={path}");
			}
			else if (!string.IsNullOrEmpty(userSessionPath))
			{
				options.AddArgument(@$"user-data-dir={userSessionPath}");
			}
			if (headless)
			{
				options.AddArgument("headless");
			}
			if (incognito)
			{
				options.AddArgument("incognito");
			}

			if (kioskMode)
			{
				options.AddArgument("kios");
			}
			if (argumentOptions != null)
			{
				foreach (var args in argumentOptions)
				{
					options.AddArgument(args.Key + "=" + args.Value);
				}
			}
			options.SetLoggingPreference(LogType.Browser, OpenQA.Selenium.LogLevel.All);

			if (hideTestingMode)
			{
				options.AddExcludedArgument("enable-automation");
				options.AddAdditionalOption("useAutomationExtension", false);

				options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
				options.AddExcludedArgument("enable-automation");

				options.AddArgument("--disable-blink-features=AutomationControlled");
			}

			var service = ChromeDriverService.CreateDefaultService();
			service.SuppressInitialDiagnosticInformation = true;
			service.HideCommandPromptWindow = true;

			var driver = new ChromeDriver(service, options);

			if (hideTestingMode)
			{
				// After initializing the driver
				IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

				// Override the navigator.webdriver property to undefined
				js.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
				js.ExecuteScript(@" 
    Object.defineProperty(navigator, 'plugins', { 
        get: () => [1, 2, 3], 
    }); 
    Object.defineProperty(navigator, 'languages', { 
        get: () => ['en-US', 'en'], 
    });
");
			}
			return driver;
		}

	}
}
