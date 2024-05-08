using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Safari;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PLang.Modules.WebCrawlerModule
{
	[Description("Run a browser instance, browse a website, input values and click on html elements, sendkeys, wait for browser and extract content")]
	public class Program : BaseProgram, IDisposable
	{
		WebDriver? driver = null;
		private readonly IPLangFileSystem fileSystem;

		public Program(PLangAppContext context, IPLangFileSystem fileSystem) : base()
		{
			this.context = context;
			this.fileSystem = fileSystem;
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

		[Description("browserType=Chrome|Edge|Firefox|IE|Safari")]
		public async Task StartBrowser(string browserType = "Chrome", bool headless = false, bool useUserSession = false, string userSessionPath = "", 
			bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null)
		{
			driver = GetBrowserType(browserType, headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions);
			
			driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
			context.TryAdd("SeleniumBrowser", driver);
		}

		private WebDriver GetBrowserType(string browserType, bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions)
		{
			switch (browserType)
			{
				case "Edge":
					return GetEdgeDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions);
				case "Firefox":
					return GetFirefoxDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions);
				case "Safari":
					return GetSafariDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions);
				default:
					return GetChromeDriver(headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions);
			}

			
		}


		private async Task<ChromeDriver> GetDriver(string browserType = "Chrome", bool headless = false, bool useUserSession = false, string userSessionPath = "", bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null)
		{
			if (!context.ContainsKey("SeleniumBrowser"))
			{
				await StartBrowser(browserType, headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions);
			}
			return context["SeleniumBrowser"] as ChromeDriver;
		}

		private async Task<IWebElement> GetElement(string? cssSelector = null)
		{
			var driver = await GetDriver();
			if (cssSelector == null) cssSelector = GetCssSelector();

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
			if (context.ContainsKey("SeleniumBrowser"))
			{
				var driver = context["SeleniumBrowser"] as ChromeDriver;
				if (driver != null)
				{
					driver.Quit();
					context.Remove("SeleniumBrowser");
				}
			}
		}

		public async Task NavigateToUrl(string url, string browserType = "Chrome", bool headless = false, bool useUserSession = false, string userSessionPath = "", bool incognito = false, bool kioskMode = false, Dictionary<string, string>? argumentOptions = null)
		{
			if (string.IsNullOrEmpty(url))
			{
				throw new RuntimeException("url cannot be empty");
			}
			var driver = await GetDriver(browserType, headless, useUserSession, userSessionPath, incognito, kioskMode, argumentOptions);
			if (!url.StartsWith("http")) {
				url = "https://" + url;
			}
			driver.Navigate().GoToUrl(url);
		}

		public async Task SetFocus(string? cssSelector = null)
		{
			var driver = await GetDriver(cssSelector);
			var element = await GetElement(cssSelector);
			new Actions(driver).MoveToElement(element).Perform();
		}

		public async Task Click(string cssSelector)
		{
			var element = await GetElement(cssSelector);
			element.Click();
			SetCssSelector(cssSelector);
		}

		private string GetCssSelector(string? cssSelector = null)
		{
			if (string.IsNullOrEmpty(cssSelector) && context.ContainsKey("prevCssSelector"))
			{
				cssSelector = context["prevCssSelector"].ToString();
			}
		
			if (cssSelector == null)
			{
				var focusedElement = (IWebElement)((IJavaScriptExecutor)driver).ExecuteScript("return document.activeElement;");
				cssSelector = GetCssSelector(focusedElement, driver);
			}

			return cssSelector;
		}
		private void SetCssSelector(string? cssSelector)
		{
			context.AddOrReplace("prevCssSelector", cssSelector);
		}

		public async Task SendKey(string value, string? cssSelector = null)
		{
			cssSelector = GetCssSelector(cssSelector);
			var element = await GetElement(cssSelector);
			var input = ConvertKeyCommand(value);
			element.SendKeys(input);
			SetCssSelector(cssSelector);
		}

		[Description("set the value of an input by cssSelector")]
		public async Task Input(string value, string? cssSelector = null)
		{
			cssSelector = GetCssSelector(cssSelector);
			var element = await GetElement(cssSelector);
			element.SendKeys(value);
			SetCssSelector(cssSelector);
		}

		public async Task Submit(string? cssSelector = null)
		{
			cssSelector = GetCssSelector(cssSelector);
			var element = await GetElement(cssSelector);
			element.Submit();
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

		public async Task<ReadOnlyCollection<IWebElement>> GetElements(string? cssSelector = null)
		{
			cssSelector = GetCssSelector(cssSelector);
			var driver = await GetDriver();
			var elements = driver.FindElements(By.CssSelector(cssSelector));

			return elements;
		}
		
		public async Task<List<string>> ExtractContent(bool clearHtml = false, string? cssSelector = null)
		{		
			cssSelector = GetCssSelector(cssSelector);
			List<string> results = new List<string>();
			var driver = await GetDriver();
			var elements = driver.FindElements(By.CssSelector(cssSelector));
			foreach (var e in elements)
			{
				string text = e.Text;
				if (clearHtml)
				{
					text = text.ClearHtml();
				}
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

		public async Task TakeScreenshotOfWebsite(string saveToPath)
		{
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(saveToPath)))
			{
				return;
			}

			var screenShot = driver.GetScreenshot();
			screenShot.SaveAsFile(saveToPath);
		}

		private string ConvertKeyCommand(string value)
		{
			if (value == "\\t") return "\t";
			if (value == "\\r") return "\r";
			if (value == "\\n") return "\n";
			return value;
		}

		public void Dispose()
		{
			CloseBrowser().Wait();
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

		private SafariDriver GetSafariDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions)
		{
			var options = new SafariOptions();

			return new SafariDriver(options);
		}
		private FirefoxDriver GetFirefoxDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions)
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
		private ChromiumDriver GetEdgeDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string> argumentOptions)
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
		private ChromiumDriver GetChromeDriver(bool headless, bool useUserSession, string userSessionPath, bool incognito, bool kioskMode, Dictionary<string, string>? argumentOptions)
		{
			ChromeOptions options = new ChromeOptions();

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

			return new ChromeDriver(options);
		}

	}
}
