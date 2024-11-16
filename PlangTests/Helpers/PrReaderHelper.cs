using System.Reflection;

namespace PLangTests.Helpers;

public class PrReaderHelper
{
    public static string GetPrFileRaw(string fileName)
    {
        // Get the current assembly's directory
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // Combine with the relative path to the examples folder
        var filePath = Path.Combine(assemblyDirectory, "PrFiles", fileName);

        return File.ReadAllText(filePath);
    }
}