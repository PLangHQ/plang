using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Models.ObjectValueExtractors;
using RazorEngineCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueExtractors.Tests
{
	[TestClass()]
	public class HtmlExtractorTests
	{
		[TestMethod()]
		public void PatternToXPathTest()
		{
			var testCases = new List<(string Pattern, string XPath)>
				{
					("html.head.title", "//html/head/title"),
					("body.table#tbl", "//body/table[@id='tbl']"),
					("table#tbl.td", "//table[@id='tbl']/td"),
					("body.table[class=\"abc def\"]", "//body/table[@class='abc def']"),
					("body.table[class=\"abc def\", border=\"1\"]", "//body/table[@class='abc def'][@border='1']"),
					("table[border=\"1\"].tr[align=\"left\"]", "//table[@border='1']/tr[@align='left']"),
					("body.div#main[role=\"main\"]", "//body/div[@id='main'][@role='main']"),
					("div#content.section[class=\"foo\"]", "//div[@id='content']/section[@class='foo']"),
					("table#tbl.td[colspan=\"2\"]", "//table[@id='tbl']/td[@colspan='2']"),
					("ul#myList.li[class=\"item\"]", "//ul[@id='myList']/li[@class='item']"),
					("span[style=\"color:red\"]", "//span[@style='color:red']"),
					("div#foo[class=\"bar\", data-id=\"123\"]", "//div[@id='foo'][@class='bar'][@data-id='123']"),
					("body.div#main.section[id=\"mainSection\"]", "//body/div[@id='main']/section[@id='mainSection']")
				};

			var htmlExtractor = new HtmlExtractor(new ObjectTypes.HtmlType(""), new Runtime.ObjectValue("ble", ""));

			foreach (var test in testCases)
			{
				var result = htmlExtractor.PatternToXPath(test.Pattern);
				Assert.AreEqual(test.XPath, result);
			}


		}
	}
}