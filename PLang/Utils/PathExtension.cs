namespace PLang.Utils;

public static class PathExtension
{
    public static string AdjustPathToOs(this string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        if (Path.DirectorySeparatorChar == '\\') return path.Replace('/', Path.DirectorySeparatorChar);

        return path.Replace('\\', Path.DirectorySeparatorChar);
    }

    public static string RemoveExtension(this string path)
    {
        if (!path.Contains(".")) return path;
        return path.Substring(0, path.LastIndexOf("."));
    }
}