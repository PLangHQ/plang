using System.Reflection;

namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 1
// [Out] discipline applied per the wire-out-attributes inventory.
// One assertion per (type, property) in the 13-type inventory.
// Properties without [Out] are excluded from the wire by Stage 2's filter — so the
// presence/absence of [Out] here IS the contract.

public class OutAttributeInventoryTests
{
    private static bool HasOut(System.Type t, string prop)
        => t.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
            ?.IsDefined(typeof(global::app.OutAttribute), inherit: true) ?? false;

    private static bool HasMasked(System.Type t, string prop)
        => t.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
            ?.IsDefined(typeof(global::app.MaskedAttribute), inherit: true) ?? false;

    // 1. Identity ------------------------------------------------------------
    [Test] public async Task Identity_Name_HasOut()
    {
        await Assert.That(HasOut(typeof(global::app.module.identity.Identity), "Name")).IsTrue();
    }
    [Test] public async Task Identity_PublicKey_HasOut()
    {
        await Assert.That(HasOut(typeof(global::app.module.identity.Identity), "PublicKey")).IsTrue();
    }
    [Test] public async Task Identity_PrivateKey_StaysSensitive_NoOut()
    {
        await Assert.That(HasOut(typeof(global::app.module.identity.Identity), "PrivateKey")).IsFalse();
    }
    [Test] public async Task Identity_IsDefault_IsArchived_Created_NotOut()
    {
        var t = typeof(global::app.module.identity.Identity);
        await Assert.That(HasOut(t, "IsDefault")).IsFalse();
        await Assert.That(HasOut(t, "IsArchived")).IsFalse();
        await Assert.That(HasOut(t, "Created")).IsFalse();
    }

    // 2. path / FilePath / HttpPath -----------------------------------------
    [Test] public async Task Path_Scheme_HasOut()
    {
        await Assert.That(HasOut(typeof(global::app.type.path.@this), "Scheme")).IsTrue();
    }
    [Test] public async Task Path_Relative_HasOut()
    {
        // Stage 3: Relative went internal (the wire is the type-owned single
        // location string; containment goes through IsUnder/Matches) — no [Out].
        await Assert.That(HasOut(typeof(global::app.type.path.@this), "Relative")).IsFalse();
    }
    [Test] public async Task Path_Absolute_NotOut_LeaksFilesystemLayout()
    {
        await Assert.That(HasOut(typeof(global::app.type.path.@this), "Absolute")).IsFalse();
    }
    [Test] public async Task Path_Raw_NotOut()
    {
        await Assert.That(HasOut(typeof(global::app.type.path.@this), "Raw")).IsFalse();
    }
    [Test] public async Task Path_DerivedProps_NotOut_Extension_FileName_Directory_MimeType_IsFile_IsDirectory()
    {
        var t = typeof(global::app.type.path.@this);
        foreach (var p in new[] { "Extension", "FileName", "FileNameWithoutExtension", "Directory", "MimeType", "IsFile", "IsDirectory" })
            await Assert.That(HasOut(t, p)).IsFalse().Because($"Path.{p} is derived; receiver recomputes");
    }
    [Test] public async Task Path_Content_Source_NotOut()
    {
        var t = typeof(global::app.type.path.@this);
        await Assert.That(HasOut(t, "Content")).IsFalse();
        await Assert.That(HasOut(t, "Source")).IsFalse();
    }
    [Test] public async Task Path_GoalCall_Context_StayJsonIgnore_NoOut()
    {
        var t = typeof(global::app.type.path.@this);
        await Assert.That(HasOut(t, "GoalCall")).IsFalse();
        await Assert.That(HasOut(t, "Context")).IsFalse();
    }

    // 3. list (module.list.types.list) -------------------------------------
    [Test] public async Task List_Count_HasOut()
    {
        await Assert.That(HasOut(typeof(global::app.module.list.type.list), "count")).IsTrue();
    }
    [Test] public async Task List_Value_HasOut()
    {
        await Assert.That(HasOut(typeof(global::app.module.list.type.list), "value")).IsTrue();
    }

    // 4. Variable ------------------------------------------------------------
    [Test] public async Task Variable_Name_HasOut()
    {
        await Assert.That(HasOut(typeof(global::app.variable.@this), "Name")).IsTrue();
    }
    [Test] public async Task Variable_RawValue_WasPercentWrapped_NotOut()
    {
        var t = typeof(global::app.variable.@this);
        await Assert.That(HasOut(t, "RawValue")).IsFalse();
        await Assert.That(HasOut(t, "WasPercentWrapped")).IsFalse();
    }

    // 5. Data itself ---------------------------------------------------------
    [Test] public async Task Data_Value_Success_Error_Type_HaveOut()
    {
        var t = typeof(global::app.data.@this);
        // Value is the async door (a method), not a property — the wire writes the
        // value slot by hand (Wire.Write), so no [Out] property exists for it.
        await Assert.That(t.GetProperty("Value")).IsNull();
        await Assert.That(HasOut(t, "Success")).IsTrue();
        await Assert.That(HasOut(t, "Error")).IsTrue();
        await Assert.That(HasOut(t, "Type")).IsTrue();
    }
    [Test] public async Task Data_Properties_HasOut_FormerlyJsonIgnore()
    {
        await Assert.That(HasOut(typeof(global::app.data.@this), "Properties")).IsTrue();
    }
    [Test] public async Task Data_Context_NotOut_RuntimeGraph()
    {
        await Assert.That(HasOut(typeof(global::app.data.@this), "Context")).IsFalse();
    }

    // 6. StatInfo ------------------------------------------------------------
    [Test] public async Task StatInfo_Exists_IsFile_Length_Modified_HaveOut()
    {
        var t = typeof(global::app.type.path.@this.StatInfo);
        await Assert.That(HasOut(t, "Exists")).IsTrue();
        await Assert.That(HasOut(t, "IsFile")).IsTrue();
        await Assert.That(HasOut(t, "Length")).IsTrue();
        await Assert.That(HasOut(t, "Modified")).IsTrue();
    }

    // 7. GoalCall ------------------------------------------------------------
    [Test] public async Task GoalCall_Name_Parallel_Parameters_PrPath_HaveOut()
    {
        var t = typeof(global::app.goal.GoalCall);
        await Assert.That(HasOut(t, "Name")).IsTrue();
        await Assert.That(HasOut(t, "Parallel")).IsTrue();
        await Assert.That(HasOut(t, "Parameters")).IsTrue();
        await Assert.That(HasOut(t, "PrPath")).IsTrue();
    }
    [Test] public async Task GoalCall_Event_Action_StayJsonIgnore_NoOut()
    {
        var t = typeof(global::app.goal.GoalCall);
        await Assert.That(HasOut(t, "Event")).IsFalse();
        await Assert.That(HasOut(t, "Action")).IsFalse();
    }

    // 8. permission ----------------------------------------------------------
    [Test] public async Task Permission_Actor_Path_Verb_Match_HaveOut()
    {
        var t = typeof(global::app.type.permission.@this);
        await Assert.That(HasOut(t, "Actor")).IsTrue();
        await Assert.That(HasOut(t, "Path")).IsTrue();
        await Assert.That(HasOut(t, "Verbs")).IsTrue();
        await Assert.That(HasOut(t, "Match")).IsTrue();
    }

    // 9. setting -------------------------------------------------------------
    [Test] public async Task Setting_Key_HasOut()
    {
        await Assert.That(HasOut(typeof(global::app.module.setting.type.setting), "key")).IsTrue();
    }
    [Test] public async Task Setting_Value_HasOut_AndMasked()
    {
        var t = typeof(global::app.module.setting.type.setting);
        await Assert.That(HasOut(t, "value")).IsTrue();
        await Assert.That(HasMasked(t, "value")).IsTrue();
    }

    // 10. http.Response dissolved (Decision 6) — body is the lazy Data value,
    //     status/headers/duration are Properties; no record [Out] inventory.

    // 11. Ask ----------------------------------------------------------------
    [Test] public async Task Ask_Answer_HasOut()
    {
        await Assert.That(HasOut(typeof(global::app.module.output.Ask), "Answer")).IsTrue();
    }

    // 12. Mock ---------------------------------------------------------------
    [Test] public async Task Mock_NoOutProperties_TestOnlyType()
    {
        var t = typeof(global::app.mock.@this);
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            await Assert.That(p.IsDefined(typeof(global::app.OutAttribute), inherit: true))
                .IsFalse()
                .Because($"Mock.{p.Name} is local test state; nothing on Mock should ship");
    }

    // 13. condition.Operator -------------------------------------------------
    [Test] public async Task ConditionOperator_Value_HasOut()
    {
        await Assert.That(HasOut(typeof(global::app.module.condition.Operator), "Value")).IsTrue();
    }
    [Test] public async Task ConditionOperator_Evaluate_NotOut_Delegate()
    {
        await Assert.That(HasOut(typeof(global::app.module.condition.Operator), "Evaluate")).IsFalse();
    }
}
