// netstandard2.0 polyfill: records use init-only setters which require this type.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
