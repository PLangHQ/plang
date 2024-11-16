using System.Reflection;

namespace PLang.Utils;

internal static class MethodCache
{
    public static Dictionary<string, MethodInfo> Cache = new();
}