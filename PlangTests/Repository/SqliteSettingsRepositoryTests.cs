using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using PLang.Models;
using PLang.Services.SettingsService;
using PLang.Utils;
using PLangTests;

namespace PLang.Repository.Tests;

[TestClass]
public class SqliteSettingsRepositoryTests : BasePLangTest
{
    [TestInitialize]
    public void Init()
    {
        Initialize();
        context.AddOrReplace(Settings.SaltKey, "123");
    }

    [TestMethod]
    public void TestSetData()
    {
        var appId = "123";
        var key = "Dunder";
        var module = "SqliteSettingsRepositoryTests";
        var value = new DunderMifflin("Micheal");

        var settingsRepository = new SqliteSettingsRepository(fileSystem, context, logger);
        settingsRepository.Init();

        var setting = settingsRepository.GetSettings().FirstOrDefault(p => p.Key == key);

        settingsRepository.Remove(setting);

        var dict = new Dictionary<string, object>();
        dict.Add("Test", "hello");
        setting = new Setting(appId, module, typeof(string).Name, key, JsonConvert.SerializeObject(value), dict);

        settingsRepository.Set(setting);

        var returnedSetting = settingsRepository.GetSettings().FirstOrDefault(p => p.Key == key);
        Assert.AreEqual(setting.Key, returnedSetting.Key);
        Assert.AreEqual(setting.Value, returnedSetting.Value);
        Assert.AreEqual(setting.ValueType, returnedSetting.ValueType);
        Assert.AreEqual(setting.AppId, returnedSetting.AppId);
        Assert.AreEqual(setting.Created, returnedSetting.Created);
        Assert.AreEqual(setting.ClassOwnerFullName, returnedSetting.ClassOwnerFullName);
        //Assert.AreEqual("hello", returnedSetting.Signature["Test"]);
    }


    [TestMethod]
    public void TestMultipleSets()
    {
        var appId = "123";
        var key = "Dunder";
        var classType = "SqliteSettingsRepositoryTests";
        var value = new DunderMifflin("Micheal");

        var settingsRepository = new SqliteSettingsRepository(fileSystem, context, logger);
        settingsRepository.Init();

        Assert.IsNotNull(context[Settings.SaltKey]);

        var settings = settingsRepository.GetSettings().Where(p => p.ClassOwnerFullName == classType);
        foreach (var setting in settings) settingsRepository.Remove(setting);
        List<DunderMifflin> list = new();
        list.Add(value);
        var now = DateTime.Now;
        SystemTime.Now = () => { return now; };

        var setting2 = new Setting(appId, classType, typeof(string).Name, key, JsonConvert.SerializeObject(list));
        settingsRepository.Set(setting2);

        var returnedSetting = settingsRepository.GetSettings().FirstOrDefault(p => p.ClassOwnerFullName == classType);
        Assert.AreEqual(setting2.Key, returnedSetting.Key);

        list = JsonConvert.DeserializeObject<List<DunderMifflin>>(returnedSetting.Value);
        Assert.AreEqual(value.name, list[0].name);
        Assert.AreEqual(setting2.ValueType, returnedSetting.ValueType);
        Assert.AreEqual(setting2.AppId, returnedSetting.AppId);
        Assert.AreEqual(setting2.Created, returnedSetting.Created);
        Assert.AreEqual(setting2.ClassOwnerFullName, returnedSetting.ClassOwnerFullName);

        var value2 = new DunderMifflin("Pamela");
        list.Add(value2);
        setting2 = new Setting(appId, classType, typeof(string).Name, key, JsonConvert.SerializeObject(list),
            returnedSetting.Signature, returnedSetting.Created);

        settingsRepository.Set(setting2);

        var moreSettings = settingsRepository.GetSettings().ToList();
        Assert.AreEqual(2, JsonConvert.DeserializeObject<List<DunderMifflin>>(moreSettings[0].Value).Count);
    }


    public record DunderMifflin(string name);
}