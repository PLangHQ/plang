using Microsoft.Playwright;
using System.Runtime.Serialization;

namespace PLang.Modules.WebCrawlerModule
{
	public class PlangWebElement
	{
		// Properties
		public string ComputedAccessibleLabel { get; set; }
		public string ComputedAccessibleRole { get; set; }

		public Coordinates Coordinates { get; set; }
		public bool Displayed { get; set; }
		public bool Enabled { get; set; }
		public string Id { get; set; }
		public Location Location { get; set; }
		public Location LocationOnScreenOnceScrolledIntoView { get; set; }
		public bool Selected { get; set; }
		public Size Size { get; set; }
		public string TagName { get; set; }
		public string Text { get; set; }
		public string InnerHtml { get; set; }	

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public IElementHandle WebElement { get; set; }
		public string InnerText { get; internal set; }
	}

	// Coordinates class
	public class Coordinates
	{
		public string AuxiliaryLocator { get; set; }
		public Location LocationInDom { get; set; }
		public Location LocationInViewport { get; set; }
	}

	// Location class
	public class Location
	{
		public int X { get; set; }
		public int Y { get; set; }
	}

	// Size class
	public class Size
	{
		public int Width { get; set; }
		public int Height { get; set; }
	}

}
