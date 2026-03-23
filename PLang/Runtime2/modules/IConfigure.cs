using PLang.Runtime2.Engine.Settings;

namespace PLang.Runtime2.modules;

/// <summary>
/// Marks a configure action and links it to its IConfig class.
/// The builder uses this to reflect on TConfig for filling defaults
/// instead of reflecting on the action record itself.
/// </summary>
public interface IConfigure<TConfig> where TConfig : IConfig, new() { }
