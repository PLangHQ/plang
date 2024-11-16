namespace Plang.Channels
{
	/// <summary>
	/// Provides helper methods for working with categories.
	/// </summary>
	public static class CategoryHelper
	{
		/// <summary>
		/// Parses a collection of category names into a list of Category enums.
		/// </summary>
		/// <param name="categoryNames">A collection of category names as strings.</param>
		/// <returns>A list of Category enums.</returns>
		public static List<Category> ParseCategories(IEnumerable<string> categoryNames)
		{
			var categories = new List<Category>();

			foreach (var name in categoryNames)
			{
				if (Enum.TryParse(name, true, out Category category))
				{
					categories.Add(category);
				}
				else
				{
					throw new ArgumentException($"Unknown category: {name}");
				}
			}

			return categories;
		}
	}
}
