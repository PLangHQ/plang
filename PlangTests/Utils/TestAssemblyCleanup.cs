﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Services.SettingsService;

namespace PLangTests.Utils;

[TestClass]
public static class TestAssemblyCleanup
{
    public static void CloseAllConnectionsAndClearCache()
    {
        foreach (var entry in SqliteSettingsRepository.InMemoryDbCache) entry.Value.Close();

        SqliteSettingsRepository.InMemoryDbCache.Clear();
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        CloseAllConnectionsAndClearCache();
    }
}