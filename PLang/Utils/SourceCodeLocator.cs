namespace PLang.Utils;

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

public class SourceCodeLocator
{
	public static TypeInformation? GetTypeInformationFromRuntime(Type type)
	{
		var sourceFile = GetSourceFileForType(type);
		if (sourceFile == null || !File.Exists(sourceFile))
			return null;

		var sourceCode = File.ReadAllText(sourceFile);
		var parser = new CSharpParser();
		parser.LoadCode(sourceCode);

		return parser.GetType(type.Name);
	}

	public static string? GetSourceFileForType(Type type)
	{
		var assemblyPath = type.Assembly.Location;
		var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");

		if (!File.Exists(pdbPath))
			return null;

		try
		{
			using var pdbStream = File.OpenRead(pdbPath);
			using var pdbReader = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
			var metadata = pdbReader.GetMetadataReader();

			// Find any method or constructor to get file location
			var method = GetFirstMethod(type);
			if (method == null)
				return null;

			var methodToken = method.MetadataToken;
			var handle = MetadataTokens.MethodDefinitionHandle(methodToken);
			var debugInfo = metadata.GetMethodDebugInformation(
				MetadataTokens.MethodDebugInformationHandle(MetadataTokens.GetRowNumber(handle)));

			if (debugInfo.Document.IsNil)
				return null;

			var document = metadata.GetDocument(debugInfo.Document);
			return metadata.GetString(document.Name);
		}
		catch
		{
			return null;
		}
	}

	private static MethodBase? GetFirstMethod(Type type)
	{
		var flags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

		// Try methods first
		var method = type.GetMethods(flags).FirstOrDefault();
		if (method != null)
			return method;

		// Try constructors
		var ctor = type.GetConstructors(flags).FirstOrDefault();
		if (ctor != null)
			return ctor;

		// Try property getters/setters
		var prop = type.GetProperties(flags).FirstOrDefault();
		if (prop != null)
			return prop.GetGetMethod(true) ?? prop.GetSetMethod(true);

		return null;
	}
}