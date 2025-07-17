using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PLang.Modules.WebserverModule
{
	public static class UserAgentHelper
	{
		public static string GetUserAgentMode(string? userAgent)
		{
			if (userAgent == null) return "desktop";

			if (IsBot(userAgent)) return "bot";
			if (IsMobile(userAgent)) return "mobile";
			if (IsTablet(userAgent)) return "tablet";
			return "desktop";
		}
		public static bool IsBot(string userAgent)
			=> userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
			   userAgent.Contains("crawl", StringComparison.OrdinalIgnoreCase) ||
			   userAgent.Contains("spider", StringComparison.OrdinalIgnoreCase);

		public static bool IsMobile(string userAgent)
			=> Regex.IsMatch(userAgent, "Android|iPhone|iPod|Windows Phone", RegexOptions.IgnoreCase);

		public static bool IsTablet(string userAgent)
			=> Regex.IsMatch(userAgent, "iPad|Tablet|Nexus 7|Nexus 10|Kindle", RegexOptions.IgnoreCase);

		public static bool IsDesktop(string userAgent)
			=> !IsMobile(userAgent) && !IsTablet(userAgent) && !IsBot(userAgent);
	}
}
